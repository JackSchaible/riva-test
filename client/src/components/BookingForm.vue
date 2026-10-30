<script setup lang="ts">
import { ref, watch } from "vue";
import type { Booking, Room } from "../models/booking";
import { ApiError, createBooking } from "../services/booking.service";
import { toUtcEndIso, toUtcIso } from "../utils/utc";

const props = defineProps<{
  rooms: Room[];
  /** The UTC day being booked, YYYY-MM-DD. */
  date: string;
}>();

const emit = defineEmits<{
  created: [booking: Booking];
}>();

const roomId = ref<number | null>(null);
const bookedBy = ref("");
const startTime = ref("");
const endTime = ref("");

const saving = ref(false);
const conflictMessage = ref<string | null>(null);
const errors = ref<string[]>([]);

// Default to the first room once rooms have loaded.
watch(
  () => props.rooms,
  (rooms) => {
    roomId.value ??= rooms[0]?.id ?? null;
  },
  { immediate: true },
);

const clearMessages = (): void => {
  conflictMessage.value = null;
  errors.value = [];
};

const submit = async (): Promise<void> => {
  clearMessages();
  saving.value = true;

  try {
    // Validation is left to the API so the form shows exactly the server's rules.
    const booking = await createBooking({
      roomId: roomId.value,
      bookedBy: bookedBy.value,
      startUtc: startTime.value ? toUtcIso(props.date, startTime.value) : null,
      endUtc: endTime.value ? toUtcEndIso(props.date, endTime.value) : null,
    });

    bookedBy.value = "";
    startTime.value = "";
    endTime.value = "";
    emit("created", booking);
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      conflictMessage.value = err.message;
    } else if (err instanceof ApiError && err.errors.length > 0) {
      errors.value = err.errors;
    } else {
      errors.value = [err instanceof Error ? err.message : "Something went wrong"];
    }
  } finally {
    saving.value = false;
  }
};
</script>

<template>
  <v-card>
    <v-card-title>New booking</v-card-title>
    <v-card-subtitle>For {{ date }} &middot; all times are UTC</v-card-subtitle>

    <v-form @submit.prevent="submit">
      <v-card-text>
        <v-select
          v-model="roomId"
          :items="rooms"
          item-title="name"
          item-value="id"
          label="Room"
          name="roomId"
          variant="outlined"
          density="comfortable"
        />
        <v-text-field
          v-model="bookedBy"
          label="Your name"
          name="bookedBy"
          variant="outlined"
          density="comfortable"
          counter="100"
        />
        <v-row dense>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="startTime"
              type="time"
              label="Start time (UTC)"
              name="startTime"
              variant="outlined"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="endTime"
              type="time"
              label="End time (UTC)"
              name="endTime"
              hint="Use 00:00 for midnight at the end of the day"
              persistent-hint
              variant="outlined"
              density="comfortable"
            />
          </v-col>
        </v-row>

        <v-alert
          v-if="conflictMessage"
          type="warning"
          variant="tonal"
          class="mt-4"
          data-testid="conflict-message"
        >
          {{ conflictMessage }}
        </v-alert>

        <v-alert
          v-if="errors.length > 0"
          type="error"
          variant="tonal"
          class="mt-4"
          data-testid="validation-errors"
        >
          <ul class="ml-4">
            <li v-for="error in errors" :key="error">{{ error }}</li>
          </ul>
        </v-alert>
      </v-card-text>

      <v-card-actions>
        <v-btn type="submit" color="primary" variant="flat" :loading="saving" block>
          Book room
        </v-btn>
      </v-card-actions>
    </v-form>
  </v-card>
</template>
