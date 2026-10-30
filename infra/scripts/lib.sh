# Shared helpers for the demo scripts. Uses the caller's AWS credentials (AWS_PROFILE etc.).
set -euo pipefail

INFRA_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_DIR="$(cd "$INFRA_DIR/.." && pwd)"
REGION=ca-central-1

# Prints one CloudFormation output of a stack.
stack_output() {
  aws cloudformation describe-stacks --region "$REGION" --stack-name "$1" \
    --query "Stacks[0].Outputs[?OutputKey=='$2'].OutputValue" --output text
}

cdk() { (cd "$INFRA_DIR" && pnpm exec cdk "$@"); }
