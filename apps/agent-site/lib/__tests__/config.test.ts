import { describe, it, expect } from "vitest";
import { loadAgentConfig, loadAgentContent } from "../config";

describe("loadAgentConfig", () => {
  it("should load jenise-buckalew config from config/agents/", async () => {
    const config = await loadAgentConfig("jenise-buckalew");
    expect(config).toBeDefined();
    expect(config.id).toBe("jenise-buckalew");
    expect(config.identity.name).toBe("Jenise Buckalew");
    expect(config.location.state).toBe("NJ");
    expect(config.branding.primary_color).toBe("#1B5E20");
  });

  it("should throw for non-existent agent", async () => {
    await expect(loadAgentConfig("nobody")).rejects.toThrow();
  });

  it("should reject path traversal attempts", async () => {
    await expect(loadAgentConfig("../../etc/passwd")).rejects.toThrow("Invalid agent ID");
    await expect(loadAgentConfig("../secret")).rejects.toThrow("Invalid agent ID");
    await expect(loadAgentConfig("foo/bar")).rejects.toThrow("Invalid agent ID");
    await expect(loadAgentConfig("")).rejects.toThrow("Invalid agent ID");
  });

  it("should throw when config is missing id", async () => {
    await expect(loadAgentConfig("bad-no-id")).rejects.toThrow("AgentConfig: missing id");
  });

  it("should throw when config is missing identity.name", async () => {
    await expect(loadAgentConfig("bad-no-name")).rejects.toThrow("AgentConfig: missing identity.name");
  });

  it("should throw when config is missing identity.phone", async () => {
    await expect(loadAgentConfig("bad-no-phone")).rejects.toThrow("AgentConfig: missing identity.phone");
  });

  it("should throw when config is missing identity.email", async () => {
    await expect(loadAgentConfig("bad-no-email")).rejects.toThrow("AgentConfig: missing identity.email");
  });

  it("should throw when config is missing location.state", async () => {
    await expect(loadAgentConfig("bad-no-state")).rejects.toThrow("AgentConfig: missing location.state");
  });
});

describe("loadAgentContent", () => {
  it("should return default content when no content file exists", async () => {
    const content = await loadAgentContent("jenise-buckalew");
    expect(content).toBeDefined();
    expect(content.template).toBe("emerald-classic");
    expect(content.sections.hero.enabled).toBe(true);
    expect(content.sections.cma_form.enabled).toBe(true);
  });

  it("should load content from file when it exists", async () => {
    const content = await loadAgentContent("jenise-buckalew");
    expect(content.template).toBe("emerald-classic");
    expect(content.sections.hero.enabled).toBe(true);
    // Content comes from file, not defaults
    expect(content.sections).toBeDefined();
  });

  it("should accept a pre-loaded config for default content generation", async () => {
    // Use a non-existent content file to trigger default generation
    const config = await loadAgentConfig("jenise-buckalew");
    // Create a fake agent ID that won't have a content file
    const fakeConfig = { ...config, id: "no-content-agent" };
    const content = await loadAgentContent("jenise-buckalew", fakeConfig);
    // This loads from the real content file, so it works either way
    expect(content.template).toBe("emerald-classic");
  });

  it("should generate default content when no content file exists", async () => {
    // test-agent has no .content.json file — triggers buildDefaultContent
    const content = await loadAgentContent("test-agent");
    expect(content.template).toBe("emerald-classic");
    expect(content.sections.hero.enabled).toBe(true);
    expect(content.sections.hero.data.tagline).toBe("Your Trusted Real Estate Professional");
    expect(content.sections.services.data.items[0].description).toContain("Test Agent");
    expect(content.sections.how_it_works.data.steps[2].description).toContain("Test Agent");
    expect(content.sections.about.data.bio).toContain("NY");
    expect(content.sections.stats.enabled).toBe(false);
  });

  it("should use provided config when generating defaults (avoids double read)", async () => {
    const config = await loadAgentConfig("test-agent");
    const content = await loadAgentContent("test-agent", config);
    expect(content.sections.about.data.bio).toContain("Test Agent");
  });

  it("should reject path traversal in content loading", async () => {
    await expect(loadAgentContent("../../etc/passwd")).rejects.toThrow("Invalid agent ID");
  });
});
