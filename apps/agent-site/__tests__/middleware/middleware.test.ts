/**
 * @vitest-environment node
 */
import { describe, it, expect, vi, beforeEach } from "vitest";

// Mock NextResponse before importing middleware
const mockRewrite = vi.fn();
const mockNext = vi.fn();
const mockClone = vi.fn();

vi.mock("next/server", () => {
  return {
    NextResponse: {
      rewrite: mockRewrite,
      next: mockNext,
    },
  };
});

// We need to re-import after mocking
let middleware: typeof import("@/middleware").middleware;

// Helper to build a minimal NextRequest-like object
function makeRequest(host: string, pathname = "/") {
  const url = new URL(`http://${host}${pathname}`);
  const clonedUrl = new URL(`http://${host}${pathname}`);
  clonedUrl.searchParams.set = vi.fn((key, value) => {
    (clonedUrl as URL).searchParams.append(key, value);
  });
  mockClone.mockReturnValue(clonedUrl);

  return {
    headers: {
      get: (name: string) => (name === "host" ? host : null),
    },
    nextUrl: {
      clone: mockClone,
    },
  };
}

describe("middleware", () => {
  beforeEach(async () => {
    vi.resetModules();
    vi.resetAllMocks();
    mockRewrite.mockReturnValue({ type: "rewrite" });
    mockNext.mockReturnValue({ type: "next" });
    const mod = await import("@/middleware");
    middleware = mod.middleware;
  });

  it("calls NextResponse.next() for the base domain (no subdomain)", () => {
    const req = makeRequest("realestatestar.com");
    middleware(req as never);
    expect(mockNext).toHaveBeenCalled();
    expect(mockRewrite).not.toHaveBeenCalled();
  });

  it("calls NextResponse.next() for localhost", () => {
    const req = makeRequest("localhost:3000");
    middleware(req as never);
    expect(mockNext).toHaveBeenCalled();
    expect(mockRewrite).not.toHaveBeenCalled();
  });

  it("calls NextResponse.rewrite() for a valid agent subdomain", () => {
    const req = makeRequest("jenise-buckalew.realestatestar.com");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
    expect(mockNext).not.toHaveBeenCalled();
  });

  it("sets agentId search param when rewriting for valid subdomain", () => {
    const req = makeRequest("jenise-buckalew.realestatestar.com");
    const clonedUrl = req.nextUrl.clone();
    middleware(req as never);
    // The cloned URL's searchParams.set should have been called with agentId
    expect(clonedUrl.searchParams.set).toHaveBeenCalledWith("agentId", "jenise-buckalew");
  });

  it("calls NextResponse.next() for reserved subdomain 'www'", () => {
    const req = makeRequest("www.realestatestar.com");
    middleware(req as never);
    expect(mockNext).toHaveBeenCalled();
    expect(mockRewrite).not.toHaveBeenCalled();
  });

  it("calls NextResponse.next() for reserved subdomain 'api'", () => {
    const req = makeRequest("api.realestatestar.com");
    middleware(req as never);
    expect(mockNext).toHaveBeenCalled();
    expect(mockRewrite).not.toHaveBeenCalled();
  });

  it("calls NextResponse.next() for reserved subdomain 'portal'", () => {
    const req = makeRequest("portal.realestatestar.com");
    middleware(req as never);
    expect(mockNext).toHaveBeenCalled();
  });

  it("calls NextResponse.next() for reserved subdomain 'app'", () => {
    const req = makeRequest("app.realestatestar.com");
    middleware(req as never);
    expect(mockNext).toHaveBeenCalled();
  });

  it("calls NextResponse.next() for reserved subdomain 'admin'", () => {
    const req = makeRequest("admin.realestatestar.com");
    middleware(req as never);
    expect(mockNext).toHaveBeenCalled();
  });

  it("calls NextResponse.next() for nested subdomains (more than one level)", () => {
    const req = makeRequest("agent.sub.realestatestar.com");
    middleware(req as never);
    expect(mockNext).toHaveBeenCalled();
    expect(mockRewrite).not.toHaveBeenCalled();
  });

  it("calls NextResponse.next() for a custom domain (not in base list)", () => {
    const req = makeRequest("customdomain.com");
    middleware(req as never);
    expect(mockNext).toHaveBeenCalled();
    expect(mockRewrite).not.toHaveBeenCalled();
  });

  it("falls back to localhost when host header is missing", () => {
    const fakeReq = {
      headers: { get: () => null },
      nextUrl: { clone: mockClone },
    };
    const clonedUrl = { searchParams: { set: vi.fn() } };
    mockClone.mockReturnValue(clonedUrl);
    mockNext.mockReturnValue({ type: "next" });

    middleware(fakeReq as never);
    // localhost has no agent subdomain so should call next
    expect(mockNext).toHaveBeenCalled();
  });

  it("handles host with port correctly for agent subdomain", () => {
    const req = makeRequest("my-agent.realestatestar.com:443");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
  });
});

describe("middleware config export", () => {
  it("exports a matcher that excludes _next, favicon, and api paths", async () => {
    const { config } = await import("@/middleware");
    expect(config).toBeDefined();
    expect(config.matcher).toBeDefined();
    expect(Array.isArray(config.matcher)).toBe(true);
    const pattern = config.matcher[0];
    expect(pattern).toContain("_next");
    expect(pattern).toContain("favicon.ico");
    expect(pattern).toContain("api");
  });
});
