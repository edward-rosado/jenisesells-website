// apps/agent-site/features/config/config.ts
import type { AccountConfig, AgentConfig, ContentConfig } from "./types";
import { accounts, accountContent, localizedContent, accountLanguages, agentConfigs, agentContent, legalContent } from "./config-registry";

const VALID_HANDLE = /^[a-z0-9-]+$/;

function validateHandle(handle: string): void {
  if (!VALID_HANDLE.test(handle)) {
    throw new Error(`Invalid handle: ${handle}`);
  }
}

function assertAccountConfig(value: unknown): asserts value is AccountConfig {
  const v = value as Record<string, unknown>;
  if (typeof v?.handle !== "string") throw new Error("AccountConfig: missing handle");
  if (typeof v?.template !== "string") throw new Error("AccountConfig: missing template");
  const brokerage = v.brokerage as Record<string, unknown> | undefined;
  if (typeof brokerage?.name !== "string") throw new Error("AccountConfig: missing brokerage.name");
  const location = v.location as Record<string, unknown> | undefined;
  if (typeof location?.state !== "string") throw new Error("AccountConfig: missing location.state");
}

export function loadAccountConfig(handle: string): AccountConfig {
  validateHandle(handle);
  const config = accounts[handle];
  if (!config) {
    throw new Error(`Account not found: ${handle}`);
  }
  assertAccountConfig(config);
  return config;
}

export function loadAccountContent(
  handle: string,
  config?: AccountConfig,
): ContentConfig {
  validateHandle(handle);
  const content = accountContent[handle];
  if (content) return content;
  const resolved = config ?? loadAccountConfig(handle);
  return buildDefaultContent(resolved);
}

export function loadAgentConfig(handle: string, agentId: string): AgentConfig {
  validateHandle(handle);
  validateHandle(agentId);
  const agentMap = agentConfigs[handle];
  if (!agentMap || !agentMap[agentId]) {
    throw new Error(`Agent not found: ${handle}/${agentId}`);
  }
  return agentMap[agentId];
}

export function loadAgentContent(handle: string, agentId: string): ContentConfig | undefined {
  validateHandle(handle);
  validateHandle(agentId);
  return agentContent[handle]?.[agentId];
}

export function loadLegalContent(
  handle: string,
  page: "privacy" | "terms" | "accessibility",
): { above?: string; below?: string } {
  validateHandle(handle);
  return legalContent[handle]?.[page] ?? { above: undefined, below: undefined };
}

export function getAgentIds(handle: string): string[] {
  validateHandle(handle);
  return Object.keys(agentConfigs[handle] ?? {});
}

/** Load content for a specific locale, falling back to default English content */
export function loadLocalizedContent(
  handle: string,
  locale: string,
  config?: AccountConfig,
): ContentConfig {
  validateHandle(handle);
  if (locale !== "en") {
    const localized = localizedContent[handle]?.[locale];
    if (localized) return localized;
  }
  return loadAccountContent(handle, config);
}

/** Get the supported locale codes for an account (from agent config languages) */
export function getAccountLocales(handle: string): string[] {
  validateHandle(handle);
  return accountLanguages[handle] ?? ["en"];
}

function buildDefaultContent(config: AccountConfig): ContentConfig {
  const agentName = config.agent?.name ?? config.broker?.name ?? config.brokerage.name;
  const tagline = config.agent?.tagline ?? "Your Trusted Real Estate Professional";

  return {
    pages: {
      home: {
        sections: {
          hero: {
            enabled: true,
            data: {
              headline: "Sell Your Home with Confidence",
              tagline,
              cta_text: "Get Your Free Home Value",
              cta_link: "#contact_form",
            },
          },
          stats: { enabled: false, data: { items: [] } },
          features: {
            enabled: true,
            data: {
              items: [
                { title: "Expert Market Analysis", description: `${agentName} provides a detailed analysis of your local market to price your home right.` },
                { title: "Strategic Marketing Plan", description: "Professional photography, virtual tours, and targeted online advertising." },
                { title: "Negotiation & Closing", description: "Skilled negotiation to get you the best possible price and smooth closing." },
              ],
            },
          },
          steps: {
            enabled: true,
            data: {
              steps: [
                { number: 1, title: "Submit Your Info", description: "Fill out the quick form below with your property details." },
                { number: 2, title: "Get Your Free Report", description: "Receive a professional Comparative Market Analysis within minutes." },
                { number: 3, title: "Schedule a Walkthrough", description: `Meet with ${agentName} to discuss your selling strategy.` },
              ],
            },
          },
          gallery: { enabled: false, data: { items: [] } },
          testimonials: { enabled: false, data: { items: [] } },
          contact_form: {
            enabled: true,
            data: {
              title: "What's Your Home Worth?",
              subtitle: "Get a free, professional Comparative Market Analysis",
            },
          },
          about: {
            enabled: true,
            data: {
              bio: `${agentName} is a dedicated real estate professional serving ${config.location.service_areas?.join(", ") || config.location.state}. Contact ${agentName} today to learn how they can help you achieve your real estate goals.`,
              credentials: [],
            },
          },
          city_pages: { enabled: false, data: { cities: [] } },
        },
      },
    },
  };
}
