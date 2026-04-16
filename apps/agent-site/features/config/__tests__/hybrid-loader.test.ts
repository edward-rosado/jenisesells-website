import { describe, it, expect, vi, beforeEach } from "vitest";

// ---------------------------------------------------------------------------
// Mock @opennextjs/cloudflare so tests never hit a real Cloudflare context
// ---------------------------------------------------------------------------
vi.mock("@opennextjs/cloudflare", () => ({
  getCloudflareContext: vi.fn(),
}));

// Mock config to isolate bundled fallback path
vi.mock("../config", () => ({
  loadLocalizedContent: vi.fn(),
}));

import { getCloudflareContext } from "@opennextjs/cloudflare";
import { loadLocalizedContent } from "../config";
import { loadContent } from "../hybrid-loader";
import type { ContentConfig } from "../types";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const mockGetCtx = vi.mocked(getCloudflareContext);
const mockLoadLocalizedContent = vi.mocked(loadLocalizedContent);

function makeKvStub(data: Record<string, string | null>): KVNamespace {
  return {
    get: vi.fn(async (key: string) => data[key] ?? null),
    put: vi.fn(),
    delete: vi.fn(),
    list: vi.fn(),
    getWithMetadata: vi.fn(),
  } as unknown as KVNamespace;
}

function makeContentConfig(headline = "Test Headline"): ContentConfig {
  return {
    pages: {
      home: {
        sections: {
          hero: { enabled: true, data: { headline, tagline: "t", cta_text: "Go", cta_link: "#" } },
          stats: { enabled: false, data: { items: [] } },
          features: { enabled: false, data: { items: [] } },
          steps: { enabled: false, data: { steps: [] } },
          gallery: { enabled: false, data: { items: [] } },
          testimonials: { enabled: false, data: { items: [] } },
          contact_form: { enabled: false, data: { title: "", subtitle: "" } },
          about: { enabled: false, data: { bio: "", credentials: [] } },
          city_pages: { enabled: false, data: { cities: [] } },
        },
      },
    },
  };
}

const BUNDLED_CONTENT = makeContentConfig("Bundled Headline");
const KV_CONTENT = makeContentConfig("KV Headline");

function configureKvBinding(kv: KVNamespace | null) {
  if (kv === null) {
    mockGetCtx.mockResolvedValue({ env: {}, cf: undefined, ctx: {} as ExecutionContext });
  } else {
    mockGetCtx.mockResolvedValue({
      env: { CONTENT_KV: kv } as unknown as CloudflareEnv,
      cf: undefined,
      ctx: {} as ExecutionContext,
    });
  }
}

function configureKvError() {
  mockGetCtx.mockRejectedValue(new Error("context unavailable"));
}

beforeEach(() => {
  vi.clearAllMocks();
  mockLoadLocalizedContent.mockReturnValue(BUNDLED_CONTENT);
});

// ---------------------------------------------------------------------------
// production — KV hit
// ---------------------------------------------------------------------------

describe("production env — KV hit", () => {
  it("returns content from KV with source='kv' when live key exists", async () => {
    const kv = makeKvStub({
      "content:v1:jenise-buckalew:en:live": JSON.stringify(KV_CONTENT),
    });
    configureKvBinding(kv);

    const result = await loadContent("jenise-buckalew", "en", "production");

    expect(result.source).toBe("kv");
    expect(result.content?.pages.home.sections.hero.data.headline).toBe("KV Headline");
  });

  it("uses locale in KV key", async () => {
    const kv = makeKvStub({
      "content:v1:test-agent:es:live": JSON.stringify(KV_CONTENT),
    });
    configureKvBinding(kv);

    const result = await loadContent("test-agent", "es", "production");

    expect(result.source).toBe("kv");
    expect(kv.get).toHaveBeenCalledWith("content:v1:test-agent:es:live");
  });
});

// ---------------------------------------------------------------------------
// production — KV miss → bundled fallback
// ---------------------------------------------------------------------------

