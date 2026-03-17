#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# verify-live.sh — Post-deployment verification for Real Estate Star
#
# Run this after every production deployment to verify everything is working.
# Can target Azure URL or custom domain.
#
# Usage:
#   bash infra/verify-live.sh                              # Uses Azure FQDN
#   bash infra/verify-live.sh --domain real-estate-star.com  # Uses custom domain
###############################################################################

# --- Colors ------------------------------------------------------------------
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

ok()   { echo -e "  ${GREEN}✔${NC} $1"; ((PASS++)); }
warn() { echo -e "  ${YELLOW}⚠${NC} $1"; ((WARN_COUNT++)); }
fail() { echo -e "  ${RED}✘${NC} $1"; ((FAIL++)); }
info() { echo -e "  ${CYAN}→${NC} $1"; }

PASS=0
FAIL=0
WARN_COUNT=0
TIMEOUT=10

# --- Configuration -----------------------------------------------------------
RESOURCE_GROUP="real-estate-star-rg"
APP_NAME="real-estate-star-api"
CUSTOM_DOMAIN=""

while [[ $# -gt 0 ]]; do
  case $1 in
    --domain)
      CUSTOM_DOMAIN="$2"
      shift 2
      ;;
    *)
      echo "Usage: bash infra/verify-live.sh [--domain real-estate-star.com]"
      exit 1
      ;;
  esac
done

# Determine API URL
if [[ -n "$CUSTOM_DOMAIN" ]]; then
  API_BASE="https://api.$CUSTOM_DOMAIN"
  PLATFORM_BASE="https://platform.$CUSTOM_DOMAIN"
else
  API_FQDN=$(az containerapp show \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query "properties.configuration.ingress.fqdn" \
    --output tsv 2>/dev/null || echo "")

  if [[ -z "$API_FQDN" ]]; then
    echo -e "${RED}ERROR: Could not determine API URL. Are you logged into Azure?${NC}"
    exit 1
  fi
  API_BASE="https://$API_FQDN"
  PLATFORM_BASE=""
fi

echo ""
echo -e "${CYAN}╔══════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║${NC}  ${BOLD}Real Estate Star — Production Verification${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo "  API:      $API_BASE"
[[ -n "$PLATFORM_BASE" ]] && echo "  Platform: $PLATFORM_BASE"
echo ""

# --- Health Endpoints --------------------------------------------------------
echo -e "${BOLD}Health Endpoints${NC}"
echo ""

LIVE_STATUS=$(curl -sf -o /dev/null -w "%{http_code}" "$API_BASE/health/live" --max-time $TIMEOUT 2>/dev/null || echo "000")
if [[ "$LIVE_STATUS" == "200" ]]; then
  ok "Liveness: $API_BASE/health/live → 200"
else
  fail "Liveness: $API_BASE/health/live → $LIVE_STATUS"
fi

READY_STATUS=$(curl -sf -o /dev/null -w "%{http_code}" "$API_BASE/health/ready" --max-time $TIMEOUT 2>/dev/null || echo "000")
if [[ "$READY_STATUS" == "200" ]]; then
  ok "Readiness: $API_BASE/health/ready → 200"
else
  warn "Readiness: $API_BASE/health/ready → $READY_STATUS (some dependencies may be missing)"
fi

echo ""

# --- Security Headers --------------------------------------------------------
echo -e "${BOLD}Security Headers${NC}"
echo ""

HEADERS=$(curl -sI "$API_BASE/health/live" --max-time $TIMEOUT 2>/dev/null || echo "")

check_header() {
  local name="$1"
  local expected="$2"
  if echo "$HEADERS" | grep -qi "$name"; then
    ok "$name header present"
  else
    if [[ -n "$expected" ]]; then
      fail "$name header MISSING (expected: $expected)"
    else
      warn "$name header missing"
    fi
  fi
}

check_header "X-Content-Type-Options" "nosniff"
check_header "X-Frame-Options" "DENY"
check_header "Referrer-Policy" ""
check_header "Strict-Transport-Security" ""

echo ""

# --- SSL Certificate --------------------------------------------------------
echo -e "${BOLD}SSL Certificate${NC}"
echo ""

SSL_INFO=$(curl -vI "$API_BASE/health/live" 2>&1 | grep -i "SSL certificate\|issuer\|expire" || echo "")
if [[ -n "$SSL_INFO" ]]; then
  ok "SSL certificate is valid"
