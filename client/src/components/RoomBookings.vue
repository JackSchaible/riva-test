<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import type { Booking, Room } from "../models/booking";
import { cancelBooking, listBookings, listRooms } from "../services/booking.service";
import { formatUtcTime, todayUtc } from "../utils/utc";
import BookingForm from "./BookingForm.vue";

const rooms = ref<Room[]>([]);
const bookings = ref<Booking[]>([]);
const selectedDate = ref(todayUtc());

const loading = ref(false);
const loadError = ref<string | null>(null);
const cancellingId = ref<number | null>(null);
const cancelError = ref<string | null>(null);

const bookingsByRoom = computed(() =>
  rooms.value.map((room) => ({
    room,
    bookings: bookings.value.filter((b) => b.roomId === room.id),
  })),
);

const loadBookings = async (): Promise<void> => {
  if (!selectedDate.value) {
    return;
  }

  loading.value = true;
  loadError.value = null;

  try {
    bookings.value = await listBookings(selectedDate.value);
  } catch (err) {
    loadError.value = err instanceof Error ? err.message : "Failed to load bookings";
  } finally {
    loading.value = false;
  }
};

const load = async (): Promise<void> => {
  try {
    rooms.value = await listRooms();
  } catch (err) {
    loadError.value = err instanceof Error ? err.message : "Failed to load rooms";
    return;
  }
  await loadBookings();
};

const cancel = async (booking: Booking): Promise<void> => {
  cancellingId.value = booking.id;
  cancelError.value = null;

  try {
    await cancelBooking(booking.id);
    await loadBookings();
  } catch (err) {
    cancelError.value = err instanceof Error ? err.message : "Failed to cancel booking";
  } finally {
    cancellingId.value = null;
  }
};

watch(selectedDate, () => void loadBookings());

onMounted(() => void load());
</script>

<template>
  <v-container class="py-6" style="max-width: 1100px">
    <h1 class="text-h4 font-weight-light mb-6">
      <v-icon icon="mdi-calendar-clock" class="mr-2" />
      Meeting Room Bookings
    </h1>

    <v-row>
      <v-col cols="12" md="4">
        <v-text-field
          v-model="selectedDate"
          type="date"
          label="Day (UTC)"
          name="selectedDate"
          variant="outlined"
          density="comfortable"
          hint="Bookings are shown and created in UTC"
          persistent-hint
          class="mb-4"
        />
        <BookingForm :rooms="rooms" :date="selectedDate" @created="loadBookings" />
      </v-col>

      <v-col cols="12" md="8">
        <v-alert v-if="loadError" type="error" variant="tonal" class="mb-4">
          {{ loadError }}
          <template #append>
            <v-btn variant="outlined" size="small" @click="load">Retry</v-btn>
          </template>
        </v-alert>

        <v-alert
          v-if="cancelError"
          type="error"
          variant="tonal"
          closable
          class="mb-4"
          @click:close="cancelError = null"
        >
          {{ cancelError }}
        </v-alert>

        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-2" />

        <v-card v-for="{ room, bookings: roomBookings } in bookingsByRoom" :key="room.id" class="mb-4">
          <v-card-title>{{ room.name }}</v-card-title>
          <v-card-subtitle>{{ selectedDate }} (UTC)</v-card-subtitle>

          <v-list v-if="roomBookings.length > 0" density="compact">
            <v-list-item v-for="booking in roomBookings" :key="booking.id">
              <v-list-item-title>
                {{ formatUtcTime(booking.startUtc) }}&ndash;{{ formatUtcTime(booking.endUtc) }} UTC
              </v-list-item-title>
              <v-list-item-subtitle>{{ booking.bookedBy }}</v-list-item-subtitle>
              <template #append>
                <v-btn
                  variant="text"
                  color="error"
                  size="small"
                  prepend-icon="mdi-close"
                  :loading="cancellingId === booking.id"
                  @click="cancel(booking)"
                >
                  Cancel
                </v-btn>
              </template>
            </v-list-item>
          </v-list>
          <v-card-text v-else class="text-medium-emphasis">No bookings for this day.</v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>
