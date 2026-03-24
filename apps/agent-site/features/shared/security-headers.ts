import { NextResponse } from "next/server";

export function applySecurityHeaders(response: NextResponse): void {
  response.headers.set("X-Content-Type-Options", "nosniff");
  response.headers.set("Referrer-Policy", "strict-origin-when-cross-origin");
  response.headers.set(
    "Strict-Transport-Security",
    "max-age=63072000; includeSubDomains",
  );
  response.headers.set("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
}

const SAFE_HTTPS_URL = /^https:\/\/[a-zA-Z0-9._:-]+$/;
const SAFE_LOCALHOST = /^http:\/\/localhost(:\d+)?$/;

export function safeCspUrl(url: string | undefined): string {
  if (!url) return "";
  if (SAFE_HTTPS_URL.test(url) || SAFE_LOCALHOST.test(url)) return url;
  return "";
}
