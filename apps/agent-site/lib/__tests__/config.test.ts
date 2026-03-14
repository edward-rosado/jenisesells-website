import { describe, it, expect, vi, beforeEach } from "vitest";

// Mock the config-registry module — prebuild generates this at build time
vi.mock("../config-registry", () => ({
  configs: {
    "jenise-buckalew": {
      id: "jenise-buckalew",
      identity: { name: "Jenise Buckalew", phone: "555-1234", email: "jenise@test.com", tagline: "Selling NJ!" },
      location: { state: "NJ", service_areas: ["Middlesex", "Monmouth"] },
      branding: { primary_color: "#1B5E20" },
    },
    "test-agent": {
      id: "test-agent",
      identity: { name: "Test Agent", phone: "555-0000", email: "test@test.com" },
      location: { state: "NY" },
      branding: {},
    },
    "bad-no-id": {
      identity: { name: "X", phone: "1", email: "x@x.com" },
      location: { state: "NJ" },
    },
    "bad-no-name": {
      id: "bad-no-name",
      identity: { phone: "1", email: "x@x.com" },
      location: { state: "NJ" },
    },
    "bad-no-phone": {
      id: "bad-no-phone",
      identity: { name: "X", email: "x@x.com" },
      location: { state: "NJ" },
    },
    "bad-no-email": {
      id: "bad-no-email",
      identity: { name: "X", phone: "1" },
      location: { state: "NJ" },
    },
    "bad-no-state": {
      id: "bad-no-state",
      identity: { name: "X", phone: "1", email: "x@x.com" },
      location: {},
    },
  },
  contents: {
    "jenise-buckalew": {
      template: "emerald-classic",
      sections: {
        hero: { enabled: true, data: { headline: "Sell", tagline: "Selling NJ!", cta_text: "Go", cta_link: "#" } },
        stats: { enabled: true, data: { items: [] } },
        services: { enabled: true, data: { items: [] } },
        how_it_works: { enabled: true, data: { steps: [] } },
        sold_homes: { enabled: false, data: { items: [] } },
        testimonials: { enabled: false, data: { items: [] } },
        cma_form: { enabled: true, data: { title: "CMA", subtitle: "Free" } },
        about: { enabled: true, data: { bio: "Bio", credentials: [] } },
        city_pages: { enabled: false, data: { cities: [] } },
      },
    },
  },
  legalContent: {
    "jenise-buckalew": {
      privacy: { above: "# Privacy\nCustom.", below: "## More\nExtra." },
    },
  },
  customDomains: { "jenisesellsnj.com": "jenise-buckalew" },
  agentIds: new Set(["jenise-buckalew", "test-agent", "bad-no-id", "bad-no-name", "bad-no-phone", "bad-no-email", "bad-no-state"]),
}));

import { loadAgentConfig, loadAgentContent, loadLegalContent } from "../config";

describe("loadAgentConfig", () => {
  it("loads a known agent", () => {
    const config = loadAgentConfig("jenise-buckalew");
    expect(config.id).toBe("jenise-buckalew");
    expect(config.identity.name).toBe("Jenise Buckalew");
    expect(config.location.state).toBe("NJ");
  });

  it("throws for non-existent agent", () => {
    expect(() => loadAgentConfig("nobody")).toThrow("Agent not found: nobody");
  });

  it("rejects path traversal attempts", () => {
    expect(() => loadAgentConfig("../../etc/passwd")).toThrow("Invalid agent ID");
    expect(() => loadAgentConfig("../secret")).toThrow("Invalid agent ID");
    expect(() => loadAgentConfig("foo/bar")).toThrow("Invalid agent ID");
    expect(() => loadAgentConfig("")).toThrow("Invalid agent ID");
  });

  it("throws when config is missing id", () => {
    expect(() => loadAgentConfig("bad-no-id")).toThrow("AgentConfig: missing id");
  });

  it("throws when config is missing identity.name", () => {
    expect(() => loadAgentConfig("bad-no-name")).toThrow("AgentConfig: missing identity.name");
  });

  it("throws when config is missing identity.phone", () => {
    expect(() => loadAgentConfig("bad-no-phone")).toThrow("AgentConfig: missing identity.phone");
  });

  it("throws when config is missing identity.email", () => {
    expect(() => loadAgentConfig("bad-no-email")).toThrow("AgentConfig: missing identity.email");
  });

  it("throws when config is missing location.state", () => {
    expect(() => loadAgentConfig("bad-no-state")).toThrow("AgentConfig: missing location.state");
  });

  it("rejects UPPER case agent ID", () => {
    expect(() => loadAgentConfig("UPPER")).toThrow("Invalid agent ID");
  });

  it("rejects agent ID with spaces", () => {
    expect(() => loadAgentConfig("has spaces")).toThrow("Invalid agent ID");
  });

  it("rejects agent ID with dots", () => {
    expect(() => loadAgentConfig("has.dot")).toThrow("Invalid agent ID");
  });
});

describe("loadAgentContent", () => {
  it("returns content from registry when it exists", () => {
    const content = loadAgentContent("jenise-buckalew");
    expect(content.template).toBe("emerald-classic");
    expect(content.sections.stats.enabled).toBe(true);
  });

  it("generates default content when no content in registry", () => {
    const content = loadAgentContent("test-agent");
    expect(content.template).toBe("emerald-classic");
    expect(content.sections.hero.enabled).toBe(true);
    expect(content.sections.hero.data.tagline).toBe("Your Trusted Real Estate Professional");
    expect(content.sections.services.data.items[0].description).toContain("Test Agent");
    expect(content.sections.about.data.bio).toContain("NY");
    expect(content.sections.stats.enabled).toBe(false);
  });

  it("uses provided config when generating defaults", () => {
    const config = loadAgentConfig("test-agent");
    const content = loadAgentContent("test-agent", config);
    expect(content.sections.about.data.bio).toContain("Test Agent");
  });

  it("rejects path traversal", () => {
    expect(() => loadAgentContent("../../etc/passwd")).toThrow("Invalid agent ID");
  });
});

describe("loadLegalContent", () => {
  it("returns markdown when legal content exists in registry", () => {
    const result = loadLegalContent("jenise-buckalew", "privacy");
    expect(result.above).toBe("# Privacy\nCustom.");
    expect(result.below).toBe("## More\nExtra.");
  });

  it("returns undefined when no legal content for agent", () => {
    const result = loadLegalContent("test-agent", "privacy");
    expect(result.above).toBeUndefined();
    expect(result.below).toBeUndefined();
  });

  it("returns undefined when legal page not found", () => {
    const result = loadLegalContent("jenise-buckalew", "terms");
    expect(result.above).toBeUndefined();
    expect(result.below).toBeUndefined();
  });

  it("rejects path traversal", () => {
    expect(() => loadLegalContent("../../../etc/passwd", "privacy")).toThrow("Invalid agent ID");
  });
});
