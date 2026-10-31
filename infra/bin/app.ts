import * as path from "node:path";
import * as cdk from "aws-cdk-lib";
import { CertificateStack } from "../lib/certificate-stack";
import { demoConfig } from "../lib/config";
import { DemoStack } from "../lib/demo-stack";
import { DeployRoleStack } from "../lib/deploy-role-stack";
import { ImagesStack } from "../lib/images-stack";

const app = new cdk.App();

// The account comes from the AWS credentials running cdk (CDK sets CDK_DEFAULT_ACCOUNT from them), so it isn't
// in the repository. A synth without credentials, like CI's infra job, sets a placeholder explicitly.
const account = process.env.CDK_DEFAULT_ACCOUNT;
if (!account) {
  throw new Error("No AWS account: run cdk with AWS credentials, or set CDK_DEFAULT_ACCOUNT to synth without them.");
}
const env = { account, region: demoConfig.region };

cdk.Tags.of(app).add("project", "riva-room-booking-demo");
cdk.Tags.of(app).add("teardown-by", demoConfig.teardownBy);

const certificate = new CertificateStack(app, "RivaDemoCertificate", {
  env: { account, region: "us-east-1" },
  config: demoConfig,
});
const images = new ImagesStack(app, "RivaDemoImages", { env });
new DeployRoleStack(app, "RivaDemoDeployRole", {
  env,
  config: demoConfig,
  githubRepository: "JackSchaible/riva-test",
  branch: "main",
});

// The main stack needs an image to run, so it is only defined when one is given (scripts/deploy.sh passes it).
const imageTag = app.node.tryGetContext("imageTag") as string | undefined;
if (imageTag) {
  new DemoStack(app, "RivaDemo", {
    env,
    config: demoConfig,
    siteCertificate: certificate.certificate,
    repository: images.repository,
    imageTag,
    // Teardown passes a placeholder, since destroying doesn't need a real frontend build.
    siteAssetsPath: (app.node.tryGetContext("siteAssets") as string | undefined) ?? path.resolve(__dirname, "../../client/dist"),
  });
}
