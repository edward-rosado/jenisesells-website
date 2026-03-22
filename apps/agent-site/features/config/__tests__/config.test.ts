import { describe, it, expect, vi } from "vitest";

// Mock the config-registry module — prebuild generates this at build time
vi.mock("../config-registry", () => ({
  accounts: {
    "jenise-buckalew": {
      handle: "jenise-buckalew",
      template: "emerald-classic",
      branding: { primary_color: "#1B5E20" },
      brokerage: { name: "Green Light Realty", license_number: "123" },
      agent: { enabled: true, id: "jenise-buckalew", name: "Jenise Buckalew", title: "REALTOR", phone: "555-1234", email: "jenise@test.com", tagline: "Selling NJ!" },
      location: { state: "NJ", service_areas: ["Middlesex", "Monmouth"] },
    },
    "test-handle": {
      handle: "test-handle",
      template: "modern-minimal",
      branding: {},
      brokerage: { name: "Test Realty", license_number: "000" },
      agent: { enabled: true, id: "test-handle", name: "Test Agent", title: "Agent", phone: "555-0000", email: "test@test.com" },
      location: { state: "NY", service_areas: [] },
    },
    "bad-no-handle": {
      template: "emerald-classic",
      brokerage: { name: "X", license_number: "0" },
      location: { state: "NJ", service_areas: [] },
    },
    "bad-no-template": {
      handle: "bad-no-template",
      brokerage: { name: "X", license_number: "0" },
      location: { state: "NJ", service_areas: [] },
    },
    "bad-no-brokerage": {
      handle: "bad-no-brokerage",
      template: "emerald-classic",
      location: { state: "NJ", service_areas: [] },
    },
    "bad-no-state": {
      handle: "bad-no-state",
      template: "emerald-classic",
      brokerage: { name: "X", license_number: "0" },
      location: {},
    },
    "broker-only": {
      handle: "broker-only",
      template: "emerald-classic",
      branding: {},
      brokerage: { name: "Broker Realty", license_number: "999" },
      broker: { name: "Sam Broker", title: "Managing Broker" },
      location: { state: "NJ", service_areas: [] },
    },
    "brokerage-only": {
      handle: "brokerage-only",
      template: "emerald-classic",
      branding: {},
      brokerage: { name: "Brokerage LLC", license_number: "888" },
      location: { state: "NJ", service_areas: [] },
    },
  },
  accountContent: {
    "jenise-buckalew": {
      navigation: { items: [{ label: "About", href: "#about", enabled: true }] },
      pages: {
        home: {
          sections: {
            hero: { enabled: true, data: { headline: "Sell", tagline: "Selling NJ!", cta_text: "Go", cta_link: "#" } },
            stats: { enabled: true, data: { items: [] } },
            features: { enabled: true, data: { items: [] } },
            steps: { enabled: true, data: { steps: [] } },
            gallery: { enabled: false, data: { items: [] } },
            testimonials: { enabled: false, data: { items: [] } },
            contact_form: { enabled: true, data: { title: "CMA", subtitle: "Free" } },
            about: { enabled: true, data: { bio: "Bio", credentials: [] } },
            city_pages: { enabled: false, data: { cities: [] } },
          },
        },
      },
    },
  },
  agentConfigs: {
    "jenise-buckalew": {
      "agent-a": { id: "agent-a", name: "Agent A", title: "Associate", phone: "555-1111", email: "a@test.com" },
    },
  },
  agentContent: {},
  legalContent: {
    "jenise-buckalew": {
      privacy: { above: "# Privacy\nCustom.", below: "## More\nExtra." },
    },
  },
  customDomains: { "jenisesellsnj.com": "jenise-buckalew" },
  accountHandles: new Set(["jenise-buckalew", "test-handle", "bad-no-handle", "bad-no-template", "bad-no-brokerage", "bad-no-state", "broker-only", "brokerage-only"]),
}));

import { loadAccountConfig, loadAccountContent, loadAgentConfig, loadAgentContent, loadLegalContent, getAgentIds } from "../config";

describe("loadAccountConfig", () => {
  it("loads a known account", () => {
    const config = loadAccountConfig("jenise-buckalew");
    expect(config.handle).toBe("jenise-buckalew");
    expect(config.agent?.name).toBe("Jenise Buckalew");
    expect(config.location.state).toBe("NJ");
  });

  it("throws for non-existent account", () => {
    expect(() => loadAccountConfig("nobody")).toThrow("Account not found: nobody");
  });

  it("rejects path traversal attempts", () => {
    expect(() => loadAccountConfig("../../etc/passwd")).toThrow("Invalid handle");
    expect(() => loadAccountConfig("../secret")).toThrow("Invalid handle");
    expect(() => loadAccountConfig("foo/bar")).toThrow("Invalid handle");
    expect(() => loadAccountConfig("")).toThrow("Invalid handle");
  });

  it("throws when config is missing handle", () => {
    expect(() => loadAccountConfig("bad-no-handle")).toThrow("AccountConfig: missing handle");
  });

  it("throws when config is missing template", () => {
    expect(() => loadAccountConfig("bad-no-template")).toThrow("AccountConfig: missing template");
  });

  it("throws when config is missing brokerage.name", () => {
    expect(() => loadAccountConfig("bad-no-brokerage")).toThrow("AccountConfig: missing brokerage.name");
  });

  it("throws when config is missing location.state", () => {
    expect(() => loadAccountConfig("bad-no-state")).toThrow("AccountConfig: missing location.state");
  });

  it("rejects UPPER case handle", () => {
    expect(() => loadAccountConfig("UPPER")).toThrow("Invalid handle");
  });

  it("rejects handle with spaces", () => {
    expect(() => loadAccountConfig("has spaces")).toThrow("Invalid handle");
  });

  it("rejects handle with dots", () => {
    expect(() => loadAccountConfig("has.dot")).toThrow("Invalid handle");
  });
});

