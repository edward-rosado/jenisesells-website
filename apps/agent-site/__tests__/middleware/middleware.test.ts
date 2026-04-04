/**
 * @vitest-environment node
 */
import { describe, it, expect, vi, beforeEach } from "vitest";

vi.stubGlobal("crypto", {
  randomUUID: () => "test-uuid-1234",
});

const mockRewrite = vi.fn();
const mockNext = vi.fn();
const mockRedirect = vi.fn();
const mockClone = vi.fn();

function createMockResponse() {
  const headers = new Map<string, string>();
  const cookiesMock = { set: vi.fn() };
  return {
    status: 200,
    headers: {
      set: (key: string, value: string) => headers.set(key, value),
      get: (key: string) => headers.get(key),
    },
    cookies: cookiesMock,
    _headers: headers,
  };
}

// Mock NextResponse as both a class (for `new NextResponse(body, opts)`) and static methods
class MockNextResponse {
  headers: Map<string, string>;
  status: number;
  constructor(body?: string, init?: { status?: number }) {
    this.headers = new Map();
    this.status = init?.status ?? 200;
  }
  static rewrite = mockRewrite;
  static next = mockNext;
  static redirect = mockRedirect;
}

vi.mock("next/server", () => ({
  NextResponse: MockNextResponse,
}));

vi.mock("@/features/config/routing", () => ({
  extractAgentId: vi.fn(),
  resolveAgentFromCustomDomain: vi.fn(),
  isWwwCustomDomain: vi.fn(),
  getAgentIds: vi.fn(),
}));

// accountLanguages controls which locales each agent supports
vi.mock("@/features/config/config-registry", () => ({
  accountLanguages: {
    "jenise-buckalew": ["en"],
    "test-agent": ["en", "es"],
  },
}));

import { extractAgentId, resolveAgentFromCustomDomain, isWwwCustomDomain, getAgentIds } from "@/features/config/routing";

const mockExtractAgentId = vi.mocked(extractAgentId);
const mockResolveCustomDomain = vi.mocked(resolveAgentFromCustomDomain);
const mockIsWwwCustomDomain = vi.mocked(isWwwCustomDomain);
const mockGetAgentIds = vi.mocked(getAgentIds);

let middleware: typeof import("@/middleware").middleware;

interface RequestOptions {
  query?: Record<string, string>;
  cookieLocale?: string;
  acceptLanguage?: string;
}

function makeRequest(host: string, pathname = "/", options: RequestOptions = {}) {
  const { query = {}, cookieLocale, acceptLanguage } = options;
  const qs = new URLSearchParams(query).toString();
  const fullPath = qs ? `${pathname}?${qs}` : pathname;
  const clonedUrl = new URL(`http://${host}${fullPath}`);
  clonedUrl.searchParams.set = vi.fn((key, value) => {
    (clonedUrl as URL).searchParams.append(key, value);
  });
  mockClone.mockReturnValue(clonedUrl);

  const requestUrl = new URL(`http://${host}${fullPath}`);
  return {
    headers: {
      get: (name: string) => {
        if (name === "host") return host;
        if (name === "accept-language") return acceptLanguage ?? null;
        return null;
      },
    },
    cookies: {
      get: (name: string) => (name === "locale" && cookieLocale ? { value: cookieLocale } : undefined),
    },
    nextUrl: {
      clone: mockClone,
      pathname,
      searchParams: requestUrl.searchParams,
      search: requestUrl.search,
    },
  };
}

