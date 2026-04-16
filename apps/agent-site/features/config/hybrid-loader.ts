// apps/agent-site/features/config/hybrid-loader.ts

import type { ContentConfig } from "./types";
import { loadLocalizedContent } from "./config";

export type LoaderEnv = "preview" | "production";

export interface LoaderResult {
  content: ContentConfig | null;
  source: "bundled" | "kv";
}

const VALID_HANDLE = /^[a-z0-9-]+$/;

function isValidHandle(handle: string): boolean {
  return VALID_HANDLE.test(handle);
}

/**
 * Environment-aware content loader.
 * - preview: returns bundled fixtures from config-registry (safe at build/edge time)
 * - production: stub for Cloudflare KV — falls back to bundled (real KV wiring in B8)
 */
export async function loadContent(
  handle: string,
  locale: string,
  env: LoaderEnv,
): Promise<LoaderResult> {
  if (env === "preview") {
    return loadFromBundled(handle, locale);
  }

  // Production KV path — stub for now, real implementation in B8
  return loadFromKv(handle, locale);
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

async function loadFromKv(handle: string, locale: string): Promise<LoaderResult> {
  // Stub: will be wired to Cloudflare Workers KV in B8
  // For now, fall back to bundled fixtures so the production path is safe to call
  return loadFromBundled(handle, locale);
}