describe("production env — KV miss", () => {
  it("falls back to bundled when live key is not in KV", async () => {
    const kv = makeKvStub({}); // everything returns null
    configureKvBinding(kv);

    const result = await loadContent("jenise-buckalew", "en", "production");

    expect(result.source).toBe("bundled");
    expect(result.content?.pages.home.sections.hero.data.headline).toBe("Bundled Headline");
    expect(mockLoadLocalizedContent).toHaveBeenCalledWith("jenise-buckalew", "en");
  });

  it("returns null content when both KV and bundled miss", async () => {
    const kv = makeKvStub({});
    configureKvBinding(kv);
    mockLoadLocalizedContent.mockImplementation(() => {
      throw new Error("not found");
    });

    const result = await loadContent("unknown-agent", "en", "production");

    expect(result.source).toBe("bundled");
    expect(result.content).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// preview env — reads :draft key
// ---------------------------------------------------------------------------

describe("preview env — reads draft key", () => {
  it("uses :draft variant when env='preview'", async () => {
    const kv = makeKvStub({
      "content:v1:jenise-buckalew:en:draft": JSON.stringify(KV_CONTENT),
    });
    configureKvBinding(kv);

    const result = await loadContent("jenise-buckalew", "en", "preview");

    expect(result.source).toBe("kv");
    expect(kv.get).toHaveBeenCalledWith("content:v1:jenise-buckalew:en:draft");
    expect(kv.get).not.toHaveBeenCalledWith(
      expect.stringContaining(":live"),
    );
  });

  it("falls back to bundled when draft key is missing", async () => {
    const kv = makeKvStub({});
    configureKvBinding(kv);

    const result = await loadContent("jenise-buckalew", "en", "preview");

    expect(result.source).toBe("bundled");
  });
});

// ---------------------------------------------------------------------------
// KV binding not present
// ---------------------------------------------------------------------------

describe("no KV binding", () => {
  it("falls back to bundled when CONTENT_KV is not in env", async () => {
    configureKvBinding(null); // env has no CONTENT_KV key

    const result = await loadContent("jenise-buckalew", "en", "production");

    expect(result.source).toBe("bundled");
    expect(mockLoadLocalizedContent).toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// KV error → graceful degradation
// ---------------------------------------------------------------------------

describe("KV error — graceful degradation", () => {
  it("falls back to bundled when getCloudflareContext throws", async () => {
    configureKvError();

    const result = await loadContent("jenise-buckalew", "en", "production");

    expect(result.source).toBe("bundled");
    expect(result.content).toBe(BUNDLED_CONTENT);
  });

  it("falls back to bundled when kv.get throws", async () => {
    const kv = {
      get: vi.fn().mockRejectedValue(new Error("KV network error")),
    } as unknown as KVNamespace;
    configureKvBinding(kv);

    const result = await loadContent("jenise-buckalew", "en", "production");

    expect(result.source).toBe("bundled");
  });

  it("falls back to bundled when KV returns corrupt JSON", async () => {
    const kv = makeKvStub({
      "content:v1:jenise-buckalew:en:live": "{{not valid json{{",
    });
    configureKvBinding(kv);

    const result = await loadContent("jenise-buckalew", "en", "production");

    expect(result.source).toBe("bundled");
  });
});

// ---------------------------------------------------------------------------
// Handle validation
// ---------------------------------------------------------------------------

describe("handle validation", () => {
  it("returns null content for path-traversal handle without calling KV", async () => {
    const kv = makeKvStub({});
    configureKvBinding(kv);

    const result = await loadContent("../../etc/passwd", "en", "production");

    expect(result.content).toBeNull();
    expect(result.source).toBe("bundled");
    expect(kv.get).not.toHaveBeenCalled();
  });

  it("returns null content for handle with dots", async () => {
    const kv = makeKvStub({});
    configureKvBinding(kv);

    const result = await loadContent("has.dot", "en", "production");

    expect(result.content).toBeNull();
    expect(kv.get).not.toHaveBeenCalled();
  });

  it("returns null content for empty handle", async () => {
    const kv = makeKvStub({});
    configureKvBinding(kv);

    const result = await loadContent("", "en", "production");

    expect(result.content).toBeNull();
    expect(kv.get).not.toHaveBeenCalled();
  });

  it("accepts valid handle with hyphens and numbers", async () => {
    const kv = makeKvStub({
      "content:v1:agent-123:en:live": JSON.stringify(KV_CONTENT),
    });
    configureKvBinding(kv);

    const result = await loadContent("agent-123", "en", "production");

    expect(result.source).toBe("kv");
  });
});
