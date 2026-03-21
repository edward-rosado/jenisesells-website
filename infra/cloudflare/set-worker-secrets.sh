#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# set-worker-secrets.sh — Set runtime secrets on the Cloudflare Worker
#
# Prerequisites:
#   - Node.js installed (for npx wrangler)
#   - CLOUDFLARE_API_TOKEN env var set, or wrangler logged in via `wrangler login`
#
# These secrets are NOT in wrangler.jsonc (they'd be plaintext). They must be
# set via `wrangler secret put` or `wrangler secret bulk`.
#
# Usage:
#   ./set-worker-secrets.sh              # interactive prompts
#   ./set-worker-secrets.sh --from-env   # read from environment variables
###############################################################################

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
APP_DIR="$REPO_ROOT/apps/agent-site"

# Secrets to configure (env var name : display name)
declare -a SECRETS=(
  "TURNSTILE_SECRET_KEY|Cloudflare Turnstile Secret Key"
  "LEAD_API_KEY|Lead API Key"
  "LEAD_HMAC_SECRET|Lead HMAC Secret"
  "LEAD_API_URL|Lead API URL"
)

# --- Preflight checks --------------------------------------------------------
echo ""
echo "============================================================================"
echo "  Real Estate Star — Set Cloudflare Worker Secrets"
echo "============================================================================"
echo ""

if ! command -v npx &>/dev/null; then
  echo "ERROR: npx not found. Install Node.js first."
  exit 1
fi

# Verify wrangler can authenticate
echo "Verifying Cloudflare authentication..."
if ! npx --prefix "$APP_DIR" wrangler whoami &>/dev/null 2>&1; then
  echo "ERROR: Cloudflare authentication failed."
  echo "  Set CLOUDFLARE_API_TOKEN or run: npx wrangler login"
  exit 1
fi
echo "  [OK] Authenticated"
echo ""

# --- Collect secrets ----------------------------------------------------------
FROM_ENV=false
if [[ "${1:-}" == "--from-env" ]]; then
  FROM_ENV=true
  echo "Reading secrets from environment variables..."
  echo ""
fi

declare -A SECRET_VALUES

for entry in "${SECRETS[@]}"; do
  SECRET_NAME="${entry%%|*}"
  DISPLAY_NAME="${entry##*|}"

  if $FROM_ENV; then
    VALUE="${!SECRET_NAME:-}"
    if [[ -z "$VALUE" ]]; then
      echo "  [SKIP] $SECRET_NAME not set in environment"
      continue
    fi
    echo "  [OK]   $SECRET_NAME (from env)"
  else
    echo -n "  $DISPLAY_NAME ($SECRET_NAME): "
    read -rs VALUE
    echo ""

    if [[ -z "$VALUE" ]]; then
      echo "    -> Skipped (empty). Secret keeps its current value."
      continue
    fi
  fi

  SECRET_VALUES["$SECRET_NAME"]="$VALUE"
done

echo ""

if [[ ${#SECRET_VALUES[@]} -eq 0 ]]; then
  echo "No secrets to update. Exiting."
  exit 0
fi

# --- Build JSON and apply via wrangler secret bulk ----------------------------
echo "Setting ${#SECRET_VALUES[@]} secret(s) on worker..."

JSON="{"
first=true
for key in "${!SECRET_VALUES[@]}"; do
  if $first; then
    first=false
  else
    JSON+=","
  fi
  # Escape double quotes in values
  ESCAPED_VALUE="${SECRET_VALUES[$key]//\"/\\\"}"
  JSON+="\"$key\":\"$ESCAPED_VALUE\""
done
JSON+="}"

echo "$JSON" | npx --prefix "$APP_DIR" wrangler secret bulk

echo ""
echo "============================================================================"
echo "  Secrets updated: ${#SECRET_VALUES[@]}"
echo "  Worker will use new values on next request (no restart needed)."
echo "============================================================================"
echo ""
