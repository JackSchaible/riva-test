-- btree_gist lets the exclusion constraint combine equality on room_id with range overlap.
CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE rooms (
    id   integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name text NOT NULL UNIQUE
);

CREATE TABLE bookings (
    id           integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    room_id      integer     NOT NULL REFERENCES rooms (id),
    booked_by    text        NOT NULL,
    start_utc    timestamptz NOT NULL,
    end_utc      timestamptz NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now(),
    cancelled_at timestamptz NULL,

    CONSTRAINT bookings_end_after_start CHECK (end_utc > start_utc),

    -- The source of truth for "no double bookings": two active bookings for the same room may not
    -- have overlapping half-open [start, end) ranges, so 10:00-11:00 and 11:00-12:00 can coexist.
    CONSTRAINT bookings_no_overlap EXCLUDE USING gist (
        room_id WITH =,
        tstzrange(start_utc, end_utc, '[)') WITH &&
    ) WHERE (cancelled_at IS NULL)
);

CREATE INDEX bookings_room_id_start_utc_idx ON bookings (room_id, start_utc);
