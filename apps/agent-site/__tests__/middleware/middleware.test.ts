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
  return {
    status: 200,
    headers: {
      set: (key: string, value: string) => headers.set(key, value),
      get: (key: string) => headers.get(key),
    },
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

vi.mock("@/lib/routing", () => ({
  extractAgentId: vi.fn(),
  resolveAgentFromCustomDomain: vi.fn(),
  isWwwCustomDomain: vi.fn(),
  getAgentIds: vi.fn(),
}));

import { extractAgentId, resolveAgentFromCustomDomain, isWwwCustomDomain, getAgentIds } from "@/lib/routing";

const mockExtractAgentId = vi.mocked(extractAgentId);
const mockResolveCustomDomain = vi.mocked(resolveAgentFromCustomDomain);
const mockIsWwwCustomDomain = vi.mocked(isWwwCustomDomain);
const mockGetAgentIds = vi.mocked(getAgentIds);

let middleware: typeof import("@/middleware").middleware;

function makeRequest(host: string, pathname = "/", query: Record<string, string> = {}) {
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
      get: (name: string) => (name === "host" ? host : null),
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

  it("sets agentId search param when rewriting for known subdomain", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const clonedUrl = req.nextUrl.clone();
    middleware(req as never);
    expect(clonedUrl.searchParams.set).toHaveBeenCalledWith("agentId", "jenise-buckalew");
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
  it("rewrites workers.dev host with ?agentId to the specified agent", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    const req = makeRequest("real-estate-star-agents-pr-18.workers.dev", "/", { agentId: "test-agent" });
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
    const clonedUrl = mockClone.mock.results[0].value;
    expect(clonedUrl.searchParams.set).toHaveBeenCalledWith("agentId", "test-agent");
  });

  it("rewrites workers.dev host without ?agentId to default agent", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    const req = makeRequest("real-estate-star-agents-pr-18.workers.dev");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
    const clonedUrl = mockClone.mock.results[0].value;
    expect(clonedUrl.searchParams.set).toHaveBeenCalledWith("agentId", "jenise-buckalew");
  });

  it("returns 404 on workers.dev when ?agentId is unknown and default is also unknown", () => {
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    mockGetAgentIds.mockReturnValue(new Set([]));
    const req = makeRequest("real-estate-star-agents-pr-18.workers.dev", "/", { agentId: "unknown" });
    const response = middleware(req as never);
    expect(mockRewrite).not.toHaveBeenCalled();
    expect(response.status).toBe(404);
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
