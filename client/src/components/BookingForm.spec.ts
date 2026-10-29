import { afterEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createVuetify } from "vuetify";
import { VSelect } from "vuetify/components";
import BookingForm from "./BookingForm.vue";
import { booking, json, requestBody, rooms, stubApi } from "../test/api";

const mountForm = (): VueWrapper =>
  mount(BookingForm, {
    props: { rooms, date: "2030-01-15" },
    global: { plugins: [createVuetify()] },
  });

const fill = async (
  wrapper: VueWrapper,
  values: { bookedBy?: string; startTime?: string; endTime?: string },
): Promise<void> => {
  for (const [name, value] of Object.entries(values)) {
    await wrapper.find(`input[name="${name}"]`).setValue(value);
  }
};

const submit = async (wrapper: VueWrapper): Promise<void> => {
  await wrapper.find("form").trigger("submit");
  await flushPromises();
};

describe("BookingForm", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sends the selected room, name, and UTC times, then emits the new booking and clears the form", async () => {
    const created = booking({ id: 9, roomId: 2, roomName: "Room B" });
    const fetchMock = stubApi({
      "POST /api/bookings": () => json(201, { success: true, data: created }),
    });
    const wrapper = mountForm();

    await wrapper.findComponent(VSelect).setValue(2);
    await fill(wrapper, { bookedBy: "Alice", startTime: "10:00", endTime: "11:00" });
    await submit(wrapper);

    expect(requestBody(fetchMock, "POST /api/bookings")).toEqual({
      roomId: 2,
      bookedBy: "Alice",
      startUtc: "2030-01-15T10:00:00.000Z",
      endUtc: "2030-01-15T11:00:00.000Z",
    });
    expect(wrapper.emitted("created")).toEqual([[created]]);
    expect((wrapper.find('input[name="bookedBy"]').element as HTMLInputElement).value).toBe("");
    expect((wrapper.find('input[name="startTime"]').element as HTMLInputElement).value).toBe("");
    expect(wrapper.find('[data-testid="conflict-message"]').exists()).toBe(false);
    expect(wrapper.find('[data-testid="validation-errors"]').exists()).toBe(false);
  });

  it("defaults to the first room", async () => {
    const fetchMock = stubApi({
      "POST /api/bookings": () => json(201, { success: true, data: booking() }),
    });
    const wrapper = mountForm();

    await fill(wrapper, { bookedBy: "Alice", startTime: "10:00", endTime: "11:00" });
    await submit(wrapper);

    expect(requestBody(fetchMock, "POST /api/bookings")).toMatchObject({ roomId: 1 });
  });

  it("sends an end time of 00:00 as midnight at the end of the day", async () => {
    const fetchMock = stubApi({
      "POST /api/bookings": () => json(201, { success: true, data: booking() }),
    });
    const wrapper = mountForm();

    await fill(wrapper, { bookedBy: "Alice", startTime: "23:00", endTime: "00:00" });
    await submit(wrapper);

    expect(requestBody(fetchMock, "POST /api/bookings")).toMatchObject({
      startUtc: "2030-01-15T23:00:00.000Z",
      endUtc: "2030-01-16T00:00:00.000Z",
    });
  });

  it("sends empty times as null and shows every validation error from a 400", async () => {
    const errors = ["Name is required", "Start time is required", "End time is required"];
    const fetchMock = stubApi({
      "POST /api/bookings": () => json(400, { success: false, message: "Validation failed", errors }),
    });
    const wrapper = mountForm();

    await submit(wrapper);

    expect(requestBody(fetchMock, "POST /api/bookings")).toEqual({
      roomId: 1,
      bookedBy: "",
      startUtc: null,
      endUtc: null,
    });
    const items = wrapper.findAll('[data-testid="validation-errors"] li').map((li) => li.text());
    expect(items).toEqual(errors);
    expect(wrapper.find('[data-testid="conflict-message"]').exists()).toBe(false);
    expect(wrapper.emitted("created")).toBeUndefined();
  });

  it("keeps the entered values after a conflict and clears the message on a successful retry", async () => {
    let conflict = true;
    stubApi({
      "POST /api/bookings": () =>
        conflict
          ? json(409, { success: false, message: "Room A is already booked 10:00-11:00 UTC" })
          : json(201, { success: true, data: booking() }),
    });
    const wrapper = mountForm();

    await fill(wrapper, { bookedBy: "Bob", startTime: "10:30", endTime: "11:30" });
    await submit(wrapper);

    expect(wrapper.find('[data-testid="conflict-message"]').text()).toContain(
      "Room A is already booked 10:00-11:00 UTC",
    );
    expect((wrapper.find('input[name="bookedBy"]').element as HTMLInputElement).value).toBe("Bob");

    conflict = false;
    await fill(wrapper, { startTime: "11:00", endTime: "12:00" });
    await submit(wrapper);

    expect(wrapper.find('[data-testid="conflict-message"]').exists()).toBe(false);
    expect(wrapper.emitted("created")).toHaveLength(1);
  });

  it("shows a network failure as an error", async () => {
    vi.stubGlobal("fetch", vi.fn(() => Promise.reject(new TypeError("Failed to fetch"))));
    const wrapper = mountForm();

    await fill(wrapper, { bookedBy: "Alice", startTime: "10:00", endTime: "11:00" });
    await submit(wrapper);

    expect(wrapper.find('[data-testid="validation-errors"]').text()).toContain("Failed to fetch");
  });
});
