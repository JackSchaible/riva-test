/** Settings for the end-to-end stack. Ports differ from `dev.sh` so both can run at once. */
export const apiPort = Number(process.env.E2E_API_PORT ?? 5159);
export const webPort = Number(process.env.E2E_WEB_PORT ?? 5174);

/**
 * A dedicated database on the local (docker compose) PostgreSQL, so end-to-end runs never touch dev data.
 * The API creates it on startup if it does not exist.
 */
export const database = {
  host: process.env.E2E_DB_HOST ?? "localhost",
  port: Number(process.env.E2E_DB_PORT ?? 5432),
  user: process.env.E2E_DB_USER ?? "postgres",
  password: process.env.E2E_DB_PASSWORD ?? "postgres",
  database: process.env.E2E_DB_NAME ?? "room_booking_e2e",
};

export const apiConnectionString =
  `Host=${database.host};Port=${String(database.port)};Database=${database.database};` +
  `Username=${database.user};Password=${database.password}`;
