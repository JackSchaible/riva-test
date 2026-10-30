/** Today's date in UTC as YYYY-MM-DD. */
export const todayUtc = (): string => new Date().toISOString().slice(0, 10);

/** Combines a YYYY-MM-DD date and HH:mm time into a UTC ISO timestamp. */
export const toUtcIso = (date: string, time: string): string =>
  new Date(`${date}T${time}:00Z`).toISOString();

/** End times use 00:00 to mean midnight at the end of the day. */
export const toUtcEndIso = (date: string, time: string): string => {
  const end = new Date(`${date}T${time}:00Z`);
  if (time === "00:00") {
    end.setUTCDate(end.getUTCDate() + 1);
  }
  return end.toISOString();
};

/** HH:mm in UTC for an ISO timestamp. */
export const formatUtcTime = (iso: string): string => new Date(iso).toISOString().slice(11, 16);
