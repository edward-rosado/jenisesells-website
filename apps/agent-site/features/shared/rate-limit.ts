"use server";

/**
 * Simple sliding-window rate limiter for server actions.
 *
 * Uses an in-memory Map keyed by action+identifier. On Cloudflare Workers
 * each isolate has its own Map, so this is per-instance, not distributed.
 * That's fine for basic abuse prevention — the API has its own HMAC auth
 * as a second layer.
 *
 * Entries auto-expire: stale keys are pruned on each check call.
 */

interface RateLimitEntry {
  timestamps: number[];
}

const store = new Map<string, RateLimitEntry>();

const DEFAULT_WINDOW_MS = 60_000; // 1 minute
const DEFAULT_MAX_REQUESTS = 5;

// Prune stale entries every 100 checks to prevent unbounded growth
let checkCount = 0;
const PRUNE_INTERVAL = 100;

function pruneStale(windowMs: number): void {
  const now = Date.now();
  for (const [key, entry] of store) {
    entry.timestamps = entry.timestamps.filter((t) => now - t < windowMs);
    if (entry.timestamps.length === 0) store.delete(key);
  }
}

export async function checkRateLimit(
  action: string,
  identifier: string,
  maxRequests: number = DEFAULT_MAX_REQUESTS,
  windowMs: number = DEFAULT_WINDOW_MS,
): Promise<{ allowed: boolean; retryAfterMs?: number }> {
  checkCount++;
  if (checkCount % PRUNE_INTERVAL === 0) pruneStale(windowMs);

  const key = `${action}:${identifier}`;
  const now = Date.now();
  const entry = store.get(key) ?? { timestamps: [] };

  // Remove timestamps outside the window
  entry.timestamps = entry.timestamps.filter((t) => now - t < windowMs);

  if (entry.timestamps.length >= maxRequests) {
    const oldestInWindow = entry.timestamps[0];
    const retryAfterMs = windowMs - (now - oldestInWindow);
    return { allowed: false, retryAfterMs };
  }

  entry.timestamps.push(now);
  store.set(key, entry);
  return { allowed: true };
}

/** Reset all rate limit state — for testing only. */
export async function resetRateLimits(): Promise<void> {
  store.clear();
  checkCount = 0;
}
