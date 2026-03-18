#!/usr/bin/env node
// One-time migration: config/agents/ -> config/accounts/
import fs from "fs";
import path from "path";

const AGENTS_DIR = path.resolve("config/agents");
const ACCOUNTS_DIR = path.resolve("config/accounts");

const SECTION_RENAMES = {
  services: "features",
  how_it_works: "steps",
  sold_homes: "gallery",
  cma_form: "contact_form",
};

const NAV_SECTION_RENAMES = {
  services: "#features",
  "how-it-works": "#steps",
  sold: "#gallery",
  "cma-form": "#contact_form",
  testimonials: "#testimonials",
  about: "#about",
  hero: "#hero",
};

function loadJson(p) { return JSON.parse(fs.readFileSync(p, "utf-8")); }
function writeJson(p, data) {
  fs.mkdirSync(path.dirname(p), { recursive: true });
  fs.writeFileSync(p, JSON.stringify(data, null, 2) + "\n", "utf-8");
}

function migrateConfig(config) {
  const id = config.id;
  const identity = config.identity || {};
  return {
    handle: id,
    template: "emerald-classic",
    branding: config.branding || {},
    brokerage: {
      name: identity.brokerage || "Unknown Brokerage",
      license_number: identity.brokerage_id || "",
      ...(identity.office_phone && { office_phone: identity.office_phone }),
      ...(config.location?.office_address && { office_address: config.location.office_address }),
    },
    agent: {
      enabled: true,
      id,
      name: identity.name,
      title: identity.title || "Real Estate Agent",
      phone: identity.phone,
      email: identity.email,
      ...(identity.headshot_url && { headshot_url: identity.headshot_url }),
      ...(identity.license_id && { license_number: identity.license_id }),
      ...(identity.languages && { languages: identity.languages }),
      ...(identity.tagline && { tagline: identity.tagline }),
    },
    location: {
      state: config.location?.state || "US",
      service_areas: config.location?.service_areas || [],
    },
    ...(config.integrations && { integrations: config.integrations }),
    ...(config.compliance && { compliance: config.compliance }),
  };
}

function migrateContent(content, account) {
  if (content.template) {
    account.template = content.template;
  }

  if (content.contact_info) {
    account.contact_info = content.contact_info;
  }

  const oldSections = content.sections || {};
  const newSections = {};
  for (const [key, val] of Object.entries(oldSections)) {
    const newKey = SECTION_RENAMES[key] || key;
    newSections[newKey] = val;
  }

  const navItems = (content.navigation?.items || []).map(item => ({
    label: item.label,
    href: NAV_SECTION_RENAMES[item.section] || `#${item.section}`,
    enabled: true,
  }));

  return {
    ...(navItems.length > 0 && { navigation: { items: navItems } }),
    pages: {
      home: { sections: newSections },
      ...(content.pages?.thank_you && { thank_you: content.pages.thank_you }),
    },
  };
}

function main() {
  if (!fs.existsSync(AGENTS_DIR)) {
    console.error("config/agents/ not found");
    process.exit(1);
  }

  fs.mkdirSync(ACCOUNTS_DIR, { recursive: true });

  for (const entry of fs.readdirSync(AGENTS_DIR, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const agentDir = path.join(AGENTS_DIR, entry.name);
    const configPath = path.join(agentDir, "config.json");
    if (!fs.existsSync(configPath)) continue;

    console.log(`Migrating ${entry.name}...`);
    const config = loadJson(configPath);
    const account = migrateConfig(config);

    const contentPath = path.join(agentDir, "content.json");
    let newContent = null;
    if (fs.existsSync(contentPath)) {
      const content = loadJson(contentPath);
      newContent = migrateContent(content, account);
    }

    const accountDir = path.join(ACCOUNTS_DIR, entry.name);
    writeJson(path.join(accountDir, "account.json"), account);
    if (newContent) {
      writeJson(path.join(accountDir, "content.json"), newContent);
    }

    const legalDir = path.join(agentDir, "legal");
    if (fs.existsSync(legalDir)) {
      const destLegal = path.join(accountDir, "legal");
      fs.mkdirSync(destLegal, { recursive: true });
      for (const file of fs.readdirSync(legalDir)) {
        fs.copyFileSync(path.join(legalDir, file), path.join(destLegal, file));
      }
    }
  }

  console.log("Migration complete. Review config/accounts/ then delete config/agents/.");
}

main();
