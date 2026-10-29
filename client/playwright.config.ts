import { defineConfig, devices } from "@playwright/test";
import { apiConnectionString, apiPort, webPort } from "./e2e/env";

const apiUrl = `http://localhost:${String(apiPort)}`;
const webUrl = `http://localhost:${String(webPort)}`;
const ci = Boolean(process.env.CI);

/**
 * End-to-end tests against the real stack: Vite, the ASP.NET API, and PostgreSQL.
 * PostgreSQL must already be running (`docker compose up -d --wait` from the repo root).
 */
export default defineConfig({
  testDir: "./e2e",
  globalSetup: "./e2e/global-setup.ts",
  fullyParallel: true,
  forbidOnly: ci,
  retries: ci ? 1 : 0,
  reporter: ci ? [["list"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: webUrl,
    trace: "retain-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      // --no-launch-profile skips launchSettings.json (Development environment, port 5059), so the
      // sample bookings are not seeded and the tests start from an empty schedule.
      command: `dotnet run --project ../server/RoomBooking --no-launch-profile --urls ${apiUrl}`,
      url: `${apiUrl}/api/rooms`,
      env: {
        ASPNETCORE_ENVIRONMENT: "E2E",
        ConnectionStrings__DefaultConnection: apiConnectionString,
      },
      reuseExistingServer: !ci,
      timeout: 180_000,
    },
    {
      command: `pnpm dev --port ${String(webPort)} --strictPort`,
      url: webUrl,
      env: { API_PROXY_TARGET: apiUrl },
      reuseExistingServer: !ci,
    },
  ],
});
