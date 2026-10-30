import * as cdk from "aws-cdk-lib";
import * as ecr from "aws-cdk-lib/aws-ecr";
import { Construct } from "constructs";

/**
 * The API image repository. It is separate so an image (tagged with the commit SHA) can be pushed before the
 * main stack starts a service that uses it. Destroying the stack deletes the repository and its images.
 */
export class ImagesStack extends cdk.Stack {
  readonly repository: ecr.IRepository;

  constructor(scope: Construct, id: string, props: cdk.StackProps) {
    super(scope, id, props);

    const repository = new ecr.Repository(this, "ApiRepository", {
      repositoryName: "riva-room-booking-api",
      imageTagMutability: ecr.TagMutability.IMMUTABLE,
      removalPolicy: cdk.RemovalPolicy.DESTROY,
      emptyOnDelete: true,
      lifecycleRules: [{ maxImageCount: 10 }],
    });
    this.repository = repository;

    new cdk.CfnOutput(this, "RepositoryUri", { value: repository.repositoryUri });
  }
}
