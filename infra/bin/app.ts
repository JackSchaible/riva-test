import * as path from "node:path";
import * as cdk from "aws-cdk-lib";
import { CertificateStack } from "../lib/certificate-stack";
import { demoConfig } from "../lib/config";
import { DemoStack } from "../lib/demo-stack";
import { DeployRoleStack } from "../lib/deploy-role-stack";
import { ImagesStack } from "../lib/images-stack";

const app = new cdk.App();
const env = { account: demoConfig.account, region: demoConfig.region };

cdk.Tags.of(app).add("project", "riva-room-booking-demo");
cdk.Tags.of(app).add("teardown-by", demoConfig.teardownBy);

const certificate = new CertificateStack(app, "RivaDemoCertificate", {
  env: { account: demoConfig.account, region: "us-east-1" },
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
