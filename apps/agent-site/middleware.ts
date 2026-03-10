import { NextRequest, NextResponse } from "next/server";
import { extractAgentId } from "./lib/routing";

function buildCspHeader(nonce: string): string {
  return [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic' https://*.sentry.io https://*.googletagmanager.com https://*.google-analytics.com https://connect.facebook.net`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: https:",
    "connect-src 'self' https://formspree.io https://*.sentry.io https://*.google-analytics.com https://*.analytics.google.com https://*.googletagmanager.com https://www.facebook.com https://connect.facebook.net",
    "frame-ancestors 'none'",
  ].join("; ");
}

export function middleware(request: NextRequest) {
  const hostname = request.headers.get("host") || "localhost:3000";
  const agentId = extractAgentId(hostname);

  // Generate a per-request nonce for CSP
  const nonce = Buffer.from(crypto.randomUUID()).toString("base64");

  let response: NextResponse;

  if (agentId) {
    // Rewrite the URL to include agent ID for the page to access
    const url = request.nextUrl.clone();
    url.searchParams.set("agentId", agentId);
    response = NextResponse.rewrite(url);
  } else {
    response = NextResponse.next();
  }

  // Set CSP with nonce and expose nonce to server components via header
  response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
  response.headers.set("x-nonce", nonce);

  return response;
}

export const config = {
  matcher: ["/((?!_next|favicon.ico|api).*)"],
};
