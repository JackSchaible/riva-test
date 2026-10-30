#!/usr/bin/env bash
# Runs the one-off migration task (migrations + app role) and fails unless it exits 0.
source "$(dirname "$0")/lib.sh"

CLUSTER="$(stack_output RivaDemo ClusterName)"
TASK_ARN="$(aws ecs run-task --region "$REGION" --cluster "$CLUSTER" --launch-type FARGATE \
  --task-definition "$(stack_output RivaDemo MigrateTaskDefinitionArn)" \
  --network-configuration "awsvpcConfiguration={subnets=[$(stack_output RivaDemo TaskSubnetIds)],securityGroups=[$(stack_output RivaDemo TaskSecurityGroupId)],assignPublicIp=ENABLED}" \
  --query 'tasks[0].taskArn' --output text)"
echo "Started $TASK_ARN"
aws ecs wait tasks-stopped --region "$REGION" --cluster "$CLUSTER" --tasks "$TASK_ARN"

EXIT_CODE="$(aws ecs describe-tasks --region "$REGION" --cluster "$CLUSTER" --tasks "$TASK_ARN" \
  --query 'tasks[0].containers[0].exitCode' --output text)"
if [[ "$EXIT_CODE" != "0" ]]; then
  echo "Migration task failed (exit code $EXIT_CODE). Logs: CloudWatch log group of the RivaDemo stack, prefix 'migrate'." >&2
  exit 1
fi
echo "Migrations applied"
