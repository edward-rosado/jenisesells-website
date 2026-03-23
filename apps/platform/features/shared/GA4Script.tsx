"use client";

/**
 * Injects the Google Analytics 4 gtag script for the platform.
 * Uses NEXT_PUBLIC_GA4_ID — the platform's own GA4 measurement ID.
 * This is NOT agent-specific; it tracks the platform itself.
 *
 * Renders nothing when NEXT_PUBLIC_GA4_ID is not set (dev / preview envs).
 *
 * Security note: gaId comes exclusively from a build-time env var (no user input),
 * so there is no XSS surface here. The inline script is intentionally minimal.
 */
export function GA4Script() {
  const gaId = process.env.NEXT_PUBLIC_GA4_ID;
  if (!gaId) return null;

  // Build the inline config script as a fixed template from the build-time env var.
  // This is safe: gaId is a build-time constant (e.g. "G-XXXXXXXXXX"), never user-supplied.
  const inlineScript = [
    "window.dataLayer = window.dataLayer || [];",
    "function gtag(){dataLayer.push(arguments);}",
    "gtag('js', new Date());",
    `gtag('config', '${gaId}');`,
  ].join("\n");

  return (
    <>
      <script
        async
        src={`https://www.googletagmanager.com/gtag/js?id=${gaId}`}
        data-testid="ga4-gtag-script"
      />
      {/* Safe: inlineScript is derived entirely from a build-time env var */}
      {/* eslint-disable-next-line react/no-danger */}
      <script
        dangerouslySetInnerHTML={{ __html: inlineScript }}
        data-testid="ga4-config-script"
      />
    </>
  );
}
