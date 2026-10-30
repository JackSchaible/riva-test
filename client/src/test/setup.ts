// jsdom lacks ResizeObserver, which several Vuetify components use.
globalThis.ResizeObserver = class {
  observe(): void {
    // no-op
  }
  unobserve(): void {
    // no-op
  }
  disconnect(): void {
    // no-op
  }
};
