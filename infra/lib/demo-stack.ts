import * as cdk from "aws-cdk-lib";
import * as apigw from "aws-cdk-lib/aws-apigateway";
import * as apigwv2 from "aws-cdk-lib/aws-apigatewayv2";
import * as acm from "aws-cdk-lib/aws-certificatemanager";
import * as cloudfront from "aws-cdk-lib/aws-cloudfront";
import * as origins from "aws-cdk-lib/aws-cloudfront-origins";
import * as cloudwatch from "aws-cdk-lib/aws-cloudwatch";
import * as ec2 from "aws-cdk-lib/aws-ec2";
import * as ecr from "aws-cdk-lib/aws-ecr";
import * as ecs from "aws-cdk-lib/aws-ecs";
import * as elbv2 from "aws-cdk-lib/aws-elasticloadbalancingv2";
import * as logs from "aws-cdk-lib/aws-logs";
import * as rds from "aws-cdk-lib/aws-rds";
import * as route53 from "aws-cdk-lib/aws-route53";
import * as targets from "aws-cdk-lib/aws-route53-targets";
import * as s3 from "aws-cdk-lib/aws-s3";
import * as s3deploy from "aws-cdk-lib/aws-s3-deployment";
import * as secretsmanager from "aws-cdk-lib/aws-secretsmanager";
import { Construct } from "constructs";
import { DemoConfig, siteOrigin } from "./config";

export interface DemoStackProps extends cdk.StackProps {
  config: DemoConfig;
  siteCertificate: acm.ICertificate;
  repository: ecr.IRepository;
  /** The API image tag to run: the commit SHA. */
  imageTag: string;
  /** The built frontend (client/dist, built with VITE_API_BASE_URL pointing at the API domain). */
  siteAssetsPath: string;
}

const DATABASE_NAME = "room_booking";
const MASTER_USER = "postgres";
const APP_USER = "room_booking_app";
const API_PORT = 8080;

/**
 * The demo: CloudFront + S3 for the frontend, and API Gateway -> VPC link -> internal ALB -> Fargate -> RDS for
 * the API. Everything is created to be destroyed: no deletion protection, backups, or retained data.
 */
export class DemoStack extends cdk.Stack {
  // Fixed rather than looked up, so synth needs no AWS credentials.
  override get availabilityZones(): string[] {
    return [`${this.region}a`, `${this.region}b`];
  }

