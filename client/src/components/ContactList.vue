<template>
  <v-container fluid class="pa-6 pa-sm-4 pa-xs-3">
    <!-- Header -->
    <v-row class="mb-6 mb-sm-4">
      <v-col>
        <h1 class="text-h3 text-sm-h4 text-xs-h5 font-weight-light">
          <v-icon icon="mdi-account-group" class="mr-3 mr-sm-2"></v-icon>
          Contacts
        </h1>
      </v-col>
    </v-row>

    <!-- Search Bar -->
    <v-row class="mb-4 mb-sm-3">
      <v-col cols="12">
        <v-text-field
          v-model="searchQuery"
          @input="handleSearch"
          prepend-inner-icon="mdi-magnify"
          label="Search contacts..."
          variant="outlined"
          density="comfortable"
          clearable
          @click:clear="clearSearch"
          :hint="
            searchQuery.length > 0 && searchQuery.length < 3
              ? 'Please enter at least 3 characters to search'
              : ''
          "
          persistent-hint
          class="search-field"
        ></v-text-field>
      </v-col>
    </v-row>

    <!-- Add Contact Button -->
    <v-row class="mb-4 mb-sm-3">
      <v-col>
        <v-btn
          @click="showCreateForm = true"
          color="primary"
          size="large"
          prepend-icon="mdi-plus"
          elevation="2"
          class="add-contact-btn"
          block
        >
          Add Contact
        </v-btn>
      </v-col>
    </v-row>

    <!-- Loading State -->
    <v-row v-if="loading" class="justify-center">
      <v-col cols="auto">
        <div class="text-center">
          <v-progress-circular
            indeterminate
            color="primary"
            size="64"
          ></v-progress-circular>
          <p class="mt-4 text-h6">Loading contacts...</p>
        </div>
      </v-col>
    </v-row>

    <!-- Error State -->
    <v-row v-if="error && !loading" class="justify-center">
      <v-col cols="12" md="8">
        <v-alert
          type="error"
          variant="tonal"
          prominent
          closable
          @click:close="error = null"
        >
          <template v-slot:title>Error</template>
          {{ error }}
          <template v-slot:append>
            <v-btn
              @click="loadContacts"
              color="error"
              variant="outlined"
              size="small"
            >
              Retry
            </v-btn>
          </template>
        </v-alert>
      </v-col>
    </v-row>

    <!-- Contacts Grid -->
    <v-row v-if="!loading && !error">
      <v-col v-if="contacts == null || contacts.length === 0" cols="12">
        <v-card class="text-center pa-8" variant="tonal">
          <v-icon
            icon="mdi-account-search"
            size="64"
            class="mb-4 text-grey"
          ></v-icon>
          <h3 class="text-h5 mb-2">No contacts found</h3>
          <p class="text-body-1 text-grey">
            {{
              searchQuery
                ? "Try adjusting your search query"
                : "Get started by adding your first contact"
            }}
          </p>
        </v-card>
      </v-col>

      <v-col
        v-if="contacts.length > 0"
        v-for="contact in contacts"
        :key="contact.id"
        cols="12"
        class="mb-2"
      >
        <ContactCard
          :contact="contact"
          @edit="editContact"
          @delete="deleteContactConfirm"
        />
      </v-col>
    </v-row>

    <!-- Create/Edit Form Dialog -->
    <v-dialog 
      v-model="dialogVisible" 
      max-width="600" 
      :fullscreen="$vuetify.display.xs"
      :transition="$vuetify.display.xs ? 'dialog-bottom-transition' : 'dialog-transition'"
    >
      <v-card>
        <v-card-title class="text-h5 pa-4 pa-sm-6">
          <v-icon
            :icon="editingContact ? 'mdi-account-edit' : 'mdi-account-plus'"
            class="mr-2"
          ></v-icon>
          {{ editingContact ? "Edit Contact" : "Create Contact" }}
        </v-card-title>

        <v-form @submit.prevent="saveContact" ref="form" v-model="formValid">
          <v-card-text class="pa-4 pa-sm-6">
            <v-row>
              <v-col cols="12" sm="6">
                <v-text-field
                  v-model="formData.firstName"
                  label="First Name"
                  variant="outlined"
                  :rules="[rules.required]"
                  prepend-icon="mdi-account"
                ></v-text-field>
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field
                  v-model="formData.lastName"
                  label="Last Name"
                  variant="outlined"
                  :rules="[rules.required]"
                  prepend-icon="mdi-account"
                ></v-text-field>
              </v-col>
              <v-col cols="12">
                <v-text-field
                  v-model="formData.email"
                  label="Email"
                  variant="outlined"
                  type="email"
                  :rules="[rules.required, rules.email]"
                  prepend-icon="mdi-email"
                ></v-text-field>
              </v-col>
              <v-col cols="12">
                <v-text-field
                  v-model="formData.phone"
                  label="Phone"
                  variant="outlined"
                  type="tel"
                  :rules="[rules.required]"
                  prepend-icon="mdi-phone"
                ></v-text-field>
              </v-col>
            </v-row>
          </v-card-text>

          <v-card-actions class="pa-4 pa-sm-6">
            <v-spacer></v-spacer>
            <v-btn
              @click="cancelForm"
              color="grey"
              variant="text"
              prepend-icon="mdi-close"
              class="mr-2"
            >
              Cancel
            </v-btn>
            <v-btn
              type="submit"
              color="primary"
              variant="elevated"
              :loading="saving"
              :disabled="!formValid"
              prepend-icon="mdi-content-save"
            >
              {{ saving ? "Saving..." : "Save" }}
            </v-btn>
          </v-card-actions>
        </v-form>
      </v-card>
    </v-dialog>

    <!-- Delete Confirmation Dialog -->
    <v-dialog 
      v-model="deleteDialogVisible" 
      max-width="500"
      :fullscreen="$vuetify.display.xs"
      :transition="$vuetify.display.xs ? 'dialog-bottom-transition' : 'dialog-transition'"
    >
      <v-card>
        <v-card-title class="text-h5 text-error pa-4 pa-sm-6">
          <v-icon icon="mdi-delete-alert" class="mr-2"></v-icon>
          Delete Contact
        </v-card-title>

        <v-card-text class="pa-4 pa-sm-6">
          <p class="text-body-1">
            Are you sure you want to delete
            <strong
              >{{ contactToDelete?.firstName }}
              {{ contactToDelete?.lastName }}</strong
            >?
          </p>
          <p class="text-body-2 text-grey">This action cannot be undone.</p>
        </v-card-text>

        <v-card-actions class="pa-4 pa-sm-6">
          <v-spacer></v-spacer>
          <v-btn @click="contactToDelete = null" color="grey" variant="text" class="mr-2">
            Cancel
          </v-btn>
          <v-btn
            @click="confirmDelete"
            color="error"
            variant="elevated"
            :loading="deleting"
            prepend-icon="mdi-delete"
          >
            {{ deleting ? "Deleting..." : "Delete" }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive, computed } from "vue";