else
  # curl succeeded (we got 200 above) so SSL is working even if verbose output is minimal
  if [[ "$LIVE_STATUS" == "200" ]]; then
    ok "SSL handshake successful (HTTPS working)"
  else
    warn "Could not verify SSL certificate details"
  fi
fi

echo ""

# --- Azure Secrets -----------------------------------------------------------
echo -e "${BOLD}Azure Container App Secrets${NC}"
echo ""

if az account show --output none 2>/dev/null; then
  SECRETS_JSON=$(az containerapp secret list \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --output json 2>/dev/null || echo "[]")

  EXPECTED_SECRETS=(
    "anthropic-api-key"
    "stripe-secret-key"
    "stripe-webhook-secret"
    "google-client-id"
    "google-client-secret"
    "cloudflare-api-token"
    "cloudflare-account-id"
    "scraper-api-key"
    "attom-api-key"
  )

  for secret in "${EXPECTED_SECRETS[@]}"; do
    if echo "$SECRETS_JSON" | python3 -c "
import sys, json
secrets = json.load(sys.stdin)
names = [s.get('name','') for s in secrets]
sys.exit(0 if '$secret' in names else 1)
" 2>/dev/null; then
      ok "Secret '$secret' exists"
    else
      fail "Secret '$secret' MISSING"
    fi
  done
else
  warn "Not logged into Azure CLI — skipping secret verification"
fi

echo ""

# --- DNS (custom domain only) -----------------------------------------------
if [[ -n "$CUSTOM_DOMAIN" ]]; then
  echo -e "${BOLD}DNS Resolution${NC}"
  echo ""

  for sub in "api" "platform" "www"; do
    RESOLVED=$(dig +short "$sub.$CUSTOM_DOMAIN" CNAME 2>/dev/null || echo "")
    if [[ -n "$RESOLVED" ]]; then
      ok "$sub.$CUSTOM_DOMAIN → $RESOLVED"
    else
      # Could be an A record or proxied
      A_RECORD=$(dig +short "$sub.$CUSTOM_DOMAIN" A 2>/dev/null || echo "")
      if [[ -n "$A_RECORD" ]]; then
        ok "$sub.$CUSTOM_DOMAIN → $A_RECORD (A record / proxied)"
      else
        fail "$sub.$CUSTOM_DOMAIN does not resolve"
      fi
    fi
  done

  echo ""
fi

# --- API Endpoints -----------------------------------------------------------
echo -e "${BOLD}API Functionality${NC}"
echo ""

# Test onboarding session creation
SESSION_RESPONSE=$(curl -sf -X POST "$API_BASE/onboarding/sessions" \
  -H "Content-Type: application/json" \
  -d '{"profileUrl": "https://www.zillow.com/profile/test-agent"}' \
  --max-time $TIMEOUT 2>/dev/null || echo "")

if [[ -n "$SESSION_RESPONSE" ]] && echo "$SESSION_RESPONSE" | python3 -c "import sys,json; json.load(sys.stdin)['sessionId']" > /dev/null 2>&1; then
  ok "Onboarding session creation works"
else
  warn "Onboarding session creation failed (may need secrets configured)"
fi

echo ""

# --- Platform (custom domain only) ------------------------------------------
if [[ -n "$PLATFORM_BASE" ]]; then
  echo -e "${BOLD}Platform${NC}"
  echo ""

  PLATFORM_STATUS=$(curl -sf -o /dev/null -w "%{http_code}" "$PLATFORM_BASE" --max-time $TIMEOUT 2>/dev/null || echo "000")
  if [[ "$PLATFORM_STATUS" == "200" ]]; then
    ok "Platform loads: $PLATFORM_BASE → 200"
  else
    warn "Platform returned $PLATFORM_STATUS (may not be deployed to Cloudflare Pages yet)"
  fi

  echo ""
fi

# --- Summary -----------------------------------------------------------------
TOTAL=$((PASS + FAIL + WARN_COUNT))
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "  ${BOLD}Results:${NC}  ${GREEN}$PASS passed${NC}  ${RED}$FAIL failed${NC}  ${YELLOW}$WARN_COUNT warnings${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

if [[ "$FAIL" -eq 0 ]]; then
  echo -e "  ${GREEN}${BOLD}Production looks good!${NC}"
else
  echo -e "  ${RED}${BOLD}$FAIL critical issue(s) need attention.${NC}"
fi

echo ""
exit "$FAIL"
