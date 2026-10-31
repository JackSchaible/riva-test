import * as path from "node:path";
import * as cdk from "aws-cdk-lib";
import { Match, Template } from "aws-cdk-lib/assertions";
import { beforeAll, describe, expect, it } from "vitest";
import { CertificateStack } from "../lib/certificate-stack";
import { demoConfig } from "../lib/config";
import { DemoStack } from "../lib/demo-stack";
import { DeployRoleStack } from "../lib/deploy-role-stack";
import { ImagesStack } from "../lib/images-stack";

// Any account works for templates; the real one only comes from AWS credentials.
const account = "123456789012";

let template: Template;

beforeAll(() => {
  const app = new cdk.App();
  const env = { account, region: demoConfig.region };
  const certificate = new CertificateStack(app, "Certificate", {
    env: { account, region: "us-east-1" },
    config: demoConfig,
  });
  const images = new ImagesStack(app, "Images", { env });
  const stack = new DemoStack(app, "Demo", {
    env,
    config: demoConfig,
    siteCertificate: certificate.certificate,
    repository: images.repository,
    imageTag: "abc123",
    siteAssetsPath: path.join(__dirname, "fixtures/site"),
  });
  template = Template.fromStack(stack);
});

const environmentOf = (containerName: string): Record<string, unknown> => {
  const definitions = template.findResources("AWS::ECS::TaskDefinition");
  for (const definition of Object.values(definitions)) {
    for (const container of definition.Properties.ContainerDefinitions) {
      if (container.Name === containerName) {
        return Object.fromEntries(
          [...(container.Environment ?? []), ...(container.Secrets ?? [])].map(
            (e: { Name: string; Value?: unknown; ValueFrom?: unknown }) => [e.Name, e.Value ?? e.ValueFrom],
          ),
        );
      }
    }
  }
  throw new Error(`No container ${containerName}`);
};

describe("network", () => {
  it("has no NAT gateway", () => {
    template.resourceCountIs("AWS::EC2::NatGateway", 0);
  });

  it("keeps the ALB internal and the database private", () => {
    template.hasResourceProperties("AWS::ElasticLoadBalancingV2::LoadBalancer", { Scheme: "internal" });
    template.hasResourceProperties("AWS::RDS::DBInstance", { PubliclyAccessible: false });
  });

  it("never opens a security group to the internet", () => {
    for (const group of Object.values(template.findResources("AWS::EC2::SecurityGroup"))) {
      for (const rule of group.Properties.SecurityGroupIngress ?? []) {
        expect(rule.CidrIp).toBeUndefined();
      }
    }
    for (const rule of Object.values(template.findResources("AWS::EC2::SecurityGroupIngress"))) {
      expect(rule.Properties.CidrIp).toBeUndefined();
    }
  });
});

describe("database", () => {
  it("is a small single-AZ instance that is destroyed with no backups or snapshot", () => {
    template.hasResourceProperties("AWS::RDS::DBInstance", {
      DBInstanceClass: "db.t4g.micro",
      MultiAZ: false,
      BackupRetentionPeriod: 0,
      DeletionProtection: false,
      DeleteAutomatedBackups: true,
    });
    template.hasResource("AWS::RDS::DBInstance", { DeletionPolicy: "Delete", UpdateReplacePolicy: "Delete" });
  });
});

