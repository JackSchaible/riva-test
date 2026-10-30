import { vi } from "vitest";
import type { Booking, Room } from "../models/booking";

export const rooms: Room[] = [
  { id: 1, name: "Room A" },
  { id: 2, name: "Room B" },
  { id: 3, name: "Room C" },
];

export const booking = (overrides: Partial<Booking> = {}): Booking => ({
  id: 1,
  roomId: 1,
  roomName: "Room A",
  bookedBy: "Alice",
  startUtc: "2030-01-15T10:00:00+00:00",
  endUtc: "2030-01-15T11:00:00+00:00",
  createdAt: "2030-01-01T00:00:00+00:00",
  cancelledAt: null,
  ...overrides,
});

export const json = (status: number, body: unknown): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });

export const ok = (data: unknown): Response => json(200, { success: true, data });

type Handler = (init?: RequestInit) => Response;

/**
 * Replaces global fetch with a stub that answers requests by "METHOD url" (e.g. "GET /api/rooms").
 * Unmatched requests reject, so a test fails loudly if the component calls something unexpected.
 */
export const stubApi = (routes: Record<string, Handler>): ReturnType<typeof vi.fn> => {
  const fetchMock = vi.fn((url: string, init?: RequestInit): Promise<Response> => {
    const key = `${init?.method ?? "GET"} ${url}`;
    const handler = routes[key] as Handler | undefined;
    return handler !== undefined
      ? Promise.resolve(handler(init))
      : Promise.reject(new Error(`Unexpected request: ${key}`));
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
};

/** The parsed JSON body of the first request matching "METHOD url". */
export const requestBody = (fetchMock: ReturnType<typeof vi.fn>, key: string): unknown => {
  const call = fetchMock.mock.calls.find(
    ([url, init]) => `${(init as RequestInit | undefined)?.method ?? "GET"} ${url as string}` === key,
  );
  const body = (call?.[1] as RequestInit | undefined)?.body;
  return typeof body === "string" ? JSON.parse(body) : undefined;
};
