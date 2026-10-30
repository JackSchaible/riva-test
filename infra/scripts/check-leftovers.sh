#!/usr/bin/env bash
# Lists anything from the demo that still exists. Empty output under every heading means nothing is left.
source "$(dirname "$0")/lib.sh"

for region in "$REGION" us-east-1; do
  echo "--- $region: stacks"
  aws cloudformation list-stacks --region "$region" \
    --query "StackSummaries[?starts_with(StackName,'RivaDemo') && StackStatus!='DELETE_COMPLETE'].[StackName,StackStatus]" --output text
  echo "--- $region: tagged resources"
  aws resourcegroupstaggingapi get-resources --region "$region" --tag-filters Key=project,Values=riva-room-booking-demo \
    --query 'ResourceTagMappingList[].ResourceARN' --output text
  echo "--- $region: log groups"
  aws logs describe-log-groups --region "$region" --query "logGroups[?contains(logGroupName,'RivaDemo')].logGroupName" --output text
done
echo "--- RDS snapshots and retained backups"
aws rds describe-db-snapshots --region "$REGION" --query "DBSnapshots[?contains(DBInstanceIdentifier,'rivademo')].DBSnapshotIdentifier" --output text
aws rds describe-db-instance-automated-backups --region "$REGION" --query "DBInstanceAutomatedBackups[?contains(DBInstanceIdentifier,'rivademo')].DBInstanceIdentifier" --output text
echo "--- ECR repository"
aws ecr describe-repositories --region "$REGION" --repository-names riva-room-booking-api --query 'repositories[].repositoryName' --output text 2>/dev/null || true
echo "--- Unattached Elastic IPs"
aws ec2 describe-addresses --region "$REGION" --query 'Addresses[?AssociationId==null].PublicIp' --output text
echo "--- DNS records"
aws route53 list-resource-record-sets --hosted-zone-id Z1MRUAGRU2DG0A \
  --query "ResourceRecordSets[?Name=='riva-test.jackschaible.ca.' || Name=='riva-api.jackschaible.ca.' || starts_with(Name,'_')].[Name,Type]" --output text \
  | grep -E 'riva-(test|api)' || true
