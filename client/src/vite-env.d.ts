/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Overrides the API base URL. Defaults to same-origin "/api" (proxied by Vite in development). */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

declare module "*.vue" {
  import type { DefineComponent } from "vue";
  const component: DefineComponent;
  export default component;
}

declare module "vuetify/styles";
