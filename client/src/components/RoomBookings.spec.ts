import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount, type DOMWrapper, type VueWrapper } from "@vue/test-utils";
import { createVuetify } from "vuetify";
import RoomBookings from "./RoomBookings.vue";
import { booking, json, ok, requestBody, rooms, stubApi } from "../test/api";

const today = "2030-01-15";

const mountPage = async (): Promise<VueWrapper> => {
  const wrapper = mount(RoomBookings, {
    global: { plugins: [createVuetify()] },
  });
  await flushPromises();
  return wrapper;
};

/** The card for a room, found by its title. */
const roomCard = (wrapper: VueWrapper, name: string): DOMWrapper<Element> => {
  const card = wrapper.findAll(".v-card").find((c) => c.find(".v-card-title").text() === name);
  if (!card) {
    throw new Error(`No card for ${name}`);
  }
  return card;
};

const buttonWithText = (wrapper: VueWrapper | DOMWrapper<Element>, text: string): DOMWrapper<Element> => {
  const button = wrapper.findAll("button").find((b) => b.text().includes(text));
  if (!button) {
    throw new Error(`No button with text "${text}"`);
  }
  return button;
};

describe("RoomBookings", () => {
  beforeEach(() => {
    // Only Date is faked, so promises and Vuetify's timers behave normally.
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(`${today}T12:00:00Z`));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("shows each room's bookings for today (UTC) with UTC times", async () => {
    stubApi({
      "GET /api/rooms": () => ok(rooms),
      [`GET /api/bookings?date=${today}`]: () =>
        ok([
          booking({ id: 1, bookedBy: "Alice" }),
          booking({
            id: 2,
            roomId: 2,
            roomName: "Room B",
            bookedBy: "Carol",
            startUtc: "2030-01-15T13:00:00+00:00",
            endUtc: "2030-01-15T14:30:00+00:00",
          }),
        ]),
    });

    const wrapper = await mountPage();

    expect((wrapper.find('input[name="selectedDate"]').element as HTMLInputElement).value).toBe(today);
    expect(roomCard(wrapper, "Room A").text()).toContain("10:00–11:00 UTC");
    expect(roomCard(wrapper, "Room A").text()).toContain("Alice");
    expect(roomCard(wrapper, "Room B").text()).toContain("13:00–14:30 UTC");
    expect(roomCard(wrapper, "Room B").text()).toContain("Carol");
    expect(roomCard(wrapper, "Room C").text()).toContain("No bookings for this day.");
  });

  it("loads the bookings for a newly selected day", async () => {
    const fetchMock = stubApi({
      "GET /api/rooms": () => ok(rooms),
      [`GET /api/bookings?date=${today}`]: () => ok([]),
      "GET /api/bookings?date=2030-02-01": () => ok([booking({ bookedBy: "Dave" })]),
    });
    const wrapper = await mountPage();

    await wrapper.find('input[name="selectedDate"]').setValue("2030-02-01");
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledWith("/api/bookings?date=2030-02-01", expect.anything());
    expect(roomCard(wrapper, "Room A").text()).toContain("Dave");
  });

  it("shows the conflict message when the API rejects an overlapping booking", async () => {
    const fetchMock = stubApi({
      "GET /api/rooms": () => ok(rooms),
      "GET /api/bookings?date=2030-01-20": () => ok([]),
      [`GET /api/bookings?date=${today}`]: () => ok([]),
      "POST /api/bookings": () =>
        json(409, {
          success: false,
          message: "Room A is already booked 10:00-11:00 UTC",
        }),
    });
    const wrapper = await mountPage();

    await wrapper.find('input[name="selectedDate"]').setValue("2030-01-20");
    await wrapper.find('input[name="bookedBy"]').setValue("Bob");
    await wrapper.find('input[name="startTime"]').setValue("10:30");
    await wrapper.find('input[name="endTime"]').setValue("11:30");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    // The form books the day selected on the page.
    expect(requestBody(fetchMock, "POST /api/bookings")).toEqual({
      roomId: 1,
      bookedBy: "Bob",
      startUtc: "2030-01-20T10:30:00.000Z",
      endUtc: "2030-01-20T11:30:00.000Z",
    });
    expect(wrapper.find('[data-testid="conflict-message"]').text()).toContain(
      "Room A is already booked 10:00-11:00 UTC",
    );
  });

  it("refreshes the bookings after a successful booking", async () => {
    let saved = false;
    stubApi({
      "GET /api/rooms": () => ok(rooms),
      [`GET /api/bookings?date=${today}`]: () => ok(saved ? [booking({ bookedBy: "Alice" })] : []),
      "POST /api/bookings": () => {
        saved = true;
        return json(201, { success: true, data: booking() });
      },
    });
    const wrapper = await mountPage();

    await wrapper.find('input[name="bookedBy"]').setValue("Alice");
    await wrapper.find('input[name="startTime"]').setValue("10:00");
    await wrapper.find('input[name="endTime"]').setValue("11:00");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(roomCard(wrapper, "Room A").text()).toContain("Alice");
  });

  it("cancels a booking and refreshes the list", async () => {
    let cancelled = false;
    const fetchMock = stubApi({
      "GET /api/rooms": () => ok(rooms),
      [`GET /api/bookings?date=${today}`]: () => ok(cancelled ? [] : [booking({ id: 5 })]),
      "POST /api/bookings/5/cancel": () => {
        cancelled = true;
        return ok(booking({ id: 5, cancelledAt: "2030-01-15T12:00:00+00:00" }));
      },
    });
    const wrapper = await mountPage();

    await buttonWithText(roomCard(wrapper, "Room A"), "Cancel").trigger("click");
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledWith("/api/bookings/5/cancel", expect.objectContaining({ method: "POST" }));
    expect(roomCard(wrapper, "Room A").text()).toContain("No bookings for this day.");
  });

  it("shows an error when cancelling fails and keeps the booking listed", async () => {
    stubApi({
      "GET /api/rooms": () => ok(rooms),
      [`GET /api/bookings?date=${today}`]: () => ok([booking({ id: 5, bookedBy: "Alice" })]),
      "POST /api/bookings/5/cancel": () => json(404, { success: false, message: "Booking 5 was not found" }),
    });
    const wrapper = await mountPage();

    await buttonWithText(roomCard(wrapper, "Room A"), "Cancel").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("Booking 5 was not found");
    expect(roomCard(wrapper, "Room A").text()).toContain("Alice");
  });

  it("shows a load error and recovers when Retry is clicked", async () => {
    let roomsAvailable = false;
    stubApi({
      "GET /api/rooms": () =>
        roomsAvailable
          ? ok(rooms)
          : json(500, { success: false, message: "An unexpected error occurred" }),
      [`GET /api/bookings?date=${today}`]: () => ok([booking({ bookedBy: "Alice" })]),
    });
    const wrapper = await mountPage();

    expect(wrapper.text()).toContain("An unexpected error occurred");
    expect(wrapper.findAll(".v-card-title").map((t) => t.text())).not.toContain("Room A");

    roomsAvailable = true;
    await buttonWithText(wrapper, "Retry").trigger("click");
    await flushPromises();

    expect(wrapper.text()).not.toContain("An unexpected error occurred");
    expect(roomCard(wrapper, "Room A").text()).toContain("Alice");
  });
});
