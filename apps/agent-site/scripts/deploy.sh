#!/usr/bin/env bash
#
# Deploy agent-site to Cloudflare Pages using OpenNext adapter.
#
# Usage:
#   ./deploy.sh <agent-slug>
#
# Environment variables (required):
#   CLOUDFLARE_API_TOKEN  — Cloudflare API token with Pages write access
#   CLOUDFLARE_ACCOUNT_ID — Cloudflare account ID
#
# Environment variables (optional):
#   NEXT_PUBLIC_API_URL   — API URL baked into the client bundle at build time

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AGENT_SITE_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_NAME="real-estate-star-agents"

AGENT_SLUG="${1:-}"
if [[ -z "$AGENT_SLUG" ]]; then
  echo "ERROR: agent slug is required" >&2
  echo "Usage: $0 <agent-slug>" >&2
  exit 1
fi

# Validate slug format (lowercase alphanumeric + hyphens only)
if [[ ! "$AGENT_SLUG" =~ ^[a-z0-9-]+$ ]]; then
  echo "ERROR: invalid agent slug '$AGENT_SLUG' — must be lowercase alphanumeric with hyphens" >&2
  exit 1
fi

# Validate required environment variables
if [[ -z "${CLOUDFLARE_API_TOKEN:-}" ]]; then
  echo "ERROR: CLOUDFLARE_API_TOKEN environment variable is required" >&2
  exit 1
fi
if [[ -z "${CLOUDFLARE_ACCOUNT_ID:-}" ]]; then
  echo "ERROR: CLOUDFLARE_ACCOUNT_ID environment variable is required" >&2
  exit 1
fi

cd "$AGENT_SITE_DIR"

echo "[DEPLOY] Building Next.js app..."
npx next build

echo "[DEPLOY] Building for Cloudflare (OpenNext)..."
npx opennextjs-cloudflare build

echo "[DEPLOY] Deploying to Cloudflare Pages (branch: $AGENT_SLUG)..."
npx wrangler pages deploy .open-next/assets \
  --project-name="$PROJECT_NAME" \
  --branch="$AGENT_SLUG"

echo "[DEPLOY] Done."