import type { Contact } from "../models/contact";
import ContactCard from "./ContactCard.vue";
import {
  listContacts,
  searchContacts,
  createContact,
  updateContact,
  deleteContact,
} from "../services/contact.service";

// Reactive state
const contacts = ref<Contact[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);
const searchQuery = ref("");
const showCreateForm = ref(false);
const editingContact = ref<Contact | null>(null);
const contactToDelete = ref<Contact | null>(null);
const saving = ref(false);
const deleting = ref(false);
const formValid = ref(false);

// Form data
const formData = reactive({
  firstName: "",
  lastName: "",
  email: "",
  phone: "",
});

// Computed properties for dialog visibility
const dialogVisible = computed({
  get: () => showCreateForm.value || !!editingContact.value,
  set: (value: boolean) => {
    if (!value) {
      cancelForm();
    }
  },
});

const deleteDialogVisible = computed({
  get: () => !!contactToDelete.value,
  set: (value: boolean) => {
    if (!value) {
      contactToDelete.value = null;
    }
  },
});

// Validation rules
const rules = {
  required: (value: string) => !!value || "This field is required",
  email: (value: string) => {
    const pattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return pattern.test(value) || "Please enter a valid email address";
  },
};

// Search debounce timer
let searchTimeout: NodeJS.Timeout | null = null;

// Load contacts on component mount
onMounted(() => {
  loadContacts();
});

// Load all contacts
const loadContacts = async () => {
  try {
    loading.value = true;
    error.value = null;
    contacts.value = await listContacts();
    console.log("Contacts loaded:", contacts.value);
  } catch (err) {
    error.value = "Failed to load contacts. Please try again.";
    console.error("Error loading contacts:", err);
  } finally {
    loading.value = false;
  }
};

// Handle search with debouncing
const handleSearch = () => {
  if (searchTimeout) {
    clearTimeout(searchTimeout);
  }

  searchTimeout = setTimeout(async () => {
    const trimmedQuery = searchQuery.value.trim();

    if (trimmedQuery.length === 0) {
      // If search is empty, load all contacts
      loadContacts();
      return;
    }

    if (trimmedQuery.length < 3) {
      // If less than 3 characters, don't search but show message via hint
      return;
    }

    try {
      loading.value = true;
      error.value = null;
      contacts.value = await searchContacts(trimmedQuery);
    } catch (err) {
      error.value = "Failed to search contacts. Please try again.";
      console.error("Error searching contacts:", err);
    } finally {
      loading.value = false;
    }
  }, 500); // Increased debounce time to 500ms for better UX
};

