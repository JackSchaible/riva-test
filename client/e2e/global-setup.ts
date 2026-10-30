import pg from "pg";
import { database } from "./env";

// PostgreSQL error codes for a database or table that does not exist yet (first run).
const nothingToReset = new Set(["3D000", "42P01"]);

/** Removes bookings left by earlier runs. Rooms are seeded by the API's migrations and kept. */
export default async function globalSetup(): Promise<void> {
  const client = new pg.Client(database);
  try {
    await client.connect();
    await client.query("TRUNCATE bookings RESTART IDENTITY");
  } catch (error) {
    const code = (error as { code?: string }).code;
    if (code === undefined || !nothingToReset.has(code)) {
      throw error;
    }
  } finally {
    await client.end();
  }
}
