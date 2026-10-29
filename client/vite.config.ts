import { defineConfig } from "vitest/config";
import vue from "@vitejs/plugin-vue";
import vuetify from "vite-plugin-vuetify";

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), vuetify({ autoImport: true })],
  server: {
    // The frontend calls same-origin /api; in development Vite forwards it to the ASP.NET API.
    proxy: {
      "/api": process.env.API_PROXY_TARGET ?? "http://localhost:5059",
    },
  },
  test: {
    environment: "jsdom",
    include: ["src/**/*.spec.ts"],
    setupFiles: ["./src/test/setup.ts"],
    server: {
      deps: {
        inline: ["vuetify"],
      },
    },
  },
});
