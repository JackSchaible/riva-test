#!/usr/bin/env bash
# Checks the deployed demo end to end: the site, the API through API Gateway, CORS, and a booking round trip.
source "$(dirname "$0")/lib.sh"

SITE="https://riva-test.jackschaible.ca"
API="https://riva-api.jackschaible.ca/api"
fail() { echo "FAIL: $*" >&2; exit 1; }

curl -sf "$SITE/" | grep -q "<div id=\"app\"" || fail "site does not serve the app"
echo "ok   site serves index.html"

curl -sf "$API/rooms" | grep -q '"Room A"' || fail "GET /api/rooms"
echo "ok   GET /api/rooms"

PREFLIGHT="$(curl -s -o /dev/null -D - -X OPTIONS "$API/bookings" -H "Origin: $SITE" \
  -H "Access-Control-Request-Method: POST" -H "Access-Control-Request-Headers: content-type")"
grep -qi "access-control-allow-origin: $SITE" <<<"$PREFLIGHT" || fail "CORS preflight"
echo "ok   CORS preflight allows $SITE"

OTHER="$(curl -s -o /dev/null -D - "$API/rooms" -H "Origin: https://example.com")"
grep -qi "access-control-allow-origin" <<<"$OTHER" && fail "CORS allows another origin"
echo "ok   CORS rejects other origins"

# A slot far in the future, so it never blocks a real demo booking.
DAY="2099-$(date -u +%m-%d)"
BODY="{\"roomId\":1,\"bookedBy\":\"Smoke test\",\"startUtc\":\"${DAY}T03:00:00Z\",\"endUtc\":\"${DAY}T03:30:00Z\"}"
CREATED="$(curl -s -w '\n%{http_code}' -X POST "$API/bookings" -H "Origin: $SITE" -H "Content-Type: application/json" -d "$BODY")"
[[ "$(tail -1 <<<"$CREATED")" == "201" ]] || fail "POST /api/bookings: $CREATED"
ID="$(head -1 <<<"$CREATED" | sed -E 's/.*"id":([0-9]+).*/\1/')"
echo "ok   POST /api/bookings (id $ID)"

[[ "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/bookings" -H "Content-Type: application/json" -d "$BODY")" == "409" ]] \
  || fail "overlapping booking was not rejected"
echo "ok   overlapping booking returns 409"

[[ "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/bookings/$ID/cancel")" == "200" ]] || fail "cancel"
echo "ok   cancel"

[[ "$(curl -s -o /dev/null -w '%{http_code}' "https://riva-api.jackschaible.ca/health")" != "200" ]] || fail "/health is public"
echo "ok   /health is not reachable through API Gateway"

echo "All smoke tests passed"
