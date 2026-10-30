/** Settings for the temporary V2 demo. Nothing here is secret. */
export const demoConfig = {
  account: "657164771317",
  region: "ca-central-1",

  // The existing jackschaible.ca zone. The stacks only add (and on destroy remove) their own records.
  hostedZoneId: "Z1MRUAGRU2DG0A",
  zoneName: "jackschaible.ca",
  siteDomain: "riva-test.jackschaible.ca",
  apiDomain: "riva-api.jackschaible.ca",

  // Three days after launch. Shown on every resource as a tag; the operator destroys the stacks by then.
  teardownBy: "2026-11-02",
} as const;

export type DemoConfig = typeof demoConfig;

export const siteOrigin = (config: DemoConfig) => `https://${config.siteDomain}`;
export const apiBaseUrl = (config: DemoConfig) => `https://${config.apiDomain}/api`;
