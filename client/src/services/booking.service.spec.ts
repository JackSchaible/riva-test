import { afterEach, describe, expect, it, vi } from "vitest";
import { booking, json, ok, requestBody, rooms, stubApi } from "../test/api";
import {
  ApiError,
  cancelBooking,
  createBooking,
  listBookings,
  listRooms,
} from "./booking.service";

describe("booking.service", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("lists rooms from /api/rooms", async () => {
    stubApi({ "GET /api/rooms": () => ok(rooms) });

    await expect(listRooms()).resolves.toEqual(rooms);
  });

  it("lists bookings for a UTC day", async () => {
    stubApi({ "GET /api/bookings?date=2030-01-15": () => ok([booking()]) });

    await expect(listBookings("2030-01-15")).resolves.toEqual([booking()]);
  });

  it("posts a new booking as JSON", async () => {
    const fetchMock = stubApi({ "POST /api/bookings": () => json(201, { success: true, data: booking() }) });
    const request = {
      roomId: 1,
      bookedBy: "Alice",
      startUtc: "2030-01-15T10:00:00.000Z",
      endUtc: "2030-01-15T11:00:00.000Z",
    };

    await expect(createBooking(request)).resolves.toEqual(booking());
    expect(requestBody(fetchMock, "POST /api/bookings")).toEqual(request);
  });

  it("cancels with POST /api/bookings/{id}/cancel", async () => {
    const cancelled = booking({ id: 7, cancelledAt: "2030-01-14T00:00:00+00:00" });
    stubApi({ "POST /api/bookings/7/cancel": () => ok(cancelled) });

    await expect(cancelBooking(7)).resolves.toEqual(cancelled);
  });

  it("turns a validation failure into an ApiError with the server's errors", async () => {
    stubApi({
      "POST /api/bookings": () =>
        json(400, {
          success: false,
          message: "Validation failed",
          errors: ["Name is required", "End time must be after start time"],
        }),
    });

    const error = await createBooking({ roomId: 1, bookedBy: "", startUtc: null, endUtc: null }).catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 400,
      message: "Validation failed",
      errors: ["Name is required", "End time must be after start time"],
    });
  });

  it("treats success: false as an error even with a 200 status", async () => {
    stubApi({ "GET /api/rooms": () => json(200, { success: false, message: "Nope" }) });

    await expect(listRooms()).rejects.toMatchObject({ status: 200, message: "Nope" });
  });

  it("falls back to a generic message when the response is not JSON", async () => {
    stubApi({ "GET /api/rooms": () => new Response("<html>Bad Gateway</html>", { status: 502 }) });

    await expect(listRooms()).rejects.toMatchObject({
      status: 502,
      message: "Request failed with status 502",
      errors: [],
    });
  });
});
