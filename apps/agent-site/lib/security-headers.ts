import { NextResponse } from "next/server";

export function applySecurityHeaders(response: NextResponse): void {
  response.headers.set("X-Content-Type-Options", "nosniff");
  response.headers.set("Referrer-Policy", "strict-origin-when-cross-origin");
  response.headers.set("Strict-Transport-Security", "max-age=63072000; includeSubDomains");
}

const SAFE_URL = /^https?:\/\/[a-zA-Z0-9._:-]+$/;

/** Validate a URL is safe to inject into a CSP header (no semicolons, no directives). */
export function safeCspUrl(url: string | undefined): string {
  if (!url) return "";
  return SAFE_URL.test(url) ? url : "";
}
