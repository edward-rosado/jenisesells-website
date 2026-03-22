import { describe, it, expect, vi } from "vitest";

vi.mock("../config-registry", () => ({
  customDomains: {
    "jenisesellsnj.com": "jenise-buckalew",
    "example-agent.com": "example-agent",
  },
  agentIds: new Set(["jenise-buckalew", "test-agent", "example-agent"]),
}));

import { extractAgentId, resolveAgentFromCustomDomain, isWwwCustomDomain, getAgentIds } from "../routing";

describe("extractAgentId", () => {
  it("extracts agent-id from real-estate-star.com subdomain", () => {
    expect(extractAgentId("jenise-buckalew.real-estate-star.com")).toBe("jenise-buckalew");
  });

  it("returns null for bare domain", () => {
    expect(extractAgentId("real-estate-star.com")).toBeNull();
  });

  it("returns null for www subdomain", () => {
    expect(extractAgentId("www.real-estate-star.com")).toBeNull();
  });

  it("returns null for platform subdomain", () => {
    expect(extractAgentId("platform.real-estate-star.com")).toBeNull();
  });

  it("returns null for api subdomain", () => {
    expect(extractAgentId("api.real-estate-star.com")).toBeNull();
  });

  it("returns null for portal subdomain", () => {
    expect(extractAgentId("portal.real-estate-star.com")).toBeNull();
  });

  it("returns null for app subdomain", () => {
    expect(extractAgentId("app.real-estate-star.com")).toBeNull();
  });

  it("returns null for admin subdomain", () => {
    expect(extractAgentId("admin.real-estate-star.com")).toBeNull();
  });

  it("returns null for nested subdomains", () => {
    expect(extractAgentId("a.b.real-estate-star.com")).toBeNull();
  });

  it("handles localhost with port for dev", () => {
    expect(extractAgentId("jenise-buckalew.localhost:3000")).toBe("jenise-buckalew");
  });

  it("returns null for plain localhost", () => {
    expect(extractAgentId("localhost:3000")).toBeNull();
  });

  it("strips port from hostname", () => {
    expect(extractAgentId("jenise-buckalew.real-estate-star.com:443")).toBe("jenise-buckalew");
  });
});

describe("resolveAgentFromCustomDomain", () => {
  it("returns agent ID for known custom domain", () => {
    expect(resolveAgentFromCustomDomain("jenisesellsnj.com")).toBe("jenise-buckalew");
  });

  it("returns null for unknown domain", () => {
    expect(resolveAgentFromCustomDomain("random.com")).toBeNull();
  });

  it("strips port before lookup", () => {
    expect(resolveAgentFromCustomDomain("jenisesellsnj.com:443")).toBe("jenise-buckalew");
  });
});

describe("isWwwCustomDomain", () => {
  it("returns bare domain for www.customdomain", () => {
    expect(isWwwCustomDomain("www.jenisesellsnj.com")).toBe("jenisesellsnj.com");
  });

  it("returns null for non-www hostname", () => {
    expect(isWwwCustomDomain("jenisesellsnj.com")).toBeNull();
  });

  it("returns null for www of unknown domain", () => {
    expect(isWwwCustomDomain("www.random.com")).toBeNull();
  });

  it("strips port before checking", () => {
    expect(isWwwCustomDomain("www.jenisesellsnj.com:443")).toBe("jenisesellsnj.com");
  });
});

describe("getAgentIds", () => {
  it("returns a Set of known agent IDs", () => {
    const ids = getAgentIds();
    expect(ids.has("jenise-buckalew")).toBe(true);
    expect(ids.has("test-agent")).toBe(true);
    expect(ids.has("nobody")).toBe(false);
  });
});

describe("extractAgentId — non-base-domain fallback", () => {
  it("returns null for a hostname that is not related to any base domain", () => {
    // Hostname like "random.com" does not match localhost or real-estate-star.com
    expect(extractAgentId("random.com")).toBeNull();
  });

  it("returns null for a custom domain hostname (not a base domain subdomain)", () => {
    expect(extractAgentId("jenisesellsnj.com")).toBeNull();
  });
});
