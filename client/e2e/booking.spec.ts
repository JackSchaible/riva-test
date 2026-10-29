import { expect, test, type Locator, type Page } from "@playwright/test";

// Each test books on its own UTC day, so tests can run in parallel without seeing each other's bookings.

const openDay = async (page: Page, date: string): Promise<void> => {
  await page.goto("/");
  await page.getByLabel("Day (UTC)").fill(date);
  await expect(roomCard(page, "Room A")).toContainText(`${date} (UTC)`);
};

const roomCard = (page: Page, room: string): Locator =>
  page.locator(".v-card").filter({ has: page.locator(".v-card-title", { hasText: room }) });

const fillBooking = async (
  page: Page,
  booking: { room?: string; name: string; start: string; end: string },
): Promise<void> => {
  if (booking.room !== undefined) {
    await page.getByRole("combobox").filter({ hasText: "Room" }).click();
    await page.getByRole("option", { name: booking.room }).click();
  }
  await page.getByLabel("Your name").fill(booking.name);
  await page.getByLabel("Start time (UTC)").fill(booking.start);
  await page.getByLabel("End time (UTC)").fill(booking.end);
};

const bookRoom = async (
  page: Page,
  booking: { room?: string; name: string; start: string; end: string },
): Promise<void> => {
  await fillBooking(page, booking);
  await page.getByRole("button", { name: "Book room" }).click();
};

test("lists the three rooms for the selected UTC day", async ({ page }) => {
  await openDay(page, "2031-01-01");

  for (const room of ["Room A", "Room B", "Room C"]) {
    await expect(roomCard(page, room)).toContainText("No bookings for this day.");
  }
  await expect(page.getByText("all times are UTC")).toBeVisible();
});

test("books a room and cancels the booking", async ({ page }) => {
  await openDay(page, "2031-01-02");

  await bookRoom(page, { room: "Room B", name: "Alice", start: "10:00", end: "11:00" });

  const roomB = roomCard(page, "Room B");
  await expect(roomB).toContainText("10:00–11:00 UTC");
  await expect(roomB).toContainText("Alice");
  await expect(roomCard(page, "Room A")).toContainText("No bookings for this day.");

  // The booking is stored by the API, not just shown by the page.
  await page.reload();
  await page.getByLabel("Day (UTC)").fill("2031-01-02");
  await expect(roomB).toContainText("Alice");

  await roomB.getByRole("button", { name: "Cancel" }).click();
  await expect(roomB).toContainText("No bookings for this day.");
});

test("rejects an overlapping booking but allows an adjacent one", async ({ page }) => {
  await openDay(page, "2031-01-03");
  await bookRoom(page, { name: "Alice", start: "10:00", end: "11:00" });
  await expect(roomCard(page, "Room A")).toContainText("Alice");

  await bookRoom(page, { name: "Bob", start: "10:30", end: "11:30" });
  await expect(page.getByText("Room A is already booked 10:00-11:00 UTC")).toBeVisible();
  await expect(roomCard(page, "Room A")).not.toContainText("Bob");

  await page.getByLabel("Start time (UTC)").fill("11:00");
  await page.getByLabel("End time (UTC)").fill("12:00");
  await page.getByRole("button", { name: "Book room" }).click();

  await expect(roomCard(page, "Room A")).toContainText("11:00–12:00 UTC");
  await expect(page.getByText("Room A is already booked")).toBeHidden();
});

test("allows the same time in different rooms", async ({ page }) => {
  await openDay(page, "2031-01-04");

  await bookRoom(page, { room: "Room A", name: "Alice", start: "09:00", end: "10:00" });
  await expect(roomCard(page, "Room A")).toContainText("Alice");
  await bookRoom(page, { room: "Room C", name: "Carol", start: "09:00", end: "10:00" });

  await expect(roomCard(page, "Room C")).toContainText("Carol");
  await expect(roomCard(page, "Room A")).toContainText("Alice");
});

test("shows validation errors from the API", async ({ page }) => {
  await openDay(page, "2031-01-05");

  await page.getByRole("button", { name: "Book room" }).click();
  await expect(page.getByText("Name is required")).toBeVisible();

  await bookRoom(page, { name: "Alice", start: "11:00", end: "10:00" });
  await expect(page.getByText("End time must be after start time")).toBeVisible();
});

test("books until midnight with an end time of 00:00", async ({ page }) => {
  await openDay(page, "2031-01-06");

  await bookRoom(page, { name: "Night owl", start: "23:00", end: "00:00" });

  await expect(roomCard(page, "Room A")).toContainText("23:00–00:00 UTC");
});

test("a cancelled slot can be booked again", async ({ page }) => {
  await openDay(page, "2031-01-07");
  await bookRoom(page, { name: "Alice", start: "14:00", end: "15:00" });
  const roomA = roomCard(page, "Room A");
  await roomA.getByRole("button", { name: "Cancel" }).click();
  await expect(roomA).toContainText("No bookings for this day.");

  await bookRoom(page, { name: "Bob", start: "14:00", end: "15:00" });

  await expect(roomA).toContainText("Bob");
  await expect(roomA).not.toContainText("Alice");
});

test("two people booking the same slot at once: one succeeds, the other sees the conflict", async ({
  browser,
}) => {
  const date = "2031-01-08";
  const alice = await (await browser.newContext()).newPage();
  const bob = await (await browser.newContext()).newPage();
  await openDay(alice, date);
  await openDay(bob, date);
  await fillBooking(alice, { name: "Alice", start: "10:00", end: "11:00" });
  await fillBooking(bob, { name: "Bob", start: "10:30", end: "11:30" });

  await Promise.all([
    alice.getByRole("button", { name: "Book room" }).click(),
    bob.getByRole("button", { name: "Book room" }).click(),
  ]);

  // Wait for both forms to finish, then exactly one of them shows the conflict.
  const conflict = /Room A is already booked/;
  await expect(async () => {
    const conflicts =
      (await alice.getByText(conflict).count()) + (await bob.getByText(conflict).count());
    expect(conflicts).toBe(1);
  }).toPass();

  // Both people see the same single booking after refreshing.
  for (const page of [alice, bob]) {
    await openDay(page, date);
    await expect(roomCard(page, "Room A").getByRole("button", { name: "Cancel" })).toHaveCount(1);
  }
});
