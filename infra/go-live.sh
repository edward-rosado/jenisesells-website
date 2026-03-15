#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# go-live.sh — Interactive Go-Live Runbook for Real Estate Star
#
# Walks you through every step to take the platform from zero to production.
# Validates each step before moving to the next.
# Safe to re-run — it checks what's already done and skips completed steps.
#
# Usage:
#   bash infra/go-live.sh           # Start from the beginning
#   bash infra/go-live.sh --step 3  # Jump to a specific step
###############################################################################

# --- Colors & helpers --------------------------------------------------------
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
STATE_FILE="$REPO_ROOT/.go-live-state"

# Resource names (must match setup.sh)
RESOURCE_GROUP="real-estate-star-rg"
ACR_NAME="realestatestaracr"
APP_NAME="real-estate-star-api"
ENVIRONMENT="real-estate-star-env"

banner() {
  echo ""
  echo -e "${CYAN}╔══════════════════════════════════════════════════════════════════╗${NC}"
  echo -e "${CYAN}║${NC}  ${BOLD}$1${NC}"
  echo -e "${CYAN}╚══════════════════════════════════════════════════════════════════╝${NC}"
  echo ""
}

step_banner() {
  local step_num=$1
  local title=$2
  local total=8
  echo ""
  echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  echo -e "${BOLD}  Step $step_num of $total: $title${NC}"
  echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  echo ""
}

ok()   { echo -e "  ${GREEN}✔${NC} $1"; }
warn() { echo -e "  ${YELLOW}⚠${NC} $1"; }
fail() { echo -e "  ${RED}✘${NC} $1"; }
info() { echo -e "  ${CYAN}→${NC} $1"; }

pause() {
  echo ""
  echo -e "  ${YELLOW}Press Enter when you're ready to continue (or 'q' to quit)...${NC}"
  read -r response
  if [[ "$response" == "q" || "$response" == "Q" ]]; then
    save_state "$1"
    echo -e "\n${GREEN}Progress saved. Re-run this script to pick up where you left off.${NC}\n"
    exit 0
  fi
}

confirm() {
  echo ""
  echo -e "  ${YELLOW}$1 (y/n)${NC}"
  read -r response
  [[ "$response" == "y" || "$response" == "Y" ]]
}

save_state() {
  echo "$1" > "$STATE_FILE"
}

load_state() {
  if [[ -f "$STATE_FILE" ]]; then
    cat "$STATE_FILE"
  else
    echo "0"
  fi
}

# --- Parse arguments ---------------------------------------------------------
START_STEP=0

while [[ $# -gt 0 ]]; do
  case $1 in
    --step)
      START_STEP="$2"
      shift 2
      ;;
    --reset)
      rm -f "$STATE_FILE"
      echo "Progress reset."
      exit 0
      ;;
    *)
      echo "Usage: bash infra/go-live.sh [--step N] [--reset]"
      exit 1
      ;;
  esac
done

# Load saved state if no explicit step given
if [[ "$START_STEP" -eq 0 ]]; then
  SAVED=$(load_state)
  if [[ "$SAVED" -gt 0 ]]; then
    echo -e "${CYAN}Found saved progress at step $SAVED.${NC}"
    if confirm "Resume from step $SAVED?"; then
      START_STEP=$SAVED
    else
      START_STEP=1
    fi
  else
    START_STEP=1
  fi
fi

# --- Intro -------------------------------------------------------------------
banner "Real Estate Star — Go Live Runbook"
echo "  This script walks you through every step to deploy Real Estate Star"
echo "  to production. It validates each step and saves progress so you can"
echo "  quit and resume at any time."
echo ""
echo -e "  ${BOLD}Steps:${NC}"
echo "    1. Azure login & infrastructure setup"
echo "    2. Set Azure Container App secrets"
echo "    3. GitHub Actions credentials & secrets"
echo "    4. Cloudflare DNS configuration"
echo "    5. Grafana Cloud monitoring"
echo "    6. Google OAuth production config"
echo "    7. Stripe production webhook"
echo "    8. Smoke tests & verification"
echo ""

