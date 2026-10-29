export interface Room {
  id: number;
  name: string;
}

/** Timestamps are ISO 8601 strings in UTC (offset +00:00). */
export interface Booking {
  id: number;
  roomId: number;
  roomName: string;
  bookedBy: string;
  startUtc: string;
  endUtc: string;
  createdAt: string;
  cancelledAt: string | null;
}

export interface CreateBookingRequest {
  roomId: number | null;
  bookedBy: string;
  startUtc: string | null;
  endUtc: string | null;
}
