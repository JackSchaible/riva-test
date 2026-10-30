#!/usr/bin/env bash
# Destroys the demo and everything it created, then lists anything billable that is left.
source "$(dirname "$0")/lib.sh"

echo "==> Destroying stacks (the main stack takes ~15-20 minutes, mostly CloudFront and RDS)"
cdk destroy RivaDemo RivaDemoImages RivaDemoCertificate RivaDemoDeployRole -c imageTag=teardown -c siteAssets="$INFRA_DIR/test/fixtures/site" --force

echo "==> Deleting log groups of CDK's helper Lambdas (CloudFormation doesn't own them)"
for region in "$REGION" us-east-1; do
  for group in $(aws logs describe-log-groups --region "$region" --log-group-name-prefix /aws/lambda/RivaDemo \
      --query 'logGroups[].logGroupName' --output text); do
    aws logs delete-log-group --region "$region" --log-group-name "$group" && echo "deleted $group ($region)"
  done
done

echo "==> Checking for leftovers"
"$INFRA_DIR/scripts/check-leftovers.sh"