describe("middleware", () => {
  beforeEach(async () => {
    vi.resetModules();
    vi.resetAllMocks();
    mockRewrite.mockReturnValue(createMockResponse());
    mockNext.mockReturnValue(createMockResponse());
    mockRedirect.mockReturnValue(createMockResponse());
    mockGetAgentIds.mockReturnValue(new Set(["jenise-buckalew", "test-agent"]));
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    mockIsWwwCustomDomain.mockReturnValue(null);
    const mod = await import("@/middleware");
    middleware = mod.middleware;
  });

  // --- www redirect ---
  it("301 redirects www.customdomain to bare domain", () => {
    mockIsWwwCustomDomain.mockReturnValue("jenisesellsnj.com");
    const req = makeRequest("www.jenisesellsnj.com", "/about");
    middleware(req as never);
    expect(mockRedirect).toHaveBeenCalled();
    const redirectUrl = mockRedirect.mock.calls[0][0];
    expect(redirectUrl.toString()).toContain("jenisesellsnj.com/about");
  });

  // --- subdomain match ---
  it("rewrites for a known agent subdomain", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
    expect(mockNext).not.toHaveBeenCalled();
  });

  it("returns 404 response for unknown agent subdomain", () => {
    mockExtractAgentId.mockReturnValue("unknown-agent");
    const req = makeRequest("unknown-agent.real-estate-star.com");
    const response = middleware(req as never);
    expect(mockRewrite).not.toHaveBeenCalled();
    expect(mockNext).not.toHaveBeenCalled();
    expect(response.status).toBe(404);
  });

  it("sets accountId search param when rewriting for known subdomain", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const clonedUrl = req.nextUrl.clone();
    middleware(req as never);
    expect(clonedUrl.searchParams.set).toHaveBeenCalledWith("accountId", "jenise-buckalew");
  });

  // --- custom domain match ---
  it("rewrites for a known custom domain", () => {
    mockResolveCustomDomain.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenisesellsnj.com");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
  });

  // --- no match -> 404 ---
  it("returns 404 for completely unknown hostname", () => {
    const req = makeRequest("random.com");
    const response = middleware(req as never);
    expect(mockRewrite).not.toHaveBeenCalled();
    expect(mockNext).not.toHaveBeenCalled();
    expect(response.status).toBe(404);
  });

  // --- reserved subdomains fall through to 404 (not served by agent-site) ---
  it("returns 404 when extractAgentId returns null and no custom domain match", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    const req = makeRequest("www.real-estate-star.com");
    const response = middleware(req as never);
    expect(mockRewrite).not.toHaveBeenCalled();
    expect(response.status).toBe(404);
  });

  // --- CSP ---
  it("sets Content-Security-Policy header on the response", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = middleware(req as never);
    expect(response.headers.get("Content-Security-Policy")).toContain("default-src 'self'");
  });

  it("sets x-nonce header on the response", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = middleware(req as never);
    expect(response.headers.get("x-nonce")).toBeTruthy();
  });

  it("includes API URL in CSP connect-src when set", async () => {
    process.env.NEXT_PUBLIC_API_URL = "https://api.example.com";
    vi.resetModules();
    vi.resetAllMocks();
    mockRewrite.mockReturnValue(createMockResponse());
    mockGetAgentIds.mockReturnValue(new Set(["jenise-buckalew"]));
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    mockIsWwwCustomDomain.mockReturnValue(null);

    const mod = await import("@/middleware");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = mod.middleware(req as never);
    const csp = response.headers.get("Content-Security-Policy")!;
    expect(csp).toContain("https://api.example.com");
    expect(csp).toContain("wss://api.example.com");

    delete process.env.NEXT_PUBLIC_API_URL;
  });

  it("converts http:// to ws:// in CSP connect-src", async () => {
    process.env.NEXT_PUBLIC_API_URL = "http://localhost:5135";
    vi.resetModules();
    vi.resetAllMocks();
    mockRewrite.mockReturnValue(createMockResponse());
    mockGetAgentIds.mockReturnValue(new Set(["jenise-buckalew"]));
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    mockIsWwwCustomDomain.mockReturnValue(null);

    const mod = await import("@/middleware");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = mod.middleware(req as never);
    const csp = response.headers.get("Content-Security-Policy")!;
    expect(csp).toContain("http://localhost:5135");
    expect(csp).toContain("ws://localhost:5135");

    delete process.env.NEXT_PUBLIC_API_URL;
  });

  it("includes frame-src for Turnstile in CSP", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = middleware(req as never);
    const csp = response.headers.get("Content-Security-Policy")!;
    expect(csp).toContain("frame-src https://challenges.cloudflare.com");
  });

  it("omits API URL from CSP when NEXT_PUBLIC_API_URL is not set", () => {
    delete process.env.NEXT_PUBLIC_API_URL;
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = middleware(req as never);
    const csp = response.headers.get("Content-Security-Policy")!;
    expect(csp).not.toContain("wss://");
    expect(csp).not.toContain("ws://");
  });

  // --- fallback + edge cases ---
  it("falls back to localhost:3000 when host header is missing", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    mockGetAgentIds.mockReturnValue(new Set(["jenise-buckalew"]));
    const req = {
      headers: { get: () => null },
      cookies: { get: () => undefined },
      nextUrl: { clone: vi.fn().mockReturnValue(new URL("http://localhost:3000/")), pathname: "/" },
    };
    const response = middleware(req as never);
    expect(response).toBeDefined();
  });

  it("strips port from production hostname for agent matching", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com:443");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
  });

  // --- localhost dev ---
  it("rewrites for agent subdomain on localhost", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.localhost:3000");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
  });

  // --- dev fallback: bare localhost ---
  it("rewrites bare localhost to default agent in dev mode", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    const req = makeRequest("localhost:3000");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
  });

  it("uses DEFAULT_AGENT_ID env var for bare localhost fallback", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    process.env.DEFAULT_AGENT_ID = "test-agent";
    const req = makeRequest("localhost:3000");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
    delete process.env.DEFAULT_AGENT_ID;
  });

  it("returns 404 when DEFAULT_AGENT_ID is set but not in the known agent set", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    mockGetAgentIds.mockReturnValue(new Set(["jenise-buckalew"]));
    process.env.DEFAULT_AGENT_ID = "nonexistent-agent";
    const req = makeRequest("localhost:3000");
    const response = middleware(req as never);
    expect(mockRewrite).not.toHaveBeenCalled();
    expect(response.status).toBe(404);
    delete process.env.DEFAULT_AGENT_ID;
  });

  // --- workers.dev preview ---
  it("rewrites workers.dev host with ?accountId to the specified agent", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    const req = makeRequest("real-estate-star-agents-pr-18.workers.dev", "/", { query: { accountId: "test-agent" } });
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
    const clonedUrl = mockClone.mock.results[0].value;
    expect(clonedUrl.searchParams.set).toHaveBeenCalledWith("accountId", "test-agent");
  });

  it("rewrites workers.dev host without ?accountId to default agent", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    const req = makeRequest("real-estate-star-agents-pr-18.workers.dev");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
    const clonedUrl = mockClone.mock.results[0].value;
    expect(clonedUrl.searchParams.set).toHaveBeenCalledWith("accountId", "jenise-buckalew");
  });

  it("returns 404 on workers.dev when ?accountId is unknown and default is also unknown", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    mockGetAgentIds.mockReturnValue(new Set([]));
    const req = makeRequest("real-estate-star-agents-pr-18.workers.dev", "/", { query: { accountId: "unknown" } });
    const response = middleware(req as never);
    expect(mockRewrite).not.toHaveBeenCalled();
    expect(response.status).toBe(404);
  });

  // --- locale ---
  it("sets x-locale header on subdomain rewrite", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = middleware(req as never);
    expect(response.headers.get("x-locale")).toBe("en");
  });

  it("sets locale cookie when Accept-Language resolves to non-English for a multilingual agent", () => {
    // test-agent supports ["en", "es"] per the config-registry mock
    mockExtractAgentId.mockReturnValue("test-agent");
    const req = makeRequest("test-agent.real-estate-star.com", "/", { acceptLanguage: "es" });
    const response = middleware(req as never);
    expect(response.headers.get("x-locale")).toBe("es");
    expect(response.cookies.set).toHaveBeenCalledWith("locale", "es", expect.objectContaining({ path: "/" }));
  });

  it("does not set locale cookie when locale resolves to English", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = middleware(req as never);
    expect(response.cookies.set).not.toHaveBeenCalled();
  });

  it("uses cookie locale when present and agent supports it", () => {
    mockExtractAgentId.mockReturnValue("test-agent");
    const req = makeRequest("test-agent.real-estate-star.com", "/", { cookieLocale: "es" });
    const response = middleware(req as never);
    expect(response.headers.get("x-locale")).toBe("es");
  });

  it("sets x-locale for custom domain match", () => {
    mockResolveCustomDomain.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenisesellsnj.com");
    const response = middleware(req as never);
    expect(response.headers.get("x-locale")).toBe("en");
  });

  it("sets x-locale for workers.dev fallback", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    const req = makeRequest("real-estate-star-agents-pr-18.workers.dev");
    const response = middleware(req as never);
    expect(response.headers.get("x-locale")).toBe("en");
  });

  it("falls back to English when agent has no configured languages", () => {
    // "unknown-in-registry" is not in accountLanguages mock, so agentLangs is undefined → locales = ["en"]
    mockExtractAgentId.mockReturnValue("no-langs-agent");
    mockGetAgentIds.mockReturnValue(new Set(["no-langs-agent"]));
    const req = makeRequest("no-langs-agent.real-estate-star.com");
    const response = middleware(req as never);
    expect(response.headers.get("x-locale")).toBe("en");
  });
});

describe("middleware config export", () => {
  it("exports a matcher that excludes _next, favicon, and api paths", async () => {
    const { config } = await import("@/middleware");
    expect(config.matcher).toBeDefined();
    const pattern = config.matcher[0];
    expect(pattern).toContain("_next");
    expect(pattern).toContain("favicon.ico");
    expect(pattern).toContain("api");
  });
});
