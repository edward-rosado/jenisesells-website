import { readFile } from "fs/promises";
import path from "path";
import type { AgentConfig, AgentContent } from "./types";

const CONFIG_DIR = path.resolve(process.cwd(), "../../config/agents");

/** Matches the `id` pattern in agent.schema.json — prevents path traversal */
const VALID_AGENT_ID = /^[a-z0-9-]+$/;

function validateAgentId(agentId: string): void {
  if (!VALID_AGENT_ID.test(agentId)) {
    throw new Error(`Invalid agent ID: ${agentId}`);
  }
}

export async function loadAgentConfig(agentId: string): Promise<AgentConfig> {
  validateAgentId(agentId);
  const filePath = path.join(CONFIG_DIR, `${agentId}.json`);
  const raw = await readFile(filePath, "utf-8");
  return JSON.parse(raw) as AgentConfig;
}

export async function loadAgentContent(
  agentId: string,
  config?: AgentConfig,
): Promise<AgentContent> {
  validateAgentId(agentId);
  const filePath = path.join(CONFIG_DIR, `${agentId}.content.json`);
  try {
    const raw = await readFile(filePath, "utf-8");
    return JSON.parse(raw) as AgentContent;
  } catch {
    const resolved = config ?? await loadAgentConfig(agentId);
    return buildDefaultContent(resolved);
  }
}

function buildDefaultContent(config: AgentConfig): AgentContent {
  const name = config.identity.name;
  const tagline = config.identity.tagline || "Your Trusted Real Estate Professional";

  return {
    template: "emerald-classic",
    sections: {
      hero: {
        enabled: true,
        data: {
          headline: "Sell Your Home with Confidence",
          tagline,
          cta_text: "Get Your Free Home Value",
          cta_link: "#cma-form",
        },
      },
      stats: { enabled: false, data: { items: [] } },
      services: {
        enabled: true,
        data: {
          items: [
            { title: "Expert Market Analysis", description: `${name} provides a detailed analysis of your local market to price your home right.` },
            { title: "Strategic Marketing Plan", description: "Professional photography, virtual tours, and targeted online advertising." },
            { title: "Negotiation & Closing", description: "Skilled negotiation to get you the best possible price and smooth closing." },
          ],
        },
      },
      how_it_works: {
        enabled: true,
        data: {
          steps: [
            { number: 1, title: "Submit Your Info", description: "Fill out the quick form below with your property details." },
            { number: 2, title: "Get Your Free Report", description: "Receive a professional Comparative Market Analysis within minutes." },
            { number: 3, title: "Schedule a Walkthrough", description: `Meet with ${name} to discuss your selling strategy.` },
          ],
        },
      },
      sold_homes: { enabled: false, data: { items: [] } },
      testimonials: { enabled: false, data: { items: [] } },
      cma_form: {
        enabled: true,
        data: {
          title: "What's Your Home Worth?",
          subtitle: "Get a free, professional Comparative Market Analysis",
        },
      },
      about: {
        enabled: true,
        data: {
          bio: `${name} is a dedicated real estate professional serving ${config.location.service_areas?.join(", ") || config.location.state}. Contact ${name} today to learn how they can help you achieve your real estate goals.`,
          credentials: [],
        },
      },
      city_pages: { enabled: false, data: { cities: [] } },
    },
  };
}