// Clear search
const clearSearch = () => {
  searchQuery.value = "";
  loadContacts();
};

// Edit contact
const editContact = (contact: Contact) => {
  editingContact.value = contact;
  formData.firstName = contact.firstName;
  formData.lastName = contact.lastName;
  formData.email = contact.email;
  formData.phone = contact.phone;
};

// Cancel form
const cancelForm = () => {
  showCreateForm.value = false;
  editingContact.value = null;
  resetForm();
};

// Reset form data
const resetForm = () => {
  formData.firstName = "";
  formData.lastName = "";
  formData.email = "";
  formData.phone = "";
};

// Save contact (create or update)
const saveContact = async () => {
  try {
    saving.value = true;

    const contactData: Contact = {
      id: editingContact.value?.id || 0,
      firstName: formData.firstName,
      lastName: formData.lastName,
      email: formData.email,
      phone: formData.phone,
    };

    if (editingContact.value) {
      // Update existing contact
      await updateContact(editingContact.value.id, contactData);
    } else {
      // Create new contact
      await createContact(contactData);
    }

    // Refresh the contact list
    await loadContacts();

    // Close form
    cancelForm();
  } catch (err) {
    error.value = "Failed to save contact. Please try again.";
    console.error("Error saving contact:", err);
  } finally {
    saving.value = false;
  }
};

// Delete contact confirmation
const deleteContactConfirm = (contact: Contact) => {
  contactToDelete.value = contact;
};

// Confirm delete
const confirmDelete = async () => {
  if (!contactToDelete.value) return;

  try {
    deleting.value = true;
    await deleteContact(contactToDelete.value.id);

    // Refresh the contact list
    await loadContacts();

    // Close modal
    contactToDelete.value = null;
  } catch (err) {
    error.value = "Failed to delete contact. Please try again.";
    console.error("Error deleting contact:", err);
  } finally {
    deleting.value = false;
  }
};
</script>

<style scoped>
/* Custom styles for enhanced Material Design look */
.contact-list {
  min-height: 100vh;
}

/* Force normal cursor by default, pointer only on interactive elements */
* {
  cursor: default !important;
}

/* Allow pointer cursor only on buttons and interactive elements */
.v-btn,
button,
[role="button"],
.v-text-field input,
.v-text-field textarea {
  cursor: pointer !important;
}

/* Button specific styling */
.add-contact-btn {
  cursor: pointer !important;
}

/* Text and icon elements should have default cursor */
.v-icon,
.text-h3,
.text-h5,
.text-h6,
.text-body-1,
.text-body-2,
span,
p,
h1,
h2,
h3,
h4,
h5,
h6 {
  cursor: default !important;
}

/* Input fields should have text cursor */
.v-text-field input,
.v-text-field textarea {
  cursor: text !important;
}

/* Mobile-specific styles */
@media (max-width: 768px) {
  .v-container {
    padding: 16px !important;
  }
  
  .search-field {
    margin-bottom: 8px !important;
  }
  
  .add-contact-btn {
    width: 100% !important;
    max-width: none !important;
  }
  
  /* Responsive typography */
  .text-h3 {
    font-size: 1.75rem !important;
  }
  
  .text-h5 {
    font-size: 1.25rem !important;
  }
  
  /* Adjust spacing for mobile */
  .mb-6 {
    margin-bottom: 1rem !important;
  }
  
  .mb-4 {
    margin-bottom: 0.75rem !important;
  }
}

/* Extra small mobile devices */
@media (max-width: 480px) {
  .v-container {
    padding: 12px !important;
  }
  
  .text-h3 {
    font-size: 1.5rem !important;
  }
  
  .text-h5 {
    font-size: 1.125rem !important;
  }
  
  /* Make buttons more touch-friendly */
  .v-btn {
    min-height: 44px !important;
  }
  
  /* Adjust dialog padding for very small screens */
  .v-dialog .v-card-title,
  .v-dialog .v-card-text,
  .v-dialog .v-card-actions {
    padding: 16px !important;
  }
}

/* Tablet styles */
@media (max-width: 960px) and (min-width: 769px) {
  .v-container {
    padding: 24px !important;
  }
  
  .text-h3 {
    font-size: 2rem !important;
  }
}

/* Landscape mobile adjustments */
@media (max-height: 500px) and (orientation: landscape) {
  .v-container {
    padding: 8px !important;
  }
  
  .mb-6 {
    margin-bottom: 0.5rem !important;
  }
  
  .mb-4 {
    margin-bottom: 0.25rem !important;
  }
  
  .text-h3 {
    font-size: 1.5rem !important;
  }
}
</style>
