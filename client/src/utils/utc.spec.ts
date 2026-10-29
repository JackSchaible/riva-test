import { afterEach, describe, expect, it, vi } from "vitest";
import { formatUtcTime, todayUtc, toUtcEndIso, toUtcIso } from "./utc";

describe("UTC helpers", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("todayUtc uses the UTC date, not the local one", () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2030-01-15T23:30:00-05:00")); // already Jan 16 in UTC

    expect(todayUtc()).toBe("2030-01-16");
  });

  it("toUtcIso combines a date and time as UTC", () => {
    expect(toUtcIso("2030-01-15", "10:30")).toBe("2030-01-15T10:30:00.000Z");
  });

  it("toUtcEndIso keeps ordinary end times on the same day", () => {
    expect(toUtcEndIso("2030-01-15", "11:00")).toBe("2030-01-15T11:00:00.000Z");
  });

  it("toUtcEndIso treats 00:00 as midnight at the end of the day", () => {
    expect(toUtcEndIso("2030-01-15", "00:00")).toBe("2030-01-16T00:00:00.000Z");
  });

  it("toUtcEndIso rolls midnight over month and year boundaries", () => {
    expect(toUtcEndIso("2030-12-31", "00:00")).toBe("2031-01-01T00:00:00.000Z");
  });

  it("formatUtcTime shows HH:mm in UTC whatever the input offset", () => {
    expect(formatUtcTime("2030-01-15T10:05:00+00:00")).toBe("10:05");
    expect(formatUtcTime("2030-01-15T12:05:00+02:00")).toBe("10:05");
  });
});
