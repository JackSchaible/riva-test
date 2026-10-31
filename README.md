# Room Booking

A small app for booking meeting rooms without double bookings. The API uses ASP.NET Core 9, Dapper, and PostgreSQL; the frontend uses Vue 3 and Vuetify.

- Three seeded rooms (Room A, B, C), with each room's bookings shown for a selected UTC day.
- Book a room with a name, start time, and end time. Cancel a booking to free its slot.
- Overlapping bookings for the same room are rejected by the database, including when two requests arrive at the same moment.

**Live demo** (temporary, until 2026-11-06): <https://riva-test.jackschaible.ca> · API: <https://riva-api.jackschaible.ca/api>

## Highlights

- **Correctness is enforced by the database and proven under concurrency.** Double bookings are blocked by a PostgreSQL exclusion constraint rather than a "check, then insert" in application code, which would race. Concurrency tests release overlapping requests simultaneously over many rounds and confirm exactly one booking wins every time.
- **Least-privilege by design.** The public API runs as a database role that can only read rooms and create or cancel bookings; only a one-off migration task holds the admin credential. The load balancer is internal and reachable only through an API Gateway VPC link. CI deploys with short-lived GitHub OIDC credentials, never stored keys.
- **The demo is run like a real service.** Infrastructure is defined in AWS CDK and tested in CI. Deploy, smoke test, teardown, and a leftover-resource check are all scripted, and CI deploys only after every test job passes.

## Run locally

