import { NextRequest, NextResponse } from "next/server";
import { extractAgentId, resolveAgentFromCustomDomain, isWwwCustomDomain, getAgentIds } from "@/lib/routing";

function buildCspHeader(nonce: string): string {
  const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "";
  const apiWs = apiUrl.replace(/^https:/, "wss:").replace(/^http:/, "ws:");
  const apiConnectSrc = apiUrl ? ` ${apiUrl} ${apiWs}` : "";

  return [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic' https://maps.googleapis.com https://*.sentry.io https://*.googletagmanager.com https://*.google-analytics.com https://connect.facebook.net`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: https: https://maps.gstatic.com",
    `connect-src 'self' https://maps.googleapis.com https://maps.gstatic.com https://formspree.io https://*.sentry.io https://*.google-analytics.com https://*.analytics.google.com https://*.googletagmanager.com https://www.facebook.com https://connect.facebook.net${apiConnectSrc}`,
    "frame-ancestors 'none'",
  ].join("; ");
}

function notFoundResponse(nonce: string): NextResponse {
  const response = new NextResponse("Not Found", { status: 404 });
  response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
  response.headers.set("x-nonce", nonce);
  return response;
}

export function middleware(request: NextRequest) {
  const hostname = request.headers.get("host") || "localhost:3000";
  const nonce = Buffer.from(crypto.randomUUID()).toString("base64");

  // 1. www redirect for custom domains
  const bareDomain = isWwwCustomDomain(hostname);
  if (bareDomain) {
    const url = new URL(`https://${bareDomain}${request.nextUrl.pathname}${request.nextUrl.search}`);
    return NextResponse.redirect(url, 301);
  }

  // 2. Subdomain match
  const agentId = extractAgentId(hostname);
  if (agentId) {
    if (!getAgentIds().has(agentId)) {
      return notFoundResponse(nonce);
    }
    const url = request.nextUrl.clone();
    url.searchParams.set("agentId", agentId);
    const response = NextResponse.rewrite(url);
    response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
    response.headers.set("x-nonce", nonce);
    return response;
  }

  // 3. Custom domain match
  const customAgentId = resolveAgentFromCustomDomain(hostname);
  if (customAgentId) {
    const url = request.nextUrl.clone();
    url.searchParams.set("agentId", customAgentId);
    const response = NextResponse.rewrite(url);
    response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
    response.headers.set("x-nonce", nonce);
    return response;
  }

  // 4. Dev fallback: bare localhost gets default agent
  const host = hostname.split(":")[0];
  if (host === "localhost" && process.env.NODE_ENV !== "production") {
    const defaultId = process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
    if (getAgentIds().has(defaultId)) {
      const url = request.nextUrl.clone();
      url.searchParams.set("agentId", defaultId);
      const response = NextResponse.rewrite(url);
      response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
      response.headers.set("x-nonce", nonce);
      return response;
    }
  }

  // 5. No match -> 404
  return notFoundResponse(nonce);
}

export const config = {
  matcher: ["/((?!_next|favicon.ico|api|.*\\.(?:jpg|jpeg|png|gif|webp|svg|ico|css|js|woff|woff2)).*)"],
};
