import { reportError } from "@real-estate-star/analytics";
import type { AccountConfig, ContentConfig } from "./types";
import { loadAccountConfig, loadLocalizedContent } from "./config";

export type SiteState = "live" | "pending_approval" | "unknown";

interface KvNamespace {
  get(key: string): Promise<string | null>;
}

interface WorkerEnv {
  SITE_CONTENT_KV?: KvNamespace;
}

interface CloudflareRuntime {
  env?: WorkerEnv;
}

async function getKvBinding(): Promise<KvNamespace | null> {
  try {
    const { getCloudflareContext } = await import("@opennextjs/cloudflare");
    const ctx = getCloudflareContext() as CloudflareRuntime | undefined;
    return ctx?.env?.SITE_CONTENT_KV ?? null;
  } catch {
    return null;
  }
}

function stateKey(accountId: string): string {
  return `site-state:v1:${accountId}`;
}

function accountKey(accountId: string): string {
  return `account:v1:${accountId}`;
}

function contentKey(accountId: string, locale: string): string {
  return `content:v1:${accountId}:${locale}:live`;
}

function parseSiteState(raw: string | null): SiteState {
  if (!raw) return "unknown";
  try {
    const parsed = JSON.parse(raw);
    if (parsed === "live") return "live";
    if (parsed === "pending_approval") return "pending_approval";
    return "unknown";
  } catch {
    return "unknown";
  }
}

export async function getSiteState(accountId: string): Promise<SiteState> {
  const kv = await getKvBinding();
  if (!kv) return "unknown";
  try {
    const raw = await kv.get(stateKey(accountId));
    return parseSiteState(raw);
  } catch (err) {
    reportError(err instanceof Error ? err : new Error(String(err)), {
      context: "kv-loader:site-state",
      accountId,
    });
    return "unknown";
  }
}

/**
 * Option B semantics:
 *  - If site-state is "live", the account config MUST come from KV.
 *    If KV is missing the key, throw — do not silently fall back to the
 *    prebuilt registry for a published agent.
 *  - Otherwise (pre-launch / pending_approval / unknown), serve the
 *    prebuilt registry config.
 */
export async function loadAccountConfigAsync(
  handle: string,
  siteState?: SiteState,
): Promise<AccountConfig> {
  const state = siteState ?? (await getSiteState(handle));

  if (state !== "live") {
    return loadAccountConfig(handle);
  }

  const kv = await getKvBinding();
  if (!kv) {
    throw new Error(
      `[kv-loader] Account ${handle} is live but KV binding is not available`,
    );
  }

  let raw: string | null;
  try {
    raw = await kv.get(accountKey(handle));
  } catch (err) {
    reportError(err instanceof Error ? err : new Error(String(err)), {
      context: "kv-loader:account",
      accountId: handle,
    });
    throw new Error(
      `[kv-loader] Failed to read account config from KV for live account ${handle}`,
    );
  }

  if (!raw) {
    throw new Error(
      `[kv-loader] Live account ${handle} has no account config in KV`,
    );
  }

  try {
    return JSON.parse(raw) as AccountConfig;
  } catch (err) {
    reportError(err instanceof Error ? err : new Error(String(err)), {
      context: "kv-loader:account-parse",
      accountId: handle,
    });
    throw new Error(
      `[kv-loader] Invalid account JSON in KV for ${handle}`,
    );
  }
}

/**
 * Option B semantics for localized content — same rules as account config.
 * Backend writes per-locale content keyed by locale. If the live account has
 * no content for the requested locale, we do NOT fall back to English from KV;
 * we throw, because the publish step should have written every supported locale.
 */
export async function loadLocalizedContentAsync(
  handle: string,
  locale: string,
  account: AccountConfig,
  siteState?: SiteState,
): Promise<ContentConfig> {
  const state = siteState ?? (await getSiteState(handle));

  if (state !== "live") {
    return loadLocalizedContent(handle, locale, account);
  }

  const kv = await getKvBinding();
  if (!kv) {
    throw new Error(
      `[kv-loader] Account ${handle} is live but KV binding is not available`,
    );
  }

  let raw: string | null;
  try {
    raw = await kv.get(contentKey(handle, locale));
  } catch (err) {
    reportError(err instanceof Error ? err : new Error(String(err)), {
      context: "kv-loader:content",
      accountId: handle,
      locale,
    });
    throw new Error(
      `[kv-loader] Failed to read live content from KV for ${handle}:${locale}`,
    );
  }

  if (!raw) {
    throw new Error(
      `[kv-loader] Live account ${handle} has no content for locale ${locale} in KV`,
    );
  }

  try {
    return JSON.parse(raw) as ContentConfig;
  } catch (err) {
    reportError(err instanceof Error ? err : new Error(String(err)), {
      context: "kv-loader:content-parse",
      accountId: handle,
      locale,
    });
    throw new Error(
      `[kv-loader] Invalid content JSON in KV for ${handle}:${locale}`,
    );
  }
}
