import { describe, it, expect } from "vitest";
import type { AgentConfig, AgentContent, SectionConfig } from "../types";

describe("AgentConfig type", () => {
  it("should accept a valid agent config", () => {
    const config: AgentConfig = {
      id: "jenise-buckalew",
      identity: {
        name: "Jenise Buckalew",
        email: "jenisesellsnj@gmail.com",
        phone: "(347) 393-5993",
      },
      location: {
        state: "NJ",
      },
      branding: {},
    };
    expect(config.id).toBe("jenise-buckalew");
  });
});

describe("AgentContent type", () => {
  it("should accept a valid content config with enabled/disabled sections", () => {
    const content: AgentContent = {
      template: "emerald-classic",
      sections: {
        hero: {
          enabled: true,
          data: {
            headline: "Sell Your Home with Confidence",
            tagline: "Forward. Moving.",
            cta_text: "Get Your Free Home Value",
            cta_link: "#cma-form",
          },
        },
        stats: { enabled: false, data: { items: [] } },
        services: { enabled: false, data: { items: [] } },
        how_it_works: { enabled: false, data: { steps: [] } },
        sold_homes: { enabled: false, data: { items: [] } },
        testimonials: { enabled: false, data: { items: [] } },
        cma_form: { enabled: false, data: { title: "", subtitle: "" } },
        about: { enabled: false, data: { bio: "" } },
        city_pages: { enabled: false, data: { cities: [] } },
      },
    };
    expect(content.sections.hero.enabled).toBe(true);
    expect(content.sections.stats.enabled).toBe(false);
  });
});
