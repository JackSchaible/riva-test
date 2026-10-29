# Room Booking

A small app for booking meeting rooms without double bookings. It uses ASP.NET Core 9, Dapper, and PostgreSQL for the API, and Vue 3 with Vuetify for the frontend.

- Three seeded rooms (Room A, B, C), with each room's bookings shown for a selected UTC day.
- Book a room with a name, start time, and end time. Cancel a booking to free its slot.
- Overlapping bookings for the same room are rejected by the database, including when two requests arrive at the same moment.

## Prerequisites

- [.NET SDK 9](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and [pnpm 10](https://pnpm.io/installation)
- [Docker](https://www.docker.com/get-started), used to run PostgreSQL locally and by the backend tests (Testcontainers)

No cloud account or paid service is needed.

> Prefer a PostgreSQL install you already have? Any PostgreSQL 14+ with the standard `btree_gist` contrib extension works. Point `ConnectionStrings:DefaultConnection` in [server/RoomBooking/appsettings.Development.json](server/RoomBooking/appsettings.Development.json) at it. The connecting user needs permission to create the database (or have it already exist) and the extension.

## Run locally

The quickest way is to run `./dev.sh` from the repo root. It starts PostgreSQL, waits for the API, then starts the frontend. Press Ctrl+C to stop the API and frontend; `./dev.sh --stop-db` also stops PostgreSQL on exit.

To run each piece yourself:

```bash
# 1. Start PostgreSQL (localhost:5432, user/password postgres/postgres, database room_booking)
docker compose up -d --wait

# 2. Start the API on http://localhost:5059.
#    It creates the database if needed, runs migrations, and seeds sample data.
cd server/RoomBooking
dotnet run

# 3. In another terminal, start the frontend on http://localhost:5173
cd client
pnpm install
pnpm dev
```

Open <http://localhost:5173>. The frontend calls same-origin `/api/...`, and the Vite dev server proxies those calls to the API. Set `API_PROXY_TARGET` if the API runs somewhere other than `http://localhost:5059`.

To reset everything, run `docker compose down -v`, which deletes the database volume.

### Sample data

- **Migrations** ([server/RoomBooking/Database/Migrations](server/RoomBooking/Database/Migrations)) create the schema and seed **Room A**, **Room B**, and **Room C**. They are embedded SQL scripts run by DbUp at startup.
- **In the Development environment only**, if the `bookings` table is empty, the API also seeds a few bookings for *today (UTC)*. These include adjacent 09:00-10:00 and 10:00-11:00 bookings in Room A, so you can see the rules in action straight away.

## Tests

```bash
# Backend integration tests (needs Docker running; Testcontainers starts a throwaway PostgreSQL)
dotnet test server/RoomBooking.sln

# Frontend: interaction test, lint, and production build
cd client
pnpm test
pnpm lint
pnpm build

# End-to-end (Playwright): real browser, real API, real PostgreSQL.
# Needs PostgreSQL running (docker compose up -d --wait). Playwright starts the API and Vite itself.
pnpm exec playwright install chromium   # first time only
pnpm test:e2e
```

The backend tests (123) run the real API in memory (`WebApplicationFactory`) against a real PostgreSQL container. The container is shared across tests, and bookings are truncated before each test while the seeded rooms are kept. The tests cover:

- **Every endpoint:** list rooms, list bookings for a day (only that UTC day, cancelled bookings excluded), get by ID (plus 404), create (201, `Location` header, persisted row), and cancel (sets `cancelled_at`, 404 for unknown IDs, idempotent repeat).
- **Rules, checked against the database:** overlap (10:00-11:00 then 10:30-11:30 returns 409 and leaves one row), other overlap shapes, adjacency (both saved), same time in different rooms, and rebooking a cancelled slot.
- **Input validation**, each returning 400 with no row written: invalid times (end = start, end < start, crossing midnight, non-UTC offset, no offset at all), missing or over-long names, unknown rooms, and malformed or missing request bodies. Boundaries that are allowed are tested too: a booking ending at 00:00 the next day, a whole-day booking, and a 100-character name.
- **Concurrency:** two requests are held at a `Barrier`, released together, and awaited with `Task.WhenAll`. For overlapping requests, exactly one 201, one 409, and one saved row are expected. For adjacent requests, both must succeed. When cancelling a booking races a new overlapping booking, either may win; the test checks that the cancel succeeds, the booking gets a 201 or 409 (never a 500), and the database matches whichever happened. Each scenario runs as 25 independent rounds, with data reset between rounds.
- **Error paths that real concurrency only hits sometimes:** a test subclass of `BookingService` injects PostgreSQL errors into its insert attempts. This covers a deadlock followed by success, a deadlock followed by a real overlap (409 with the right message), deadlocks on every attempt (gives up after 3; the API returns a 500 envelope), and the fallback conflict message when the conflicting booking has disappeared.
- **Infrastructure:** unexpected exceptions return the 500 envelope without exception details, migrations can run again safely, and the Development seeder only seeds an empty table.

The frontend tests (26, Vitest and Vue Test Utils) stub `fetch` and cover:

- **The page ([RoomBookings.spec.ts](client/src/components/RoomBookings.spec.ts)):** bookings grouped by room with UTC times, changing the day, the 409 conflict message (the interaction test the assessment asks for), refreshing after a booking, cancelling a booking or failing to, and the load error with Retry.
- **The form ([BookingForm.spec.ts](client/src/components/BookingForm.spec.ts)):** the request it sends, 00:00 meaning end of day, showing 400 errors, clearing a conflict on retry, and network failures.
- **The API client and UTC helpers.**

The end-to-end tests (8, Playwright, in [client/e2e](client/e2e)) drive Chromium against the whole stack. Playwright starts its own API and Vite on ports 5159 and 5174, so they can run alongside `dev.sh`. The API uses a separate `room_booking_e2e` database on the same PostgreSQL, which is reset at the start of each run. Each test uses its own date, so the tests run in parallel. They cover listing rooms, booking and cancelling (checked again after a reload), an overlap rejected next to an allowed adjacent booking, the same time in different rooms, validation errors, booking until midnight, rebooking a cancelled slot, and two browser sessions booking overlapping slots at once, where exactly one sees the conflict.

CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs the backend tests, the client lint, tests, and build, and the end-to-end tests against a PostgreSQL service container. It does not deploy anything.

## API

All responses use the same envelope:

```json
{ "success": true, "data": { }, "message": "…", "errors": null }
```

Times are ISO 8601 and **must be UTC**: use the `Z` suffix or a `+00:00` offset.

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/rooms` | List rooms |
| `GET` | `/api/bookings?date=YYYY-MM-DD` | Active bookings for all rooms that start on that UTC day |
| `GET` | `/api/bookings/{id}` | One booking, including a cancelled one (`cancelledAt` set), or 404 |
| `POST` | `/api/bookings` | Create a booking: 201, 400 (validation), or 409 (overlap) |
| `POST` | `/api/bookings/{id}/cancel` | Cancel a booking: 200 (also when already cancelled) or 404 |

```bash
curl http://localhost:5059/api/rooms

curl "http://localhost:5059/api/bookings?date=2030-01-15"

curl -X POST http://localhost:5059/api/bookings \
  -H "Content-Type: application/json" \
  -d '{"roomId":1,"bookedBy":"Alice","startUtc":"2030-01-15T10:00:00Z","endUtc":"2030-01-15T11:00:00Z"}'
# 201 Created, Location: /api/bookings/{id}

curl -X POST http://localhost:5059/api/bookings \
  -H "Content-Type: application/json" \
  -d '{"roomId":1,"bookedBy":"Bob","startUtc":"2030-01-15T10:30:00Z","endUtc":"2030-01-15T11:30:00Z"}'
# 409 {"success":false,"message":"Room A is already booked 10:00-11:00 UTC",...}

curl -X POST http://localhost:5059/api/bookings/1/cancel
```

**Validation (400)** returns `"message": "Validation failed"` and a list of `errors`. A booking needs:

- an existing `roomId`
- a non-blank `bookedBy` of at most 100 characters
- `startUtc` and `endUtc` with an explicit zero offset (`Z` or `+00:00`). A timestamp with no offset is rejected rather than read in the server's local time zone.
- an end after its start
- an end no later than 00:00 UTC on the day after the start, so a booking stays within one UTC calendar day

Field errors (missing name or room) are reported before the time-rule errors. This is standard ASP.NET model validation behavior.

The frontend sends times from `toISOString()`, which always ends in `Z`.

## Concurrency approach

The database is the single source of truth. [Migration 001](server/RoomBooking/Database/Migrations/001_CreateRoomsAndBookings.sql) adds a PostgreSQL **exclusion constraint**:

```sql
EXCLUDE USING gist (
    room_id WITH =,
    tstzrange(start_utc, end_utc, '[)') WITH &&
) WHERE (cancelled_at IS NULL)
```

- `[)` makes ranges half-open, so 10:00-11:00 and 11:00-12:00 do not overlap. That makes adjacent bookings allowed.
- The partial `WHERE` makes only active bookings participate, so cancelling a booking frees its slot.
- `btree_gist` lets one GiST index combine equality on `room_id` with range overlap.

The API **does not check for overlaps before inserting**. A "SELECT then INSERT" check is a race: both requests can pass the check before either inserts. Instead, the API simply inserts. PostgreSQL enforces the constraint atomically, even for two uncommitted transactions racing each other. The loser gets SQLSTATE `23P01` (exclusion violation), which the API turns into **409 Conflict**. It then looks up the conflicting booking to build a useful message.

There is one subtlety, which the 25-round concurrency test exposed. When two overlapping inserts land at the same moment, each can end up waiting on the other's uncommitted index entry. PostgreSQL resolves that by aborting one with `40P01` (deadlock detected) instead of `23P01`. The API retries the insert (up to 3 attempts) on a deadlock. By then the winner has committed, so the retry fails cleanly with `23P01` and returns 409. If the winner rolled back instead, the retry succeeds.

Other choices:

- **Soft delete for cancellation:** `cancelled_at` is set, never deleted, and set again with `COALESCE`, so repeat cancels are idempotent and keep the original time.
- **Async all the way:** Dapper's async methods run on an `NpgsqlDataSource` connection pool.

## Tradeoffs

- **PostgreSQL-specific.** Exclusion constraints and `tstzrange` are not portable. On SQL Server or MySQL you would need serializable transactions, application locks, or a slot table. The payoff is that correctness doesn't depend on application code or isolation-level tuning.
- **Soft delete.** It keeps history and makes cancellation idempotent, but the table grows forever and every "active" query has to filter on `cancelled_at IS NULL`. The constraint and day query already do.
- **UTC single-day boundary.** A booking belongs to its start's UTC date and must end by 00:00 UTC the next day. This is simple and unambiguous, but for users far from UTC a "day" in the UI isn't their local day. The UI labels everything UTC to make that explicit.
- **No optimistic UI.** The frontend waits for the API before updating the list. That is simpler and always truthful, but slightly slower to feel.
- **Migrations at startup.** These are convenient for a local app. In a multi-instance deployment, migrations should run as a separate step with a separate, more privileged credential.
- **Validation in two layers.** Data annotations and `IValidatableObject` handle input rules, and the database enforces overlap, room existence (foreign key), and `end > start` (`CHECK`). A request that bypasses validation still can't corrupt data.

## What I'd improve next

- Room time zones, with local-time display alongside UTC
- Week view and pagination or range queries for bookings
- Authentication, so only the person who booked (or an admin) can cancel
- Rate limiting
- Idempotency keys on `POST /api/bookings`, so client retries can't double-submit
- Infrastructure as code for a deployed environment

## Project structure

```text
client/                         Vue 3 + Vuetify frontend (Vite)
  src/components/               RoomBookings (page), BookingForm, interaction test
  src/services/booking.service.ts   Typed API client
server/
  RoomBooking/                  ASP.NET Core API
    Controllers/                RoomsController, BookingsController
    Database/                   DbUp migrator, SQL migrations, Development seeder
    Services/                   Dapper data access
  RoomBooking.IntegrationTests/ xUnit + Testcontainers integration tests
docker-compose.yml              Local PostgreSQL
```

## AI and library disclosure

- **AI:** This project was built with help from Claude Code (Anthropic's AI coding assistant). It was used to implement the code, tests, and documentation from a plan I wrote, and I reviewed the result.
- **Backend libraries:** ASP.NET Core, [Dapper](https://github.com/DapperLib/Dapper), [Npgsql](https://www.npgsql.org/), [DbUp](https://dbup.readthedocs.io/), [xUnit](https://xunit.net/), [Testcontainers for .NET](https://dotnet.testcontainers.org/), and `Microsoft.AspNetCore.Mvc.Testing`.
- **Frontend libraries:** [Vue 3](https://vuejs.org/), [Vuetify 3](https://vuetifyjs.com/), [Vite](https://vite.dev/), [Vitest](https://vitest.dev/), [Vue Test Utils](https://test-utils.vuejs.org/), [Playwright](https://playwright.dev/), [node-postgres](https://node-postgres.com/) (end-to-end test reset), ESLint with typescript-eslint, and Material Design Icons.
