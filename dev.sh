#!/usr/bin/env bash
# Starts PostgreSQL, the API, and the frontend for local development.
# Ctrl+C stops the API and frontend; pass --stop-db to also stop PostgreSQL on exit.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_URL="http://localhost:5059"
STOP_DB=false
[[ "${1:-}" == "--stop-db" ]] && STOP_DB=true

API_PID=""
WEB_PID=""

cleanup() {
  trap - EXIT INT TERM
  echo
  echo "Shutting down..."
  # Background jobs in scripts ignore SIGINT, so send SIGTERM to each job's children (the app that
  # `dotnet run` launched, the vite node process under pnpm), then to the job itself.
  for pid in "$API_PID" "$WEB_PID"; do
    if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
      pkill -TERM -P "$pid" 2>/dev/null || true
      kill -TERM "$pid" 2>/dev/null || true
      wait "$pid" 2>/dev/null || true
    fi
  done
  if $STOP_DB; then
    docker compose -f "$ROOT/docker-compose.yml" stop
  fi
}
trap cleanup EXIT INT TERM

for cmd in docker dotnet pnpm curl; do
  command -v "$cmd" >/dev/null || { echo "Missing required command: $cmd" >&2; exit 1; }
done

if ! docker info >/dev/null 2>&1; then
  echo "Docker is not running. Start Docker Desktop and try again." >&2
  exit 1
fi

echo "==> Starting PostgreSQL"
docker compose -f "$ROOT/docker-compose.yml" up -d --wait

if [[ ! -d "$ROOT/client/node_modules" ]]; then
  echo "==> Installing frontend dependencies"
  (cd "$ROOT/client" && pnpm install)
fi

echo "==> Starting API on $API_URL"
(cd "$ROOT/server/RoomBooking" && exec dotnet run) > >(sed -u 's/^/[api] /') 2>&1 &
API_PID=$!

# The first run restores and builds, so allow a couple of minutes.
for _ in $(seq 1 120); do
  curl -sf "$API_URL/api/rooms" >/dev/null && break
  if ! kill -0 "$API_PID" 2>/dev/null; then
    echo "API exited before it was ready; see the [api] output above." >&2
    exit 1
  fi
  sleep 1
done
curl -sf "$API_URL/api/rooms" >/dev/null || { echo "API did not become ready in time." >&2; exit 1; }

echo "==> Starting frontend on http://localhost:5173"
(cd "$ROOT/client" && exec pnpm dev --port 5173 --strictPort) &
WEB_PID=$!

# `wait` (unlike a foreground command) returns as soon as a signal arrives, so cleanup runs promptly.
wait "$WEB_PID" || true
