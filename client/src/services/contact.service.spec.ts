import { describe, it, expect, vi, beforeEach } from "vitest";
import axios from "axios";
import type { AxiosInstance } from "axios";
import type { Contact } from "../models/contact";
import {
  listContacts,
  searchContacts,
  createContact,
  updateContact,
  deleteContact,
} from "../services/contact.service";

vi.mock("axios");

const mockedAxios = axios as typeof axios & {
  get: ReturnType<typeof vi.fn>;
  post: ReturnType<typeof vi.fn>;
  put: ReturnType<typeof vi.fn>;
  delete: ReturnType<typeof vi.fn>;
};

vi.mocked(axios.create).mockReturnValue(mockedAxios as AxiosInstance);
vi.mock("axios", async () => {
  const actualAxios = await vi.importActual<typeof axios>("axios");
  return {
    default: {
      ...actualAxios,
      create: vi.fn(() => mockedAxios),
    },
  };
});

describe("Contact Service", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("should list contacts", async () => {
    const mockContacts: Contact[] = [
      {
        id: 1,
        firstName: "John",
        lastName: "Doe",
        email: "john.doe@example.com",
        phone: "123-456-7890",
      },
      {
        id: 2,
        firstName: "Jane",
        lastName: "Smith",
        email: "jane.smith@example.com",
        phone: "987-654-3210",
      },
    ];
    mockedAxios.get.mockResolvedValueOnce({ data: mockContacts });

    const contacts: Contact[] = await listContacts();
    expect(mockedAxios.get).toHaveBeenCalledWith("/");
    expect(contacts).toEqual(mockContacts);
  });

  it("should search contacts", async () => {
    const query = "John";
    const mockContacts: Contact[] = [
      {
        id: 1,
        firstName: "John",
        lastName: "Doe",
        email: "john.doe@example.com",
        phone: "123-456-7890",
      },
    ];
    mockedAxios.get.mockResolvedValueOnce({ data: mockContacts });

    const contacts: Contact[] = await searchContacts(query);
    expect(mockedAxios.get).toHaveBeenCalledWith("/search", {
      params: { q: query },
    });
    expect(contacts).toEqual(mockContacts);
  });

  it("should create a contact", async () => {
    const newContact: Contact = {
      id: 0,
      firstName: "Jane",
      lastName: "Doe",
      email: "jane.doe@hotmail.com",
      phone: "123-456-7890",
    };
    const createdContact: Contact = { ...newContact, id: 1 };
    mockedAxios.post.mockResolvedValueOnce({ data: createdContact });

    const result: Contact = await createContact(newContact);
    expect(mockedAxios.post).toHaveBeenCalledWith("/", newContact);
    expect(result).toEqual(createdContact);
  });

  it("should update a contact", async () => {
    const contactToUpdate: Contact = {
      id: 1,
      firstName: "John",
      lastName: "Smith",
      email: "john.smith@hotmail.com",
      phone: "987-654-3210",
    };
    mockedAxios.put.mockResolvedValueOnce({ data: contactToUpdate });

    const updatedContact: Contact = await updateContact(1, contactToUpdate);
    expect(mockedAxios.put).toHaveBeenCalledWith("/1", contactToUpdate);
    expect(updatedContact).toEqual(contactToUpdate);
  });

  it("should delete a contact", async () => {
    mockedAxios.delete.mockResolvedValueOnce({});

    await deleteContact(1);
    expect(mockedAxios.delete).toHaveBeenCalledWith("/1");
  });
});
