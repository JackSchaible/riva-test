import type { ApiResponse } from "../models/api.response";
import type { Booking, CreateBookingRequest, Room } from "../models/booking";

const apiBase = import.meta.env.VITE_API_BASE_URL ?? "/api";

/** An unsuccessful API response, carrying the server's message and any validation errors. */
export class ApiError extends Error {
  readonly status: number;
  readonly errors: string[];

  constructor(message: string, status: number, errors: string[] = []) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.errors = errors;
  }
}

const request = async <T>(path: string, init?: RequestInit): Promise<T> => {
  const response = await fetch(`${apiBase}${path}`, {
    ...init,
    headers: { Accept: "application/json", "Content-Type": "application/json" },
  });

  let body: ApiResponse<T> | null = null;
  try {
    body = (await response.json()) as ApiResponse<T>;
  } catch {
    // Non-JSON response (e.g. a proxy error page); fall through to the generic message.
  }

  if (!response.ok || body?.success !== true) {
    throw new ApiError(
      body?.message ?? `Request failed with status ${String(response.status)}`,
      response.status,
      body?.errors ?? [],
    );
  }

  return body.data as T;
};

export const listRooms = (): Promise<Room[]> => request<Room[]>("/rooms");

/** Active bookings for every room on a UTC day (YYYY-MM-DD). */
export const listBookings = (date: string): Promise<Booking[]> =>
  request<Booking[]>(`/bookings?date=${encodeURIComponent(date)}`);

export const createBooking = (booking: CreateBookingRequest): Promise<Booking> =>
  request<Booking>("/bookings", {
    method: "POST",
    body: JSON.stringify(booking),
  });

export const cancelBooking = (id: number): Promise<Booking> =>
  request<Booking>(`/bookings/${String(id)}/cancel`, { method: "POST" });
