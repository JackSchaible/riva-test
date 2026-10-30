#!/usr/bin/env bash
# Deploys the demo from the current commit: certificate and image repository, the API image, the stack, then the
# one-off migration task. Safe to re-run.
source "$(dirname "$0")/lib.sh"

if [[ -n "$(git -C "$REPO_DIR" status --porcelain -- server client)" ]]; then
  echo "server/ or client/ has uncommitted changes; commit them so the image tag matches the code." >&2
  exit 1
fi
TAG="$(git -C "$REPO_DIR" rev-parse HEAD)"
API_BASE_URL="https://riva-api.jackschaible.ca/api"

echo "==> Certificate and image repository"
cdk deploy RivaDemoCertificate RivaDemoImages --require-approval never

echo "==> API image $TAG"
REPOSITORY="$(stack_output RivaDemoImages RepositoryUri)"
PUSHED_SIZE="$(aws ecr describe-images --region "$REGION" --repository-name "${REPOSITORY#*/}" --image-ids imageTag="$TAG" \
  --query 'imageDetails[0].imageSizeInBytes' --output text 2>/dev/null || echo 0)"
if (( PUSHED_SIZE > 10000000 )); then
  echo "Already pushed"
else
  aws ecr get-login-password --region "$REGION" | docker login --username AWS --password-stdin "${REPOSITORY%%/*}"
  # No attestations: they make the push an image index, which ECR rejects under an immutable tag (leaving the tag
  # on the small attestation manifest instead of the image).
  docker build --pull --platform linux/arm64 --provenance=false --sbom=false -t "$REPOSITORY:$TAG" "$REPO_DIR/server"
  docker push "$REPOSITORY:$TAG"
fi

echo "==> Frontend build (API: $API_BASE_URL)"
(cd "$REPO_DIR/client" && pnpm install --frozen-lockfile && VITE_API_BASE_URL="$API_BASE_URL" pnpm build)

echo "==> Stack"
cdk deploy RivaDemo -c imageTag="$TAG" --require-approval never

echo "==> Migrations"
"$INFRA_DIR/scripts/migrate.sh"

echo "==> Done: $(stack_output RivaDemo SiteUrl)"
