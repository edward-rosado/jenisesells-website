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
});

describe("loadAgentContent", () => {
  it("should return default content when no content file exists", async () => {
    const content = await loadAgentContent("jenise-buckalew");
    expect(content).toBeDefined();
    expect(content.template).toBe("emerald-classic");
    expect(content.sections.hero.enabled).toBe(true);
    expect(content.sections.cma_form.enabled).toBe(true);
  });
});
