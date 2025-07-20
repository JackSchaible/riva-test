const apiUrl = import.meta.env.VITE_API_URL;

if (!apiUrl) {
  throw new Error("API_URL is not defined in environment variables");
}

export const config = {
  apiUrl,
};
