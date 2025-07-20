import axios from "axios";
import type { Contact } from "../models/contact";
import { config } from "../config";
import type { ApiResponse } from "../models/api.response";

const api = axios.create({
  baseURL: `${config.apiUrl}`,
  headers: {
    "Content-Type": "application/json",
  },
});

export const listContacts = async (): Promise<Contact[]> => {
  const response = await api.get<ApiResponse<Contact[]>>("/");
  if (!response.data.success) {
    throw new Error(response.data.message || "Failed to fetch contacts");
  }
  return response.data.data || [];
};

export const searchContacts = async (query: string): Promise<Contact[]> => {
  const response = await api.get<ApiResponse<Contact[]>>(`/search`, {
    params: { query },
  });
  if (!response.data.success) {
    throw new Error(response.data.message || "Failed to search contacts");
  }
  return response.data.data || [];
};

export const createContact = async (contact: Contact): Promise<Contact> => {
  const response = await api.post<ApiResponse<Contact>>("/", contact);
  if (!response.data.success) {
    const errorMessage =
      response.data.errors?.join(", ") ||
      response.data.message ||
      "Failed to create contact";
    throw new Error(errorMessage);
  }
  return response.data.data!;
};

export const updateContact = async (
  id: number,
  contact: Contact
): Promise<Contact> => {
  const response = await api.put<ApiResponse<Contact>>(`/${id}`, contact);
  if (!response.data.success) {
    const errorMessage =
      response.data.errors?.join(", ") ||
      response.data.message ||
      "Failed to update contact";
    throw new Error(errorMessage);
  }
  return response.data.data!;
};

export const deleteContact = async (id: number): Promise<void> => {
  const response = await api.delete<ApiResponse<null>>(`/${id}`);
  if (!response.data.success) {
    throw new Error(response.data.message || "Failed to delete contact");
  }
};