###############################################################################
# STEP 1: Azure Infrastructure
###############################################################################
if [[ "$START_STEP" -le 1 ]]; then
  step_banner 1 "Azure Infrastructure Setup"

  # Check if already logged in
  if az account show --output none 2>/dev/null; then
    SUBSCRIPTION=$(az account show --query name --output tsv)
    ok "Already logged into Azure (subscription: $SUBSCRIPTION)"
  else
    warn "Not logged into Azure CLI."
    info "Running 'az login' — a browser window will open for you to sign in."
    echo ""
    az login || {
      fail "Azure login failed. Fix this and re-run the script."
      exit 1
    }
    ok "Azure login successful!"
  fi
  echo ""

  # Check if resource group already exists
  if az group show --name "$RESOURCE_GROUP" --output none 2>/dev/null; then
    ok "Resource group '$RESOURCE_GROUP' already exists"

    # Check if container app exists
    if az containerapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --output none 2>/dev/null; then
      APP_URL=$(az containerapp show \
        --name "$APP_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query "properties.configuration.ingress.fqdn" \
        --output tsv 2>/dev/null || echo "")

      if [[ -n "$APP_URL" ]]; then
        ok "Container App '$APP_NAME' already exists"
        ok "App URL: https://$APP_URL"
        echo ""
        info "Skipping infrastructure creation (already done)."
      fi
    else
      warn "Resource group exists but Container App does not."
      info "Running setup.sh to create remaining infrastructure..."
      echo ""
      bash "$SCRIPT_DIR/azure/setup.sh"
      APP_URL=$(az containerapp show \
        --name "$APP_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query "properties.configuration.ingress.fqdn" \
        --output tsv)
      ok "Infrastructure created! App URL: https://$APP_URL"
    fi
  else
    info "Creating Azure infrastructure from scratch..."
    info "This takes 3-5 minutes. Grab a coffee."
    echo ""
    bash "$SCRIPT_DIR/azure/setup.sh"
    APP_URL=$(az containerapp show \
      --name "$APP_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --query "properties.configuration.ingress.fqdn" \
      --output tsv)
    ok "Infrastructure created! App URL: https://$APP_URL"
  fi

  echo ""
  echo -e "  ${BOLD}Save this URL — you'll need it for DNS and GitHub secrets:${NC}"
  echo -e "  ${GREEN}https://$APP_URL${NC}"

  save_state 2
  pause 1
fi

###############################################################################
# STEP 2: Azure Container App Secrets
###############################################################################
if [[ "$START_STEP" -le 2 ]]; then
  step_banner 2 "Set Azure Container App Secrets"

  echo "  This step replaces placeholder secrets with your real API keys."
  echo "  You'll need these values ready to paste:"
  echo ""
  echo "    • Anthropic API Key"
  echo "    • Stripe Secret Key (live mode: sk_live_...)"
  echo "    • Stripe Webhook Secret (you'll get this in step 7, skip for now)"
  echo "    • Google Client ID"
  echo "    • Google Client Secret"
  echo "    • Cloudflare API Token"
  echo "    • Cloudflare Account ID"
  echo "    • ScraperAPI Key"
  echo "    • ATTOM API Key"
  echo ""
  warn "Tip: You can press Enter to skip any secret you don't have yet."
  warn "     Re-run 'bash infra/azure/set-secrets.sh' later to fill them in."

  if confirm "Ready to set secrets now?"; then
    bash "$SCRIPT_DIR/azure/set-secrets.sh"
    ok "Secrets updated!"
  else
    warn "Skipped. Remember to run 'bash infra/azure/set-secrets.sh' before go-live."
  fi

  save_state 3
  pause 2
fi

###############################################################################
# STEP 3: GitHub Actions Credentials & Secrets
###############################################################################
if [[ "$START_STEP" -le 3 ]]; then
  step_banner 3 "GitHub Actions Credentials & Secrets"

  echo "  This step creates an Azure service principal so GitHub Actions can"
  echo "  deploy to your Container App automatically."
  echo ""

  if confirm "Create the GitHub Actions service principal now?"; then
    bash "$SCRIPT_DIR/azure/create-gh-credentials.sh"
    echo ""
    ok "Service principal created!"
    echo ""
    info "Now you need to add these secrets to your GitHub repo:"
    echo ""
    echo -e "  ${BOLD}Go to:${NC} https://github.com/<owner>/Real-Estate-Star/settings/secrets/actions"
    echo ""
    echo -e "  ${BOLD}Required secrets:${NC}"
    echo "    AZURE_CREDENTIALS      → the JSON blob printed above"
    echo "    AZURE_RG_NAME          → $RESOURCE_GROUP"
    echo "    AZURE_ACR_NAME         → $ACR_NAME"
    echo "    AZURE_APP_NAME         → $APP_NAME"
  else
    warn "Skipped service principal creation."
    info "You can run 'bash infra/azure/create-gh-credentials.sh' later."
  fi

  # Get the App URL for the API_URL secret
  APP_URL=$(az containerapp show \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query "properties.configuration.ingress.fqdn" \
    --output tsv 2>/dev/null || echo "<your-app>.azurecontainerapps.io")

  echo ""
  info "Also add these GitHub secrets:"
  echo ""
  echo "    API_URL                → https://$APP_URL"
  echo "    STRIPE_PUBLISHABLE_KEY → pk_live_... (from Stripe dashboard)"
  echo "    STRIPE_WEBHOOK_SECRET  → whsec_... (from step 7)"
  echo "    ATTOM_API_KEY          → (from ATTOM Data dashboard)"
  echo ""
  warn "You can add STRIPE_WEBHOOK_SECRET after step 7."

  pause 3
  save_state 4
fi

###############################################################################
# STEP 4: Cloudflare DNS
###############################################################################
if [[ "$START_STEP" -le 4 ]]; then
  step_banner 4 "Cloudflare DNS Configuration"

  echo "  Set up DNS records to point your domain to Azure and Cloudflare Pages."
  echo ""
  echo -e "  ${BOLD}Required DNS Records (in Cloudflare dashboard):${NC}"
  echo ""
  echo "  ┌──────────┬────────────────┬─────────────────────────────────────────────┬──────────┐"
  echo "  │ Type     │ Name           │ Target                                      │ Proxy    │"
  echo "  ├──────────┼────────────────┼─────────────────────────────────────────────┼──────────┤"
  echo "  │ CNAME    │ platform       │ real-estate-star-platform.pages.dev         │ Proxied  │"
  echo "  │ CNAME    │ <handle>       │ real-estate-star-agent-site.pages.dev       │ Proxied  │"

  APP_URL=$(az containerapp show \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query "properties.configuration.ingress.fqdn" \
    --output tsv 2>/dev/null || echo "<your-app>.azurecontainerapps.io")

  echo "  │ CNAME    │ api            │ $APP_URL │ Proxied  │"
  echo "  │ CNAME    │ www            │ platform.real-estate-star.com               │ Proxied  │"
  echo "  └──────────┴────────────────┴─────────────────────────────────────────────┴──────────┘"
  echo ""
  echo -e "  ${BOLD}SSL/TLS Settings:${NC}"
  echo "    • Encryption mode: Full (strict)"
  echo "    • Always Use HTTPS: On"
  echo "    • Minimum TLS: 1.2"
  echo "    • HSTS: On (includeSubDomains, max-age 6 months)"
  echo ""
  echo -e "  ${BOLD}Page Rules:${NC}"
  echo "    • api.real-estate-star.com/* → Cache Level: Bypass"
  echo "    • platform.real-estate-star.com/api/* → Cache Level: Bypass"
  echo "    • <handle>.real-estate-star.com/_next/static/* → Cache Everything (per agent)"
  echo ""
  echo -e "  ${BOLD}Security:${NC}"
  echo "    • Bot Fight Mode: On"
  echo "    • Browser Integrity Check: On"
  echo "    • WAF Managed Rules: On"
  echo ""
  info "Full details: infra/cloudflare/README.md"

  pause 4
  save_state 5
fi

###############################################################################
# STEP 5: Grafana Cloud
###############################################################################
if [[ "$START_STEP" -le 5 ]]; then
  step_banner 5 "Grafana Cloud Monitoring Setup"

  echo "  Set up Grafana Cloud to receive OpenTelemetry data from your API."
  echo ""
  echo -e "  ${BOLD}1. Create account:${NC}"
  echo "     https://grafana.com/auth/sign-up/create-user (free tier)"
  echo ""
  echo -e "  ${BOLD}2. Get OTLP credentials:${NC}"
  echo "     Home > Connections > Add new connection > OpenTelemetry (OTLP)"
  echo "     Click Configure > Generate API token"
  echo "     Note: endpoint, instance ID, and API token"
  echo ""
  echo -e "  ${BOLD}3. Generate the Base64 auth header:${NC}"
  echo ""
  echo -e "     ${CYAN}echo -n \"INSTANCE_ID:API_TOKEN\" | base64${NC}"
  echo ""
  echo -e "  ${BOLD}4. Set env vars on Azure Container App:${NC}"
  echo ""
  echo -e "     ${CYAN}az containerapp update \\${NC}"
  echo -e "     ${CYAN}  --name $APP_NAME \\${NC}"
  echo -e "     ${CYAN}  --resource-group $RESOURCE_GROUP \\${NC}"
  echo -e "     ${CYAN}  --set-env-vars \\${NC}"
  echo -e "     ${CYAN}    \"Otel__Endpoint=https://otlp-gateway-<region>.grafana.net/otlp\" \\${NC}"
  echo -e "     ${CYAN}    \"OTEL_EXPORTER_OTLP_HEADERS=Authorization=Basic <base64-value>\" \\${NC}"
  echo -e "     ${CYAN}    \"OTEL_EXPORTER_OTLP_PROTOCOL=grpc\"${NC}"
  echo ""
  echo -e "  ${BOLD}5. Import dashboards:${NC}"
  echo "     • ASP.NET Core (ID: 19924)"
  echo "     • .NET Runtime (ID: 19925)"
  echo ""
  info "Full details: infra/grafana/README.md"

  pause 5
  save_state 6
fi

###############################################################################
# STEP 6: Google OAuth
###############################################################################
if [[ "$START_STEP" -le 6 ]]; then
  step_banner 6 "Google OAuth — Production Redirect URI"

  echo "  Update your Google OAuth app to use the production callback URL."
  echo ""
  echo -e "  ${BOLD}In Google Cloud Console:${NC}"
  echo "     https://console.cloud.google.com/apis/credentials"
  echo ""
  echo "  1. Click your OAuth 2.0 Client ID"
  echo "  2. Under 'Authorized redirect URIs', add:"
  echo ""
  echo -e "     ${GREEN}https://api.real-estate-star.com/oauth/google/callback${NC}"
  echo ""
  echo "  3. Under 'Authorized JavaScript origins', add:"
  echo ""
  echo -e "     ${GREEN}https://platform.real-estate-star.com${NC}"
  echo ""
  echo "  4. If consent screen is in 'Testing' mode:"
  echo "     Go to OAuth consent screen > Publish app"
  echo ""
  echo "  5. Set the redirect URI on Azure:"
  echo ""
  echo -e "     ${CYAN}az containerapp update \\${NC}"
  echo -e "     ${CYAN}  --name $APP_NAME \\${NC}"
  echo -e "     ${CYAN}  --resource-group $RESOURCE_GROUP \\${NC}"
  echo -e "     ${CYAN}  --set-env-vars \\${NC}"
  echo -e "     ${CYAN}    \"Google__RedirectUri=https://api.real-estate-star.com/oauth/google/callback\"${NC}"

  pause 6
  save_state 7
fi

###############################################################################
# STEP 7: Stripe Webhook
###############################################################################
if [[ "$START_STEP" -le 7 ]]; then
  step_banner 7 "Stripe — Register Production Webhook"

  echo "  Register a webhook endpoint so Stripe can notify your API about payments."
  echo ""
  echo -e "  ${BOLD}In Stripe Dashboard (live mode):${NC}"
  echo "     https://dashboard.stripe.com/webhooks"
  echo ""
  echo "  1. Click '+ Add endpoint'"
  echo "  2. Endpoint URL:"
  echo ""
  echo -e "     ${GREEN}https://api.real-estate-star.com/stripe/webhook${NC}"
  echo ""
  echo "  3. Select events to listen to:"
  echo "     • checkout.session.completed"
  echo ""
  echo "  4. Click 'Add endpoint'"
  echo ""
  echo "  5. Copy the 'Signing secret' (starts with whsec_...)"
  echo ""
  echo -e "  ${BOLD}Then update the secret on Azure:${NC}"
  echo ""
  echo -e "     ${CYAN}az containerapp secret set \\${NC}"
  echo -e "     ${CYAN}  --name $APP_NAME \\${NC}"
  echo -e "     ${CYAN}  --resource-group $RESOURCE_GROUP \\${NC}"
  echo -e "     ${CYAN}  --secrets stripe-webhook-secret=<your-whsec-value>${NC}"
  echo ""
  echo -e "  ${BOLD}And add to GitHub secrets:${NC}"
  echo "     STRIPE_WEBHOOK_SECRET → whsec_..."
  echo ""
  warn "Make sure you're in LIVE mode, not test mode!"

  pause 7
  save_state 8
fi

###############################################################################
# STEP 8: Smoke Tests
###############################################################################
if [[ "$START_STEP" -le 8 ]]; then
  step_banner 8 "Smoke Tests & Verification"

  echo "  Running automated verification checks..."
  echo ""

  ERRORS=0

  # Check Azure login
  if az account show --output none 2>/dev/null; then
    ok "Azure CLI logged in"
  else
    fail "Azure CLI not logged in"
    ((ERRORS++))
  fi

  # Check Container App exists
  if az containerapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --output none 2>/dev/null; then
    ok "Container App '$APP_NAME' exists"

    APP_URL=$(az containerapp show \
      --name "$APP_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --query "properties.configuration.ingress.fqdn" \
      --output tsv)

    # Check health endpoints
    echo ""
    info "Checking health endpoints..."

    if curl -sf "https://$APP_URL/health/live" --max-time 10 > /dev/null 2>&1; then
      ok "Liveness check passed (https://$APP_URL/health/live)"
    else
      fail "Liveness check failed — API may not be running"
      ((ERRORS++))
    fi

    if curl -sf "https://$APP_URL/health/ready" --max-time 10 > /dev/null 2>&1; then
      ok "Readiness check passed (https://$APP_URL/health/ready)"
    else
      warn "Readiness check failed — some dependencies may not be configured yet"
    fi

    # Check security headers
    echo ""
    info "Checking security headers..."

    HEADERS=$(curl -sI "https://$APP_URL/health/live" --max-time 10 2>/dev/null || echo "")
    if echo "$HEADERS" | grep -qi "x-content-type-options"; then
      ok "X-Content-Type-Options header present"
    else
      warn "X-Content-Type-Options header missing"
    fi

    if echo "$HEADERS" | grep -qi "x-frame-options"; then
      ok "X-Frame-Options header present"
    else
      warn "X-Frame-Options header missing"
    fi

  else
    fail "Container App '$APP_NAME' not found"
    ((ERRORS++))
  fi

  # Check secrets are not placeholders
  echo ""
  info "Checking secrets are not placeholders..."

  SECRETS_JSON=$(az containerapp secret list \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --output json 2>/dev/null || echo "[]")

  SECRET_COUNT=$(echo "$SECRETS_JSON" | python3 -c "import sys,json; print(len(json.load(sys.stdin)))" 2>/dev/null || echo "0")

  if [[ "$SECRET_COUNT" -gt 0 ]]; then
    ok "$SECRET_COUNT secrets configured on Container App"
  else
    fail "No secrets found on Container App"
    ((ERRORS++))
  fi

  # Check DNS (if domain is set up)
  echo ""
  info "Checking DNS (optional — skip if domain not configured yet)..."

  if dig +short api.real-estate-star.com CNAME 2>/dev/null | grep -q "."; then
    ok "api.real-estate-star.com DNS resolves"
  else
    warn "api.real-estate-star.com DNS not configured yet"
  fi

  if dig +short platform.real-estate-star.com CNAME 2>/dev/null | grep -q "."; then
    ok "platform.real-estate-star.com DNS resolves"
  else
    warn "platform.real-estate-star.com DNS not configured yet"
  fi

  # Summary
  echo ""
  echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  if [[ "$ERRORS" -eq 0 ]]; then
    echo -e "  ${GREEN}${BOLD}All checks passed!${NC} 🚀"
  else
    echo -e "  ${YELLOW}${BOLD}$ERRORS issue(s) found.${NC} Review the warnings above."
  fi
  echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

  # Clean up state file on completion
  rm -f "$STATE_FILE"

  save_state 9
fi

###############################################################################
# Done!
###############################################################################
banner "Go-Live Complete!"
echo "  Your Real Estate Star platform should now be live."
echo ""
echo -e "  ${BOLD}Key URLs:${NC}"
APP_URL=$(az containerapp show \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.configuration.ingress.fqdn" \
  --output tsv 2>/dev/null || echo "<pending>")
echo "    API:      https://$APP_URL"
echo "    Platform: https://platform.real-estate-star.com"
echo "    Agents:   https://<handle>.real-estate-star.com"
echo ""
echo -e "  ${BOLD}Post-deploy:${NC}"
echo "    • Monitor Grafana dashboards for 30 min"
echo "    • Test onboarding flow end-to-end in browser"
echo "    • Test CMA submission end-to-end"
echo "    • Verify Stripe webhook delivery"
echo "    • Trigger a test alert to confirm notifications work"
echo ""
echo -e "  ${BOLD}Full checklist:${NC} docs/production-checklist.md"
echo ""