Prerequisites: [.NET SDK 9](https://dotnet.microsoft.com/download), [Node.js 20+](https://nodejs.org/) with [pnpm 10](https://pnpm.io/installation), and [Docker](https://www.docker.com/get-started) (for PostgreSQL and the backend tests). No cloud account is needed.

The quickest way is `./dev.sh` from the repo root. It starts PostgreSQL, waits for the API, then starts the frontend. Ctrl+C stops the API and frontend; `./dev.sh --stop-db` also stops PostgreSQL.

To run each piece yourself:

```bash
# 1. PostgreSQL on localhost:5432 (postgres/postgres, database room_booking)
docker compose up -d --wait

# 2. API on http://localhost:5059 (creates the database, runs migrations, seeds data)
cd server/RoomBooking && dotnet run

# 3. In another terminal, the frontend on http://localhost:5173
cd client && pnpm install && pnpm dev
```

The Vite dev server proxies `/api/...` to the API (override with `API_PROXY_TARGET`). `docker compose down -v` resets the database.

Migrations seed the three rooms. In Development, the API also seeds a few bookings for today (UTC), including adjacent 09:00-10:00 and 10:00-11:00 bookings in Room A, so the rules are visible straight away.

## Tests

```bash
dotnet test server/RoomBooking.sln         # backend integration tests (needs Docker)

cd client
pnpm test && pnpm lint && pnpm build       # frontend tests, lint, production build
pnpm exec playwright install chromium      # first time only
pnpm test:e2e                              # end-to-end (needs PostgreSQL running)
```

- **Backend (xUnit + Testcontainers):** the real API runs in memory against a real PostgreSQL container. Tests cover every endpoint, the overlap, adjacency, and rebooking rules, input validation and its boundaries, and concurrency: overlapping requests are held at a `Barrier` and released together, expecting exactly one 201, one 409, and one saved row, across 25 rounds. Fault injection covers deadlock retries and the 500 error envelope.
- **Frontend (Vitest + Vue Test Utils):** the page, the booking form (including the 409 conflict message), the API client, and UTC helpers.
- **End-to-end (Playwright):** Chromium against the full stack with an isolated database, covering booking, cancelling, overlap and adjacency, validation, and two browser sessions booking overlapping slots at once.

[CI](.github/workflows/ci.yml) runs all of the above plus the infrastructure tests on every push and pull request, and deploys the demo on pushes to `main`.

## API

All responses use the same envelope:

```json
{ "success": true, "data": { }, "message": "…", "errors": null }
```

Times are ISO 8601 and **must be UTC** (`Z` or `+00:00`).

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/rooms` | List rooms |
| `GET` | `/api/bookings?date=YYYY-MM-DD` | Active bookings that start on that UTC day |
| `GET` | `/api/bookings/{id}` | One booking (including cancelled), or 404 |
| `POST` | `/api/bookings` | Create: 201, 400 (validation), or 409 (overlap) |
| `POST` | `/api/bookings/{id}/cancel` | Cancel: 200 (idempotent) or 404 |

```bash
curl -X POST http://localhost:5059/api/bookings \
  -H "Content-Type: application/json" \
  -d '{"roomId":1,"bookedBy":"Alice","startUtc":"2030-01-15T10:00:00Z","endUtc":"2030-01-15T11:00:00Z"}'
# 201 Created, Location: /api/bookings/{id}

curl -X POST http://localhost:5059/api/bookings \
  -H "Content-Type: application/json" \
  -d '{"roomId":1,"bookedBy":"Bob","startUtc":"2030-01-15T10:30:00Z","endUtc":"2030-01-15T11:30:00Z"}'
# 409 {"success":false,"message":"Room A is already booked 10:00-11:00 UTC",...}
```

A booking needs an existing `roomId`, a non-blank `bookedBy` of at most 100 characters, and UTC `startUtc`/`endUtc` with the end after the start and no later than 00:00 UTC the next day. Timestamps without an offset are rejected rather than read in the server's time zone. Violations return 400 with a list of `errors`.

## Concurrency approach

The database is the single source of truth. [Migration 001](server/RoomBooking/Database/Migrations/001_CreateRoomsAndBookings.sql) adds an **exclusion constraint**:

```sql
EXCLUDE USING gist (
    room_id WITH =,
    tstzrange(start_utc, end_utc, '[)') WITH &&
) WHERE (cancelled_at IS NULL)
```

- `[)` makes ranges half-open, so adjacent bookings (10:00-11:00 and 11:00-12:00) are allowed.
- The partial `WHERE` means only active bookings count, so cancelling frees the slot.
- `btree_gist` lets one GiST index combine equality on `room_id` with range overlap.

The API does not check for overlaps before inserting, since a "SELECT then INSERT" check races. It simply inserts, and PostgreSQL enforces the constraint atomically, even across concurrent uncommitted transactions. A violation (SQLSTATE `23P01`) becomes **409 Conflict** with a message naming the conflicting booking.

Two simultaneous overlapping inserts can also wait on each other's uncommitted index entries, which PostgreSQL resolves by aborting one with a deadlock (`40P01`). The API retries on deadlock (up to 3 attempts); by then the winner has committed, so the retry returns a clean 409.

Cancellation is a soft delete: `cancelled_at` is set with `COALESCE`, so repeat cancels are idempotent and keep the original time.

## Tradeoffs

- **PostgreSQL-specific.** Exclusion constraints aren't portable; other databases would need serializable transactions, application locks, or a slot table. In exchange, correctness doesn't depend on application code or isolation-level tuning.
- **Soft delete.** Keeps history and makes cancellation idempotent, at the cost of a growing table and a `cancelled_at IS NULL` filter on active queries.
- **UTC single-day bookings.** Simple and unambiguous, though a UTC "day" isn't the local day for users far from UTC. The UI labels all times as UTC.
- **No optimistic UI.** The list updates after the API responds: always truthful, slightly slower to feel.
- **Validation in two layers.** The API validates input, and the database enforces overlap, room existence, and `end > start`, so a request that bypasses validation still can't corrupt data.

## What I'd improve next

- Room time zones, with local-time display alongside UTC
- Week view and range queries for bookings
- Authentication, so only the booker (or an admin) can cancel
- Rate limiting
- Idempotency keys on `POST /api/bookings`
- Archiving old cancelled bookings

## AWS deployment

```text
Browser ─┬─> CloudFront ─> S3 (private, Origin Access Control)          frontend
         └─> API Gateway REST API ─> VPC link ─> internal ALB ─> Fargate ─> RDS PostgreSQL
```

- **Infrastructure:** an AWS CDK app (TypeScript) in [infra/](infra), deployed to `ca-central-1`.
- **Network:** the ALB is internal, reachable only through the API Gateway VPC link. API tasks accept traffic only from the ALB, and RDS sits in isolated subnets accepting traffic only from the tasks. No NAT Gateway is needed.
- **Database access:** migrations run as a one-off ECS task with the RDS master credential, then create a `room_booking_app` role limited to reading `rooms` and reading, inserting, and updating `bookings`. The API runs only as that role. An advisory lock prevents concurrent migration runs.
- **Reliability:** health checks, a deployment circuit breaker that rolls back failed deploys, and alarms for no healthy task and API 5xx errors.
- **CI deploy:** after all test jobs pass on `main`, CI assumes a deploy role via GitHub OIDC (restricted to this repository's `main` branch), then runs the deploy and smoke-test scripts.
- **Cost:** about $1.70 per day at demo traffic.

This is a temporary demo, not a production service: there is no authentication or backup, and it will be torn down after review.

Scripts (run from the repo root, signed in to the hosting AWS account):

```bash
./infra/scripts/deploy.sh             # deploy or update the stacks, images, frontend, and migrations
./infra/scripts/smoke.sh              # site, API, CORS, booking, overlap 409, cancel
./infra/scripts/migrate.sh            # re-run migrations only
./infra/scripts/teardown.sh           # destroy all stacks
./infra/scripts/check-leftovers.sh    # confirm nothing billable remains
```

## Project structure

```text
client/                           Vue 3 + Vuetify frontend (Vite)
  src/components/                 RoomBookings (page), BookingForm, tests
  src/services/booking.service.ts Typed API client
  e2e/                            Playwright end-to-end tests
server/
  RoomBooking/                    ASP.NET Core API
    Controllers/                  RoomsController, BookingsController
    Database/                     DbUp migrator, SQL migrations, Development seeder
    Services/                     Dapper data access
  RoomBooking.IntegrationTests/   xUnit + Testcontainers integration tests
  Dockerfile                      API image
infra/                            AWS CDK app and deploy scripts
docker-compose.yml                Local PostgreSQL
```

## AI and library disclosure

- **AI:** Built with help from Claude Code (Anthropic's AI coding assistant), which helped implement the code, tests, and documentation from a plan I wrote. I reviewed the result.
- **Backend:** ASP.NET Core, [Dapper](https://github.com/DapperLib/Dapper), [Npgsql](https://www.npgsql.org/), [DbUp](https://dbup.readthedocs.io/), [xUnit](https://xunit.net/), [Testcontainers for .NET](https://dotnet.testcontainers.org/), `Microsoft.AspNetCore.Mvc.Testing`.
- **Frontend:** [Vue 3](https://vuejs.org/), [Vuetify 3](https://vuetifyjs.com/), [Vite](https://vite.dev/), [Vitest](https://vitest.dev/), [Vue Test Utils](https://test-utils.vuejs.org/), [Playwright](https://playwright.dev/), [node-postgres](https://node-postgres.com/), ESLint with typescript-eslint, Material Design Icons.