describe("tasks", () => {
  it("runs the API without migrations, as the app role, with CORS for the site only", () => {
    const env = environmentOf("api");
    expect(env.Database__Migrations).toBe("Skip");
    expect(env.Cors__AllowedOrigins__0).toBe("https://riva-test.jackschaible.ca");
    expect(JSON.stringify(env.ConnectionStrings__DefaultConnection)).toContain("Username=room_booking_app");
    expect(env.PGPASSWORD).toBeDefined();
  });

  it("only gives the master credential to the migration task", () => {
    // The generated master secret and its attachment both have logical IDs starting with this.
    const masterSecret = "DatabaseSecret";
    expect(JSON.stringify(environmentOf("api"))).not.toContain(masterSecret);

    const migrate = environmentOf("migrate");
    expect(migrate.Database__Migrations).toBe("Only");
    expect(JSON.stringify(migrate.PGPASSWORD)).toContain(masterSecret);
    expect(migrate.Database__AppRole__Name).toBe("room_booking_app");
  });

  it("runs an init process, so a crash exits instead of hanging", () => {
    const definitions = Object.values(template.findResources("AWS::ECS::TaskDefinition"));
    expect(definitions).toHaveLength(2);
    for (const definition of definitions) {
      expect(definition.Properties.ContainerDefinitions[0].LinuxParameters.InitProcessEnabled).toBe(true);
    }
  });

  it("rolls back a failed deployment", () => {
    template.hasResourceProperties("AWS::ECS::Service", {
      DeploymentConfiguration: { DeploymentCircuitBreaker: { Enable: true, Rollback: true } },
    });
    template.hasResourceProperties("AWS::ElasticLoadBalancingV2::TargetGroup", { HealthCheckPath: "/health" });
  });
});

describe("API Gateway", () => {
  it("proxies every method under /api to the ALB through the VPC link", () => {
    template.hasResourceProperties("AWS::ApiGateway::Method", {
      HttpMethod: "ANY",
      Integration: Match.objectLike({
        Type: "HTTP_PROXY",
        IntegrationHttpMethod: "ANY",
        ConnectionType: "VPC_LINK",
        ConnectionId: Match.anyValue(),
        IntegrationTarget: Match.anyValue(),
      }),
    });
  });

  it("does not cache, and only serves through the custom domain", () => {
    template.hasResourceProperties("AWS::ApiGateway::Stage", {
      MethodSettings: Match.arrayWith([Match.objectLike({ CachingEnabled: false })]),
    });
    template.hasResourceProperties("AWS::ApiGateway::RestApi", { DisableExecuteApiEndpoint: true });
  });

  it("adds CORS headers for the site, never a wildcard, to its own error responses", () => {
    const responses = Object.values(template.findResources("AWS::ApiGateway::GatewayResponse"));
    expect(responses).toHaveLength(2);
    for (const response of responses) {
      expect(response.Properties.ResponseParameters["gatewayresponse.header.Access-Control-Allow-Origin"]).toBe(
        "'https://riva-test.jackschaible.ca'",
      );
    }
  });
});

describe("frontend", () => {
  it("serves a private bucket through CloudFront with OAC", () => {
    template.hasResourceProperties("AWS::S3::Bucket", {
      PublicAccessBlockConfiguration: {
        BlockPublicAcls: true,
        BlockPublicPolicy: true,
        IgnorePublicAcls: true,
        RestrictPublicBuckets: true,
      },
    });
    template.resourceCountIs("AWS::CloudFront::OriginAccessControl", 1);
    template.hasResourceProperties("AWS::CloudFront::Distribution", {
      DistributionConfig: Match.objectLike({ Aliases: ["riva-test.jackschaible.ca"] }),
    });
  });
});

describe("GitHub deploy role", () => {
  it("can only be assumed by this repo's main branch through OIDC", () => {
    const app = new cdk.App();
    const stack = new DeployRoleStack(app, "DeployRole", {
      env: { account, region: demoConfig.region },
      config: demoConfig,
      githubRepository: "JackSchaible/riva-test",
      branch: "main",
    });
    Template.fromStack(stack).hasResourceProperties("AWS::IAM::Role", {
      AssumeRolePolicyDocument: {
        Statement: [
          Match.objectLike({
            Action: "sts:AssumeRoleWithWebIdentity",
            Condition: {
              StringEquals: {
                "token.actions.githubusercontent.com:aud": "sts.amazonaws.com",
                "token.actions.githubusercontent.com:sub": "repo:JackSchaible/riva-test:ref:refs/heads/main",
              },
            },
          }),
        ],
      },
    });
  });
});