  constructor(scope: Construct, id: string, props: DemoStackProps) {
    super(scope, id, { ...props, crossRegionReferences: true });
    const { config } = props;
    const origin = siteOrigin(config);

    const zone = route53.HostedZone.fromHostedZoneAttributes(this, "Zone", {
      hostedZoneId: config.hostedZoneId,
      zoneName: config.zoneName,
    });

    // Network. No NAT Gateway: tasks run in public subnets with a public IP (to reach ECR, Secrets Manager, and
    // CloudWatch Logs) but only accept traffic from the ALB. The database and ALB sit in isolated subnets.
    const vpc = new ec2.Vpc(this, "Vpc", {
      maxAzs: 2,
      natGateways: 0,
      subnetConfiguration: [
        { name: "public", subnetType: ec2.SubnetType.PUBLIC, cidrMask: 24 },
        { name: "isolated", subnetType: ec2.SubnetType.PRIVATE_ISOLATED, cidrMask: 24 },
      ],
    });
    const isolated = { subnetType: ec2.SubnetType.PRIVATE_ISOLATED };

    const vpcLinkSecurityGroup = new ec2.SecurityGroup(this, "VpcLinkSecurityGroup", {
      vpc,
      description: "API Gateway VPC link",
      allowAllOutbound: false,
    });
    const albSecurityGroup = new ec2.SecurityGroup(this, "AlbSecurityGroup", {
      vpc,
      description: "Internal ALB: only reachable from the API Gateway VPC link",
      allowAllOutbound: false,
    });
    const taskSecurityGroup = new ec2.SecurityGroup(this, "TaskSecurityGroup", {
      vpc,
      description: "API and migration tasks: inbound only from the ALB",
    });
    const databaseSecurityGroup = new ec2.SecurityGroup(this, "DatabaseSecurityGroup", {
      vpc,
      description: "RDS: inbound only from the tasks",
      allowAllOutbound: false,
    });
    albSecurityGroup.addIngressRule(vpcLinkSecurityGroup, ec2.Port.tcp(80), "From the VPC link");
    vpcLinkSecurityGroup.addEgressRule(albSecurityGroup, ec2.Port.tcp(80), "To the ALB");
    databaseSecurityGroup.addIngressRule(taskSecurityGroup, ec2.Port.tcp(5432), "From the tasks");

    // Database. The master credential is only given to the migration task; the API uses APP_USER.
    const database = new rds.DatabaseInstance(this, "Database", {
      engine: rds.DatabaseInstanceEngine.postgres({ version: rds.PostgresEngineVersion.VER_17 }),
      instanceType: ec2.InstanceType.of(ec2.InstanceClass.T4G, ec2.InstanceSize.MICRO),
      vpc,
      vpcSubnets: isolated,
      securityGroups: [databaseSecurityGroup],
      credentials: rds.Credentials.fromGeneratedSecret(MASTER_USER),
      databaseName: DATABASE_NAME,
      allocatedStorage: 20,
      storageType: rds.StorageType.GP3,
      storageEncrypted: true,
      multiAz: false,
      publiclyAccessible: false,
      backupRetention: cdk.Duration.days(0),
      deleteAutomatedBackups: true,
      deletionProtection: false,
      removalPolicy: cdk.RemovalPolicy.DESTROY,
    });
    const masterSecret = database.secret!;

    const appUserSecret = new secretsmanager.Secret(this, "AppUserSecret", {
      description: `Password for the ${APP_USER} database role used by the API`,
      generateSecretString: {
        secretStringTemplate: JSON.stringify({ username: APP_USER }),
        generateStringKey: "password",
        excludePunctuation: true,
        passwordLength: 32,
      },
      removalPolicy: cdk.RemovalPolicy.DESTROY,
    });

    // The password is not in the connection string: ECS injects it as PGPASSWORD, which Npgsql falls back to.
    // RDS requires TLS; Require encrypts without validating the server certificate (accepted for the demo).
    const connectionString = (user: string) =>
      `Host=${database.dbInstanceEndpointAddress};Port=${database.dbInstanceEndpointPort};` +
      `Database=${DATABASE_NAME};Username=${user};SSL Mode=Require`;

    // Containers.
    const cluster = new ecs.Cluster(this, "Cluster", { vpc });
    const logGroup = new logs.LogGroup(this, "ApiLogs", {
      retention: logs.RetentionDays.ONE_WEEK,
      removalPolicy: cdk.RemovalPolicy.DESTROY,
    });
    const image = ecs.ContainerImage.fromEcrRepository(props.repository, props.imageTag);
    const taskSize = {
      cpu: 256,
      memoryLimitMiB: 512,
      runtimePlatform: {
        cpuArchitecture: ecs.CpuArchitecture.ARM64,
        operatingSystemFamily: ecs.OperatingSystemFamily.LINUX,
      },
    };

    const apiTask = new ecs.FargateTaskDefinition(this, "ApiTask", taskSize);
    apiTask.addContainer("api", {
      image,
      logging: ecs.LogDrivers.awsLogs({ streamPrefix: "api", logGroup }),
      portMappings: [{ containerPort: API_PORT }],
      environment: {
        ASPNETCORE_ENVIRONMENT: "Production",
        Database__Migrations: "Skip",
        Cors__AllowedOrigins__0: origin,
        ConnectionStrings__DefaultConnection: connectionString(APP_USER),
      },
      secrets: { PGPASSWORD: ecs.Secret.fromSecretsManager(appUserSecret, "password") },
    });

    // Run once per deploy (scripts/deploy.sh): migrates, provisions APP_USER, and exits.
    const migrateTask = new ecs.FargateTaskDefinition(this, "MigrateTask", taskSize);
    migrateTask.addContainer("migrate", {
      image,
      essential: true,
      logging: ecs.LogDrivers.awsLogs({ streamPrefix: "migrate", logGroup }),
      environment: {
        ASPNETCORE_ENVIRONMENT: "Production",
        Database__Migrations: "Only",
        Database__AppRole__Name: APP_USER,
        ConnectionStrings__DefaultConnection: connectionString(MASTER_USER),
      },
      secrets: {
        PGPASSWORD: ecs.Secret.fromSecretsManager(masterSecret, "password"),
        Database__AppRole__Password: ecs.Secret.fromSecretsManager(appUserSecret, "password"),
      },
    });

    const service = new ecs.FargateService(this, "ApiService", {
      cluster,
      taskDefinition: apiTask,
      desiredCount: 1,
      assignPublicIp: true,
      vpcSubnets: { subnetType: ec2.SubnetType.PUBLIC },
      securityGroups: [taskSecurityGroup],
      minHealthyPercent: 100,
      maxHealthyPercent: 200,
      circuitBreaker: { enable: true, rollback: true },
      healthCheckGracePeriod: cdk.Duration.seconds(60),
    });

    // Internal ALB: no public address. API Gateway reaches it through the VPC link.
    const alb = new elbv2.ApplicationLoadBalancer(this, "Alb", {
      vpc,
      internetFacing: false,
      vpcSubnets: isolated,
      securityGroup: albSecurityGroup,
    });
    const listener = alb.addListener("Http", { port: 80, open: false });
    const targetGroup = listener.addTargets("Api", {
      port: API_PORT,
      protocol: elbv2.ApplicationProtocol.HTTP,
      targets: [service],
      deregistrationDelay: cdk.Duration.seconds(10),
      healthCheck: {
        path: "/health",
        healthyHttpCodes: "200",
        interval: cdk.Duration.seconds(15),
        healthyThresholdCount: 2,
        unhealthyThresholdCount: 3,
      },
    });

    // API Gateway REST API -> VPC link V2 -> ALB. ANY on /api/{proxy+} forwards every method (including OPTIONS,
    // so the API answers CORS preflights), the path, query string, headers, and body unchanged.
    const vpcLink = new apigwv2.VpcLink(this, "VpcLink", {
      vpc,
      subnets: isolated,
      securityGroups: [vpcLinkSecurityGroup],
    });

    const api = new apigw.RestApi(this, "Api", {
      restApiName: "riva-room-booking-demo",
      description: `Room Booking demo API (teardown by ${config.teardownBy})`,
      endpointConfiguration: { types: [apigw.EndpointType.REGIONAL] },
      disableExecuteApiEndpoint: true,
      cloudWatchRole: false,
      deployOptions: {
        stageName: "prod",
        cachingEnabled: false,
        metricsEnabled: true,
        throttlingRateLimit: 20,
        throttlingBurstLimit: 40,
      },
    });

    // The L2 Integration only knows VPC link V1 (NLB), so the V2 settings are applied as overrides below.
    const proxy = api.root.addResource("api").addResource("{proxy+}");
    const method = proxy.addMethod(
      "ANY",
      new apigw.Integration({
        type: apigw.IntegrationType.HTTP_PROXY,
        integrationHttpMethod: "ANY",
        uri: `http://${alb.loadBalancerDnsName}/api/{proxy}`,
        options: {
          requestParameters: { "integration.request.path.proxy": "method.request.path.proxy" },
          passthroughBehavior: apigw.PassthroughBehavior.WHEN_NO_MATCH,
        },
      }),
      { requestParameters: { "method.request.path.proxy": true } },
    );
    const cfnMethod = method.node.defaultChild as apigw.CfnMethod;
    cfnMethod.addPropertyOverride("Integration.ConnectionType", "VPC_LINK");
    cfnMethod.addPropertyOverride("Integration.ConnectionId", vpcLink.vpcLinkId);
    cfnMethod.addPropertyOverride("Integration.IntegrationTarget", alb.loadBalancerArn);
    // Overrides aren't part of the deployment's hash, so redeploy the stage when they change.
    api.latestDeployment?.addToLogicalId({ connection: "vpc-link-v2", target: "alb" });

    // Errors API Gateway produces itself (throttling, missing route, integration failure) need CORS headers too,
    // or the browser hides them from the frontend. Responses from the API carry the API's own CORS headers.
    const corsHeaders = { "Access-Control-Allow-Origin": `'${origin}'`, Vary: "'Origin'" };
    api.addGatewayResponse("Default4xx", { type: apigw.ResponseType.DEFAULT_4XX, responseHeaders: corsHeaders });
    api.addGatewayResponse("Default5xx", { type: apigw.ResponseType.DEFAULT_5XX, responseHeaders: corsHeaders });

    const apiCertificate = new acm.Certificate(this, "ApiCertificate", {
      domainName: config.apiDomain,
      validation: acm.CertificateValidation.fromDns(zone),
    });
    const apiDomain = api.addDomainName("ApiDomain", {
      domainName: config.apiDomain,
      certificate: apiCertificate,
      endpointType: apigw.EndpointType.REGIONAL,
      securityPolicy: apigw.SecurityPolicy.TLS_1_2,
    });
    new route53.ARecord(this, "ApiRecord", {
      zone,
      recordName: config.apiDomain,
      target: route53.RecordTarget.fromAlias(new targets.ApiGatewayDomain(apiDomain)),
    });

    // Frontend: private bucket, read by CloudFront through Origin Access Control.
    const siteBucket = new s3.Bucket(this, "SiteBucket", {
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      encryption: s3.BucketEncryption.S3_MANAGED,
      enforceSSL: true,
      removalPolicy: cdk.RemovalPolicy.DESTROY,
      autoDeleteObjects: true,
    });
    const distribution = new cloudfront.Distribution(this, "Site", {
      comment: `Room Booking demo (teardown by ${config.teardownBy})`,
      defaultBehavior: {
        origin: origins.S3BucketOrigin.withOriginAccessControl(siteBucket),
        viewerProtocolPolicy: cloudfront.ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
        cachePolicy: cloudfront.CachePolicy.CACHING_OPTIMIZED,
      },
      defaultRootObject: "index.html",
      domainNames: [config.siteDomain],
      certificate: props.siteCertificate,
      minimumProtocolVersion: cloudfront.SecurityPolicyProtocol.TLS_V1_2_2021,
      priceClass: cloudfront.PriceClass.PRICE_CLASS_100,
    });
    new s3deploy.BucketDeployment(this, "SiteContent", {
      sources: [s3deploy.Source.asset(props.siteAssetsPath)],
      destinationBucket: siteBucket,
      distribution,
      distributionPaths: ["/*"],
    });
    for (const [id, Record] of [["SiteRecord", route53.ARecord], ["SiteRecordIpv6", route53.AaaaRecord]] as const) {
      new Record(this, id, {
        zone,
        recordName: config.siteDomain,
        target: route53.RecordTarget.fromAlias(new targets.CloudFrontTarget(distribution)),
      });
    }

    // Alarms without notifications: visible in the console during the demo, at no meaningful cost.
    new cloudwatch.Alarm(this, "ApiUnhealthy", {
      alarmDescription: "No healthy API task behind the ALB",
      metric: targetGroup.metrics.healthyHostCount({ period: cdk.Duration.minutes(1) }),
      comparisonOperator: cloudwatch.ComparisonOperator.LESS_THAN_THRESHOLD,
      threshold: 1,
      evaluationPeriods: 3,
      treatMissingData: cloudwatch.TreatMissingData.BREACHING,
    });
    new cloudwatch.Alarm(this, "Api5xx", {
      alarmDescription: "The API is returning 5xx responses",
      metric: targetGroup.metrics.httpCodeTarget(elbv2.HttpCodeTarget.TARGET_5XX_COUNT, {
        period: cdk.Duration.minutes(5),
      }),
      threshold: 5,
      evaluationPeriods: 1,
      treatMissingData: cloudwatch.TreatMissingData.NOT_BREACHING,
    });

    // Used by the deploy and smoke-test scripts. None of these are secrets.
    new cdk.CfnOutput(this, "SiteUrl", { value: origin });
    new cdk.CfnOutput(this, "ApiUrl", { value: `https://${config.apiDomain}/api` });
    new cdk.CfnOutput(this, "ClusterName", { value: cluster.clusterName });
    new cdk.CfnOutput(this, "ServiceName", { value: service.serviceName });
    new cdk.CfnOutput(this, "MigrateTaskDefinitionArn", { value: migrateTask.taskDefinitionArn });
    new cdk.CfnOutput(this, "TaskSubnetIds", { value: vpc.publicSubnets.map((s) => s.subnetId).join(",") });
    new cdk.CfnOutput(this, "TaskSecurityGroupId", { value: taskSecurityGroup.securityGroupId });
    new cdk.CfnOutput(this, "DistributionId", { value: distribution.distributionId });
  }
}
