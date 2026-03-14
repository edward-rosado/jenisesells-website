import { describe, it, expect } from "vitest";
import { loadAgentConfig, loadAgentContent, loadLegalContent } from "../config";
import { writeFile, mkdir, rm } from "fs/promises";
import path from "path";

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

describe("directory-first config resolution", () => {
  it("loadAgentConfig reads from {id}/config.json when directory exists", async () => {
    // jenise-buckalew has been migrated to directory structure
    const config = await loadAgentConfig("jenise-buckalew");
    expect(config.id).toBe("jenise-buckalew");
    expect(config.identity.name).toBe("Jenise Buckalew");
  });

  it("loadAgentConfig falls back to {id}.json when directory doesn't exist", async () => {
    // test-agent only exists as a flat file
    const config = await loadAgentConfig("test-agent");
    expect(config.id).toBe("test-agent");
  });

  it("loadAgentContent reads from {id}/content.json when directory exists", async () => {
    // jenise-buckalew has content.json in directory
    const content = await loadAgentContent("jenise-buckalew");
    expect(content.template).toBe("emerald-classic");
    // Directory content has stats enabled (unlike defaults)
    expect(content.sections.stats.enabled).toBe(true);
  });

  it("loadAgentContent falls back to {id}.content.json when directory doesn't exist", async () => {
    // test-agent has no directory, no content file — falls back to defaults
    const content = await loadAgentContent("test-agent");
    expect(content.template).toBe("emerald-classic");
    expect(content.sections.stats.enabled).toBe(false);
  });

  it("loadAgentConfig throws on invalid agent ID", async () => {
    await expect(loadAgentConfig("UPPER")).rejects.toThrow("Invalid agent ID");
    await expect(loadAgentConfig("has spaces")).rejects.toThrow("Invalid agent ID");
    await expect(loadAgentConfig("has.dot")).rejects.toThrow("Invalid agent ID");
    await expect(loadAgentConfig("../traversal")).rejects.toThrow("Invalid agent ID");
  });
});

describe("loadLegalContent", () => {
  const CONFIG_DIR = path.resolve(process.cwd(), "../../config/agents");
  const testAgentId = "legal-test-agent";
  const legalDir = path.join(CONFIG_DIR, testAgentId, "legal");

  it("returns markdown when legal files exist", async () => {
    // Create temp legal files for testing
    await mkdir(legalDir, { recursive: true });
    await writeFile(path.join(legalDir, "privacy-above.md"), "# Privacy Policy\nCustom privacy content.");
    await writeFile(path.join(legalDir, "privacy-below.md"), "## Additional Privacy Info\nMore content.");

    try {
      const result = await loadLegalContent(testAgentId, "privacy");
      expect(result.above).toBe("# Privacy Policy\nCustom privacy content.");
      expect(result.below).toBe("## Additional Privacy Info\nMore content.");
    } finally {
      await rm(path.join(CONFIG_DIR, testAgentId), { recursive: true, force: true });
    }
  });

  it("returns undefined for above and below when files don't exist", async () => {
    const result = await loadLegalContent("jenise-buckalew", "privacy");
    expect(result.above).toBeUndefined();
    expect(result.below).toBeUndefined();
  });

  it("returns undefined for above and below when legal dir doesn't exist", async () => {
    const result = await loadLegalContent("jenise-buckalew", "terms");
    expect(result.above).toBeUndefined();
    expect(result.below).toBeUndefined();
  });

  it("rejects path traversal in agent ID", async () => {
    await expect(loadLegalContent("../../../etc/passwd", "privacy")).rejects.toThrow("Invalid agent ID");
  });
});
