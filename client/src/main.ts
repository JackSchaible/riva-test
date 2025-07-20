import { createApp } from "vue";
import { createVuetify } from "vuetify";
import App from "./App.vue";
import "./style.css";
import "vuetify/styles";
import "@mdi/font/css/materialdesignicons.css";
import * as components from "vuetify/components";
import * as directives from "vuetify/directives";

const vuetify = createVuetify({
  components,
  directives,
  theme: {
    defaultTheme: "light",
  },
});

createApp(App).use(vuetify).mount("#app");
