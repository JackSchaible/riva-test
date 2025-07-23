<template>
  <v-card elevation="2" hover variant="elevated">
    <v-card-text class="py-4">
      <!-- Contact Name Row - Full Width -->
      <div class="mb-3">
        <h3 class="text-h5 font-weight-medium text-white">
          {{ contact.firstName }} {{ contact.lastName }}
        </h3>
      </div>

      <!-- Mobile-Responsive Layout -->
      <!-- Desktop Layout: Avatar + Contact Info + Action Buttons in a row -->
      <div class="d-flex align-center justify-space-between desktop-layout">
        <!-- Avatar -->
        <v-avatar color="primary" class="mr-4" size="48">
          <span class="text-h5 font-weight-bold">
            {{ contact.firstName.charAt(0) + contact.lastName.charAt(0) }}
          </span>
        </v-avatar>

        <!-- Contact Info (Stacked Vertically) -->
        <div class="flex-grow-1 ml-8 mr-8">
          <div class="d-flex align-center mb-2">
            <v-icon
              icon="mdi-email"
              size="small"
              class="mr-3 text-primary"
            ></v-icon>
            <a
              :href="`mailto:${contact.email}`"
              class="text-body-1 text-decoration-none contact-link text-white contact-info-text"
              @click.stop
            >
              {{ contact.email }}
            </a>
          </div>
          <div class="d-flex align-center">
            <v-icon
              icon="mdi-phone"
              size="small"
              class="mr-3 text-primary"
            ></v-icon>
            <a
              :href="`tel:${contact.phone}`"
              class="text-body-1 text-decoration-none contact-link text-white contact-info-text"
              @click.stop
            >
              {{ contact.phone }}
            </a>
          </div>
        </div>

        <!-- Action Buttons -->
        <div class="d-flex ga-2">
          <v-btn
            @click="$emit('edit', contact)"
            color="primary"
            variant="elevated"
            size="small"
            icon="mdi-pencil"
            class="action-btn"
          ></v-btn>
          <v-btn
            @click="$emit('delete', contact)"
            color="error"
            variant="elevated"
            size="small"
            icon="mdi-delete"
            class="action-btn delete-btn"
          ></v-btn>
        </div>
      </div>

      <!-- Mobile Layout: Stacked vertically -->
      <div class="mobile-layout">
        <!-- Avatar and Action Buttons Row -->
        <div class="d-flex align-center justify-space-between mb-3">
          <v-avatar color="primary" size="48">
            <span class="text-h5 font-weight-bold">
              {{ contact.firstName.charAt(0) + contact.lastName.charAt(0) }}
            </span>
          </v-avatar>
          
          <!-- Action Buttons -->
          <div class="d-flex ga-2">
            <v-btn
              @click="$emit('edit', contact)"
              color="primary"
              variant="elevated"
              size="small"
              icon="mdi-pencil"
              class="action-btn"
            ></v-btn>
            <v-btn
              @click="$emit('delete', contact)"
              color="error"
              variant="elevated"
              size="small"
              icon="mdi-delete"
              class="action-btn delete-btn"
            ></v-btn>
          </div>
        </div>

        <!-- Contact Info (Full Width) -->
        <div class="contact-info-mobile">
          <div class="d-flex align-center mb-2">
            <v-icon
              icon="mdi-email"
              size="small"
              class="mr-3 text-primary"
            ></v-icon>
            <a
              :href="`mailto:${contact.email}`"
              class="text-body-1 text-decoration-none contact-link text-white contact-info-text"
              @click.stop
            >
              {{ contact.email }}
            </a>
          </div>
          <div class="d-flex align-center">
            <v-icon
              icon="mdi-phone"
              size="small"
              class="mr-3 text-primary"
            ></v-icon>
            <a
              :href="`tel:${contact.phone}`"
              class="text-body-1 text-decoration-none contact-link text-white contact-info-text"
              @click.stop
            >
              {{ contact.phone }}
            </a>
          </div>
        </div>
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import type { Contact } from "../models/contact";

// Props
defineProps<{
  contact: Contact;
}>();

// Emits
defineEmits<{
  edit: [contact: Contact];
  delete: [contact: Contact];
}>();
</script>

<style scoped>
/* Hover effects for cards */
.v-card {
  transition: box-shadow 0.2s ease-in-out, transform 0.1s ease-in-out;
  cursor: default !important;
  background: #424242 !important; /* Dark gray background */
  border: 1px solid rgba(255, 255, 255, 0.12);
}

.v-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15), 0 2px 6px rgba(0, 0, 0, 0.1) !important;
  transform: translateY(-1px);
}

/* Avatar styling */
.v-avatar {
  font-weight: 600;
  cursor: default !important;
}

/* Action button styling */
.action-btn {
  cursor: pointer !important;
  width: 40px !important;
  height: 40px !important;
  transition: all 0.2s ease-in-out;
}

.action-btn:hover {
  transform: scale(1.05);
}

/* Improved contrast for delete button */
.delete-btn {
  background-color: #d32f2f !important;
  color: white !important;
}

.delete-btn:hover {
  background-color: #b71c1c !important;
}

/* Contact link styling - IMPORTANT: pointer cursor for links */
.contact-link {
  cursor: pointer !important;
  color: white !important;
  transition: color 0.2s ease-in-out;
  font-weight: 500;
}

.contact-link:hover {
  color: rgb(var(--v-theme-primary)) !important;
  cursor: pointer !important;
}

/* Text styling - ensure white text */
.text-white {
  color: white !important;
}

/* Text and icon elements should have default cursor */
.v-icon,
.text-h5,
.text-body-1,
span,
h3 {
  cursor: default !important;
}

/* Override for link icons specifically */
.contact-link .v-icon {
  cursor: pointer !important;
}

/* Icon color styling */
.text-primary {
  color: rgb(var(--v-theme-primary)) !important;
  opacity: 0.9;
}

/* Mobile Responsive Layouts */
.desktop-layout {
  display: flex;
}

.mobile-layout {
  display: none;
}

/* Mobile Styles */
@media (max-width: 768px) {
  .desktop-layout {
    display: none;
  }
  
  .mobile-layout {
    display: block;
  }
  
  .contact-info-mobile {
    width: 100%;
  }
  
  .contact-info-text {
    font-size: 0.875rem !important;
    word-break: break-all;
  }
  
  .v-card {
    margin-bottom: 8px;
  }
  
  .action-btn {
    width: 36px !important;
    height: 36px !important;
  }
}

/* Tablet and small desktop adjustments */
@media (max-width: 960px) and (min-width: 769px) {
  .desktop-layout .flex-grow-1 {
    margin-left: 16px !important;
    margin-right: 16px !important;
  }
  
  .contact-info-text {
    font-size: 0.875rem !important;
  }
}

/* Extra small mobile devices */
@media (max-width: 480px) {
  .contact-info-text {
    font-size: 0.75rem !important;
  }
  
  .v-avatar {
    width: 40px !important;
    height: 40px !important;
  }
  
  .v-avatar .text-h5 {
    font-size: 1rem !important;
  }
  
  .action-btn {
    width: 32px !important;
    height: 32px !important;
  }
}
</style>
