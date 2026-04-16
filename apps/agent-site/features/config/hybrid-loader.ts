// apps/agent-site/features/config/hybrid-loader.ts

import type { ContentConfig } from "./types";
import { loadLocalizedContent } from "./config";
import { getCloudflareContext } from "@opennextjs/cloudflare";

export type LoaderEnv = "preview" | "production";

export interface LoaderResult {
  content: ContentConfig | null;
  source: "bundled" | "kv";
}

/**
 * KV key format: content:v1:{handle}:{locale}:{variant}
 *
 * variant = "live"  — published content (production requests)
 * variant = "draft" — unpublished content (preview requests)
 */
const KV_KEY_VERSION = "v1";
const VALID_HANDLE = /^[a-z0-9-]+$/;

function isValidHandle(handle: string): boolean {
  return VALID_HANDLE.test(handle);
}

function buildKvKey(handle: string, locale: string, variant: "live" | "draft"): string {
  return `content:${KV_KEY_VERSION}:${handle}:${locale}:${variant}`;
}

/**
 * Retrieve the CONTENT_KV binding from the Cloudflare Workers environment.
 *
 * Uses @opennextjs/cloudflare's getCloudflareContext in async mode so it works
 * correctly inside Next.js Server Components and Route Handlers.
 *
 * Returns null when:
 * - Running in local dev/test (no Cloudflare context available)
 * - The CONTENT_KV binding is not configured in wrangler.jsonc
 */
async function getKvBinding(): Promise<KVNamespace | null> {
  try {
    const { env } = await getCloudflareContext({ async: true });
    // CloudflareEnv is globally augmented by @opennextjs/cloudflare.
    // CONTENT_KV is our custom binding — index via record to avoid augmenting the global type here.
    const kv = (env as Record<string, unknown>)["CONTENT_KV"];
    if (kv && typeof (kv as KVNamespace).get === "function") {
      return kv as KVNamespace;
    }
    return null;
  } catch {
    // Not in a Cloudflare Workers context (local dev, test, SSG build) — fall back to bundled
    return null;
  }
}

/**
 * Environment-aware content loader.
 *
 * - preview:    reads the `:draft` KV key so editors see unpublished content;
 *               falls back to bundled fixtures on KV miss or error
 * - production: reads the `:live` KV key for published content;
 *               falls back to bundled fixtures on KV miss or error
 *
 * Fallback chain (both modes):
 *   KV hit (valid JSON) → return { content, source: "kv" }
 *   KV miss             → bundled fixtures → return { content, source: "bundled" }
 *   KV error            → bundled fixtures → return { content, source: "bundled" }
 *   Invalid handle      → return { content: null, source: "bundled" } (no KV call)
 */
export async function loadContent(
  handle: string,
  locale: string,
  env: LoaderEnv,
): Promise<LoaderResult> {
  const variant = env === "preview" ? "draft" : "live";
  return loadFromKv(handle, locale, variant);
}

function loadFromBundled(handle: string, locale: string): LoaderResult {
  if (!isValidHandle(handle)) {
    return { content: null, source: "bundled" };
  }

  try {
    const content = loadLocalizedContent(handle, locale);
    return { content, source: "bundled" };
  } catch {
    return { content: null, source: "bundled" };
  }
}

async function loadFromKv(
  handle: string,
  locale: string,
  variant: "live" | "draft",
): Promise<LoaderResult> {
  if (!isValidHandle(handle)) {
    return { content: null, source: "bundled" };
  }

  const kv = await getKvBinding();

  if (!kv) {
    // No KV binding available — fall back to bundled
    return loadFromBundled(handle, locale);
  }

  const key = buildKvKey(handle, locale, variant);
  let raw: string | null;
  try {
    raw = await kv.get(key);
  } catch {
    // KV read error (network, binding misconfiguration) — degrade gracefully
    return loadFromBundled(handle, locale);
  }

  if (raw === null) {
    // KV miss — fall back to bundled
    return loadFromBundled(handle, locale);
  }

  let content: ContentConfig;
  try {
    content = JSON.parse(raw) as ContentConfig;
  } catch {
    // Corrupt KV value — fall back to bundled
    return loadFromBundled(handle, locale);
  }

  return { content, source: "kv" };
}
