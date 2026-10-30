import * as cdk from "aws-cdk-lib";
import * as iam from "aws-cdk-lib/aws-iam";
import { Construct } from "constructs";
import { DemoConfig } from "./config";

export interface DeployRoleStackProps extends cdk.StackProps {
  config: DemoConfig;
  /** owner/repo whose workflow may assume the role. */
  githubRepository: string;
  /** Only workflow runs for this branch may assume the role. */
  branch: string;
}

/**
 * The role GitHub Actions assumes (through the account's existing GitHub OIDC provider, so no long-lived keys) to
 * run scripts/deploy.sh. Infrastructure changes go through the CDK bootstrap roles; everything else the script
 * does is scoped to the demo's resources by name.
 */
export class DeployRoleStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props: DeployRoleStackProps) {
    super(scope, id, props);
    const { account, region } = props.config;

    const provider = iam.OpenIdConnectProvider.fromOpenIdConnectProviderArn(
      this,
      "GitHubProvider",
      `arn:aws:iam::${account}:oidc-provider/token.actions.githubusercontent.com`,
    );

    const role = new iam.Role(this, "GitHubDeployRole", {
      roleName: "riva-demo-github-deploy",
      description: `Deploys the Room Booking demo from ${props.githubRepository}@${props.branch}`,
      maxSessionDuration: cdk.Duration.hours(1),
      assumedBy: new iam.OpenIdConnectPrincipal(provider, {
        StringEquals: {
          "token.actions.githubusercontent.com:aud": "sts.amazonaws.com",
          "token.actions.githubusercontent.com:sub": `repo:${props.githubRepository}:ref:refs/heads/${props.branch}`,
        },
      }),
    });

    // cdk deploy: the bootstrap roles (both regions) do the CloudFormation and asset work.
    role.addToPolicy(
      new iam.PolicyStatement({
        actions: ["sts:AssumeRole"],
        resources: ["deploy", "file-publishing", "image-publishing", "lookup"].flatMap((kind) =>
          [region, "us-east-1"].map((r) => `arn:aws:iam::${account}:role/cdk-hnb659fds-${kind}-role-${account}-${r}`),
        ),
      }),
    );

    // Reading stack outputs.
    role.addToPolicy(
      new iam.PolicyStatement({
        actions: ["cloudformation:DescribeStacks"],
        resources: [`arn:aws:cloudformation:${region}:${account}:stack/RivaDemo*/*`],
      }),
    );

    // Pushing the API image.
    role.addToPolicy(new iam.PolicyStatement({ actions: ["ecr:GetAuthorizationToken"], resources: ["*"] }));
    role.addToPolicy(
      new iam.PolicyStatement({
        actions: [
          "ecr:BatchCheckLayerAvailability",
          "ecr:BatchGetImage",
          "ecr:CompleteLayerUpload",
          "ecr:DescribeImages",
          "ecr:InitiateLayerUpload",
          "ecr:PutImage",
          "ecr:UploadLayerPart",
        ],
        resources: [`arn:aws:ecr:${region}:${account}:repository/riva-room-booking-api`],
      }),
    );

    // Running the one-off migration task.
    const cluster = `arn:aws:ecs:${region}:${account}:cluster/RivaDemo-*`;
    role.addToPolicy(
      new iam.PolicyStatement({
        actions: ["ecs:RunTask"],
        resources: [`arn:aws:ecs:${region}:${account}:task-definition/RivaDemoMigrateTask*:*`],
        conditions: { ArnLike: { "ecs:cluster": cluster } },
      }),
    );
    role.addToPolicy(
      new iam.PolicyStatement({
        actions: ["ecs:DescribeTasks", "ecs:StopTask"],
        resources: [`arn:aws:ecs:${region}:${account}:task/RivaDemo-*/*`],
        conditions: { ArnLike: { "ecs:cluster": cluster } },
      }),
    );
    role.addToPolicy(
      new iam.PolicyStatement({
        actions: ["iam:PassRole"],
        resources: [
          `arn:aws:iam::${account}:role/RivaDemo-MigrateTaskTaskRole*`,
          `arn:aws:iam::${account}:role/RivaDemo-MigrateTaskExecutionRole*`,
        ],
        conditions: { StringEquals: { "iam:PassedToService": "ecs-tasks.amazonaws.com" } },
      }),
    );

    new cdk.CfnOutput(this, "RoleArn", { value: role.roleArn });
  }
}