describe("loadAccountContent", () => {
  it("returns content from registry when it exists", () => {
    const content = loadAccountContent("jenise-buckalew");
    expect(content.pages.home.sections.stats.enabled).toBe(true);
  });

  it("generates default content when no content in registry", () => {
    const content = loadAccountContent("test-handle");
    expect(content.pages.home.sections.hero.enabled).toBe(true);
    expect(content.pages.home.sections.hero.data.tagline).toBe("Your Trusted Real Estate Professional");
    expect(content.pages.home.sections.features.data.items[0].description).toContain("Test Agent");
    expect(content.pages.home.sections.about.data.bio).toContain("NY");
    expect(content.pages.home.sections.stats.enabled).toBe(false);
  });

  it("uses provided config when generating defaults", () => {
    const config = loadAccountConfig("test-handle");
    const content = loadAccountContent("test-handle", config);
    expect(content.pages.home.sections.about.data.bio).toContain("Test Agent");
  });

  it("falls back to broker name in default content when no agent", () => {
    const content = loadAccountContent("broker-only");
    expect(content.pages.home.sections.features.data.items[0].description).toContain("Sam Broker");
    expect(content.pages.home.sections.about.data.bio).toContain("Sam Broker");
  });

  it("falls back to brokerage name in default content when no agent or broker", () => {
    const content = loadAccountContent("brokerage-only");
    expect(content.pages.home.sections.features.data.items[0].description).toContain("Brokerage LLC");
    expect(content.pages.home.sections.about.data.bio).toContain("Brokerage LLC");
  });

  it("uses default tagline when agent has no tagline", () => {
    const content = loadAccountContent("broker-only");
    expect(content.pages.home.sections.hero.data.tagline).toBe("Your Trusted Real Estate Professional");
  });

  it("rejects path traversal", () => {
    expect(() => loadAccountContent("../../etc/passwd")).toThrow("Invalid handle");
  });
});

describe("loadAgentConfig", () => {
  it("loads a known agent from account", () => {
    const config = loadAgentConfig("jenise-buckalew", "agent-a");
    expect(config.id).toBe("agent-a");
    expect(config.name).toBe("Agent A");
  });

  it("throws for non-existent agent", () => {
    expect(() => loadAgentConfig("jenise-buckalew", "nonexistent")).toThrow("Agent not found: jenise-buckalew/nonexistent");
  });

  it("rejects path traversal on handle", () => {
    expect(() => loadAgentConfig("../secret", "agent-a")).toThrow("Invalid handle");
  });

  it("rejects path traversal on agent ID", () => {
    expect(() => loadAgentConfig("jenise-buckalew", "../secret")).toThrow("Invalid handle");
  });
});

describe("loadAgentContent", () => {
  it("returns undefined when no agent content exists", () => {
    const content = loadAgentContent("jenise-buckalew", "agent-a");
    expect(content).toBeUndefined();
  });
});

describe("getAgentIds", () => {
  it("returns agent IDs for an account with agents", () => {
    const ids = getAgentIds("jenise-buckalew");
    expect(ids).toContain("agent-a");
  });

  it("returns empty array for account without agents", () => {
    const ids = getAgentIds("test-handle");
    expect(ids).toEqual([]);
  });
});

describe("loadLegalContent", () => {
  it("returns markdown when legal content exists in registry", () => {
    const result = loadLegalContent("jenise-buckalew", "privacy");
    expect(result.above).toBe("# Privacy\nCustom.");
    expect(result.below).toBe("## More\nExtra.");
  });

  it("returns undefined when no legal content for account", () => {
    const result = loadLegalContent("test-handle", "privacy");
    expect(result.above).toBeUndefined();
    expect(result.below).toBeUndefined();
  });

  it("returns undefined when legal page not found", () => {
    const result = loadLegalContent("jenise-buckalew", "terms");
    expect(result.above).toBeUndefined();
    expect(result.below).toBeUndefined();
  });

  it("rejects path traversal", () => {
    expect(() => loadLegalContent("../../../etc/passwd", "privacy")).toThrow("Invalid handle");
  });
});
