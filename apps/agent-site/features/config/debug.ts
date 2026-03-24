/**
 * App-wide debug configuration. Controlled by environment variables:
 * - NEXT_PUBLIC_DEBUG: "true" to enable debug mode
 * - NEXT_PUBLIC_DEBUG_ACCOUNTS: comma-separated account handles, or "*" for all
 *
 * Import this wherever you need debug-gated logging.
 */
const debugEnabled = process.env.NEXT_PUBLIC_DEBUG === "true";
const debugAccountList = (process.env.NEXT_PUBLIC_DEBUG_ACCOUNTS ?? "").split(",").map(s => s.trim()).filter(Boolean);

export const debug = {
  /** Whether debug mode is globally enabled */
  enabled: debugEnabled,

  /** Check if a specific account has debug enabled */
  isAccountDebug(accountId: string): boolean {
    if (!debugEnabled) return false;
    if (debugAccountList.includes("*")) return true;
    return debugAccountList.includes(accountId);
  },
};
