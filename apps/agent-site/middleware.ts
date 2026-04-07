import { NextRequest, NextResponse } from "next/server";
import { extractAgentId, resolveAgentFromCustomDomain, isWwwCustomDomain, getAgentIds } from "@/features/config/routing";
import { applySecurityHeaders, safeCspUrl } from "@/features/shared/security-headers";
import { accountLanguages } from "@/features/config/config-registry";
import { resolveLocale as resolveLocaleFromHeaders } from "@/features/i18n/resolve-locale";
import type { SupportedLocale } from "@/features/i18n/locale-map";

function buildCspHeader(nonce: string): string {
  const apiUrl = safeCspUrl(process.env.NEXT_PUBLIC_API_URL);
  const apiWs = apiUrl.replace(/^https:/, "wss:").replace(/^http:/, "ws:");
  const apiConnectSrc = apiUrl ? ` ${apiUrl} ${apiWs}` : "";

  return [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic' https://maps.googleapis.com https://*.sentry.io https://*.googletagmanager.com https://*.google-analytics.com https://connect.facebook.net`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: https: https://maps.gstatic.com",
    `connect-src 'self' https://maps.googleapis.com https://places.googleapis.com https://maps.gstatic.com https://*.sentry.io https://*.google-analytics.com https://*.analytics.google.com https://*.googletagmanager.com https://www.facebook.com https://connect.facebook.net${apiConnectSrc}`,
    "frame-src https://challenges.cloudflare.com",
    "frame-ancestors 'none'",
  ].join("; ");
}

function notFoundResponse(nonce: string): NextResponse {
  const response = new NextResponse("Not Found", { status: 404 });
  response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
  response.headers.set("x-nonce", nonce);
  applySecurityHeaders(response);
  return response;
}

function resolveLocaleForAccount(request: NextRequest, accountId: string): SupportedLocale {
  const cookieLocale = request.cookies?.get("locale")?.value ?? null;
  const acceptLanguage = request.headers.get("accept-language");
  const agentLangs = accountLanguages[accountId] as SupportedLocale[] | undefined;
  const locales = agentLangs ?? ["en" as SupportedLocale];
  return resolveLocaleFromHeaders(acceptLanguage, cookieLocale, locales);
}

function applyLocaleToUrl(url: URL, locale: SupportedLocale): void {
  url.searchParams.set("locale", locale);
}

function applyLocaleToResponse(response: NextResponse, locale: SupportedLocale): void {
  response.headers.set("x-locale", locale);
  if (locale !== "en") {
    response.cookies.set("locale", locale, { path: "/", maxAge: 365 * 24 * 60 * 60, sameSite: "lax" });
  }
}

export function middleware(request: NextRequest) {
  const hostname = request.headers.get("host") || "localhost:3000";
  const nonce = Buffer.from(crypto.randomUUID()).toString("base64");

  // 1. www redirect for custom domains
  const bareDomain = isWwwCustomDomain(hostname);
  if (bareDomain) {
    const url = new URL(`https://${bareDomain}${request.nextUrl.pathname}${request.nextUrl.search}`);
    const redirectResponse = NextResponse.redirect(url, 301);
    applySecurityHeaders(redirectResponse);
    return redirectResponse;
  }

  // 2. Subdomain match
  const agentId = extractAgentId(hostname);
  if (agentId) {
    if (!getAgentIds().has(agentId)) {
      return notFoundResponse(nonce);
    }
    const locale = resolveLocaleForAccount(request, agentId);
    const url = request.nextUrl.clone();
    url.searchParams.set("accountId", agentId);
    applyLocaleToUrl(url, locale);
    const response = NextResponse.rewrite(url);
    applyLocaleToResponse(response, locale);
    response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
    response.headers.set("x-nonce", nonce);
    applySecurityHeaders(response);
    return response;
  }

  // 3. Custom domain match
  const customAgentId = resolveAgentFromCustomDomain(hostname);
  if (customAgentId) {
    const locale = resolveLocaleForAccount(request, customAgentId);
    const url = request.nextUrl.clone();
    url.searchParams.set("accountId", customAgentId);
    applyLocaleToUrl(url, locale);
    const response = NextResponse.rewrite(url);
    applyLocaleToResponse(response, locale);
    response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
    response.headers.set("x-nonce", nonce);
    applySecurityHeaders(response);
    return response;
  }

  // 4. Preview/dev fallback: workers.dev or localhost with ?accountId= param
  const host = hostname.split(":")[0];
  const queryAgentId = request.nextUrl.searchParams.get("accountId");
  const isPreviewHost = host.endsWith(".workers.dev");
  const isLocalhost = host === "localhost";

  if (isPreviewHost || isLocalhost) {
    const resolvedId = (queryAgentId && getAgentIds().has(queryAgentId))
      ? queryAgentId
      : (process.env.DEFAULT_AGENT_ID || "jenise-buckalew");

    if (getAgentIds().has(resolvedId)) {
      const locale = resolveLocaleForAccount(request, resolvedId);
      const url = request.nextUrl.clone();
      url.searchParams.set("accountId", resolvedId);
      applyLocaleToUrl(url, locale);
      const response = NextResponse.rewrite(url);
      applyLocaleToResponse(response, locale);
      response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
      response.headers.set("x-nonce", nonce);
      applySecurityHeaders(response);
      return response;
    }
  }

  // 5. No match -> 404
  return notFoundResponse(nonce);
}

export const config = {
  matcher: ["/((?!_next|favicon.ico|api|.*\\.(?:jpg|jpeg|png|gif|webp|svg|ico|css|js|woff|woff2)).*)"],
};
