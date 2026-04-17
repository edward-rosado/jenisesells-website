import { describe, it, expect, vi, beforeEach } from "vitest";

const reportError = vi.fn();

vi.mock("@real-estate-star/analytics", () => ({
  reportError: (...args: unknown[]) => reportError(...args),
}));

vi.mock("../config-registry", () => ({
  accounts: {
    "live-agent": {
      handle: "live-agent",
      template: "emerald-classic",
      branding: { primary_color: "#000" },
      brokerage: { name: "Stale Brokerage", license_number: "000" },
      location: { state: "NJ", service_areas: ["Stale City"] },
    },
    "pending-agent": {
      handle: "pending-agent",
      template: "emerald-classic",
      branding: {},
      brokerage: { name: "Pre-Launch Brokerage", license_number: "001" },
      location: { state: "NJ", service_areas: [] },
    },
  },
  accountContent: {
    "pending-agent": {
      pages: { home: { sections: { hero: { enabled: true, data: { headline: "Registry Headline" } } } } },
    },
  },
  localizedContent: {},
  accountLanguages: { "live-agent": ["en"], "pending-agent": ["en"] },
  agentConfigs: {},
  agentContent: {},
  legalContent: {},
}));

const kvStore = new Map<string, string>();
const kvGet = vi.fn(async (key: string) => kvStore.get(key) ?? null);
let kvBindingAvailable = true;

vi.mock("@opennextjs/cloudflare", () => ({
  getCloudflareContext: () => {
    if (!kvBindingAvailable) return { env: {} };
    return { env: { SITE_CONTENT_KV: { get: kvGet } } };
  },
}));

import {
  getSiteState,
  loadAccountConfigAsync,
  loadLocalizedContentAsync,
} from "../kv-loader";

beforeEach(() => {
  kvStore.clear();
  kvGet.mockClear();
  reportError.mockClear();
  kvBindingAvailable = true;
});

describe("getSiteState", () => {
  it('returns "live" when KV has "live"', async () => {
    kvStore.set("site-state:v1:live-agent", '"live"');
    expect(await getSiteState("live-agent")).toBe("live");
  });

  it('returns "pending_approval" when KV has it', async () => {
    kvStore.set("site-state:v1:pending-agent", '"pending_approval"');
    expect(await getSiteState("pending-agent")).toBe("pending_approval");
  });

  it('returns "unknown" when the key is missing', async () => {
    expect(await getSiteState("nobody")).toBe("unknown");
  });

  it('returns "unknown" and reports on malformed JSON', async () => {
    kvStore.set("site-state:v1:bad", "not json{");
    expect(await getSiteState("bad")).toBe("unknown");
  });

  it('returns "unknown" when KV binding is not available', async () => {
    kvBindingAvailable = false;
    expect(await getSiteState("anyone")).toBe("unknown");
  });

  it("reports error and returns unknown when kv.get throws", async () => {
    kvGet.mockRejectedValueOnce(new Error("KV down"));
    expect(await getSiteState("live-agent")).toBe("unknown");
    expect(reportError).toHaveBeenCalledOnce();
  });
});

describe("loadAccountConfigAsync (Option B)", () => {
  it("reads from KV when site is live", async () => {
    kvStore.set("site-state:v1:live-agent", '"live"');
    kvStore.set(
      "account:v1:live-agent",
      JSON.stringify({
        handle: "live-agent",
        template: "modern-minimal",
        branding: { primary_color: "#F00" },
        brokerage: { name: "Fresh KV Brokerage", license_number: "999" },
        location: { state: "NJ", service_areas: ["Fresh City"] },
      }),
    );

    const cfg = await loadAccountConfigAsync("live-agent");
    expect(cfg.brokerage.name).toBe("Fresh KV Brokerage");
    expect(cfg.template).toBe("modern-minimal");
  });

  it("throws when live account has no account config in KV", async () => {
    kvStore.set("site-state:v1:live-agent", '"live"');
    await expect(loadAccountConfigAsync("live-agent")).rejects.toThrow(
      /no account config in KV/,
    );
  });

  it("throws when live account has malformed JSON in KV", async () => {
    kvStore.set("site-state:v1:live-agent", '"live"');
    kvStore.set("account:v1:live-agent", "}{not json");
    await expect(loadAccountConfigAsync("live-agent")).rejects.toThrow(
      /Invalid account JSON/,
    );
    expect(reportError).toHaveBeenCalled();
  });

  it("throws when KV binding is unavailable for live account", async () => {
    kvBindingAvailable = false;
    await expect(
      loadAccountConfigAsync("live-agent", "live"),
    ).rejects.toThrow(/KV binding is not available/);
  });

  it("falls back to registry for pending_approval accounts", async () => {
    kvStore.set("site-state:v1:pending-agent", '"pending_approval"');
    const cfg = await loadAccountConfigAsync("pending-agent");
    expect(cfg.brokerage.name).toBe("Pre-Launch Brokerage");
  });

  it("falls back to registry when site-state is unknown", async () => {
    const cfg = await loadAccountConfigAsync("pending-agent");
    expect(cfg.brokerage.name).toBe("Pre-Launch Brokerage");
  });

  it("does not call KV at all for non-live accounts", async () => {
    await loadAccountConfigAsync("pending-agent", "pending_approval");
    expect(kvGet).not.toHaveBeenCalled();
  });
});

describe("loadLocalizedContentAsync (Option B)", () => {
  const account = {
    handle: "live-agent",
    template: "emerald-classic",
    branding: {},
    brokerage: { name: "B", license_number: "0" },
    location: { state: "NJ", service_areas: [] },
  };

  it("reads content from KV when site is live", async () => {
    const content = {
      pages: { home: { sections: { hero: { enabled: true, data: { headline: "Live From KV" } } } } },
    };
    kvStore.set("content:v1:live-agent:en:live", JSON.stringify(content));
    const result = await loadLocalizedContentAsync("live-agent", "en", account, "live");
    expect(
      (result.pages.home.sections.hero!.data as { headline: string }).headline,
    ).toBe("Live From KV");
  });

  it("throws when live account is missing content for the requested locale", async () => {
    await expect(
      loadLocalizedContentAsync("live-agent", "es", account, "live"),
    ).rejects.toThrow(/no content for locale es in KV/);
  });

  it("falls back to registry default content for pre-launch accounts", async () => {
    const result = await loadLocalizedContentAsync(
      "pending-agent",
      "en",
      { ...account, handle: "pending-agent" },
      "pending_approval",
    );
    expect(
      (result.pages.home.sections.hero!.data as { headline: string }).headline,
    ).toBe("Registry Headline");
  });

  it("reports and throws when KV get throws for live account", async () => {
    kvGet.mockRejectedValueOnce(new Error("KV outage"));
    await expect(
      loadLocalizedContentAsync("live-agent", "en", account, "live"),
    ).rejects.toThrow(/Failed to read live content from KV/);
    expect(reportError).toHaveBeenCalled();
  });
});
