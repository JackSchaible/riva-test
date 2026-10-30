#!/usr/bin/env bash
# Runs the one-off migration task (migrations + app role) and fails unless it exits 0.
source "$(dirname "$0")/lib.sh"

CLUSTER="$(stack_output RivaDemo ClusterName)"
TASK_ARN="$(aws ecs run-task --region "$REGION" --cluster "$CLUSTER" --launch-type FARGATE \
  --task-definition "$(stack_output RivaDemo MigrateTaskDefinitionArn)" \
  --network-configuration "awsvpcConfiguration={subnets=[$(stack_output RivaDemo TaskSubnetIds)],securityGroups=[$(stack_output RivaDemo TaskSecurityGroupId)],assignPublicIp=ENABLED}" \
  --query 'tasks[0].taskArn' --output text)"
echo "Started $TASK_ARN"
# Usually done in about a minute; give up (and stop the task) after 10.
for _ in $(seq 60); do
  STATUS="$(aws ecs describe-tasks --region "$REGION" --cluster "$CLUSTER" --tasks "$TASK_ARN" \
    --query 'tasks[0].lastStatus' --output text)"
  [[ "$STATUS" == "STOPPED" ]] && break
  sleep 10
done
if [[ "$STATUS" != "STOPPED" ]]; then
  aws ecs stop-task --region "$REGION" --cluster "$CLUSTER" --task "$TASK_ARN" --reason "Timed out" >/dev/null
  echo "Migration task did not finish in 10 minutes and was stopped." >&2
  exit 1
fi

EXIT_CODE="$(aws ecs describe-tasks --region "$REGION" --cluster "$CLUSTER" --tasks "$TASK_ARN" \
  --query 'tasks[0].containers[0].exitCode' --output text)"
if [[ "$EXIT_CODE" != "0" ]]; then
  echo "Migration task failed (exit code $EXIT_CODE). Logs: CloudWatch log group of the RivaDemo stack, prefix 'migrate'." >&2
  exit 1
fi
echo "Migrations applied"
