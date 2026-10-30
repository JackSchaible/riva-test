import { createApp } from "vue";
import { createVuetify } from "vuetify";
import App from "./App.vue";
import "vuetify/styles";
import "@mdi/font/css/materialdesignicons.css";

// Components and directives are registered on demand by vite-plugin-vuetify.
const vuetify = createVuetify({
  theme: {
    defaultTheme: "light",
  },
});

createApp(App).use(vuetify).mount("#app");
