# CI/CD Pipeline Patterns

GitHub Actions CI/CD pipeline patterns for the Real Estate Star monorepo. Use this skill
when creating, modifying, or debugging deployment workflows.

## Monorepo Workflow Structure

Each app has TWO workflows:

1. **CI workflow** (`{app}.yml`) -- runs on push AND pull_request, lint + test + build
2. **Deploy workflow** (`deploy-{app}.yml`) -- runs on push to main ONLY, test then deploy

Both use `paths:` filters so only changes to the relevant app trigger the workflow.

## ESLint + Generated Code

**CRITICAL**: ESLint will lint everything in the working directory unless explicitly ignored.
Build tools that output generated code (OpenNext, Next.js, coverage reports) MUST be added
to the ESLint ignore config.

```javascript
// eslint.config.mjs
globalIgnores([
  ".next/**",
  ".open-next/**",    // OpenNext build output
  "out/**",
  "build/**",
  "coverage/**",      // Test coverage reports
  "next-env.d.ts",
])
```

If ESLint fails in CI with thousands of errors in minified files, the generated build output
is being linted. Check the file paths in the error output -- if they reference `.open-next/`
or `coverage/`, add them to globalIgnores.

## React 19 Lint Rules

React 19 + eslint-config-next introduces `react-hooks/set-state-in-effect` which flags
`setState()` calls inside `useEffect`. This is a new ERROR (not warning).

**Bad (fails lint):**
```tsx
useEffect(() => {
  const value = localStorage.getItem("key");
  if (value) setValue(true);  // ERROR: setState in effect
}, []);
```

**Good (use useSyncExternalStore for external state):**
```tsx
import { useSyncExternalStore } from "react";

function getSnapshot() { return localStorage.getItem("key"); }
function getServerSnapshot() { return null; }
function subscribe(cb: () => void) {
  window.addEventListener("storage", cb);
  return () => window.removeEventListener("storage", cb);
}

const value = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
```

## Platform Deploy Workflow (Cloudflare Workers)

```yaml
# .github/workflows/deploy-platform.yml
name: Deploy Platform

on:
  push:
    branches: [main]
    paths:
      - 'apps/platform/**'
      - '.github/workflows/deploy-platform.yml'

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: apps/platform/package-lock.json
      - run: npm ci --prefix apps/platform
      - run: npm run lint --prefix apps/platform
      - run: npm run test:coverage --prefix apps/platform

  deploy:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: apps/platform/package-lock.json
      - run: npm ci --prefix apps/platform
      - name: Build with OpenNext
        working-directory: apps/platform
        env:
          NEXT_PUBLIC_API_URL: ${{ secrets.API_URL }}
        run: npx opennextjs-cloudflare build
      - name: Deploy to Cloudflare Workers
        uses: cloudflare/wrangler-action@v3
        with:
          apiToken: ${{ secrets.CLOUDFLARE_API_TOKEN }}
          accountId: ${{ secrets.CLOUDFLARE_ACCOUNT_ID }}
          workingDirectory: apps/platform
          command: deploy
```

### Key Points

- **Use `wrangler deploy` NOT `pages deploy`** -- this project is a Cloudflare Worker, not
  Pages. Using `pages deploy` will fail or deploy to the wrong target.
- **OpenNext build runs `next build` internally** -- do NOT run `npm run build` separately
  before `npx opennextjs-cloudflare build`. That doubles the build time for no benefit.
- **CI runs on ubuntu-latest (Linux)** -- the Docker workaround for Turbopack bracket
  filenames is only needed on Windows. Linux handles brackets in filenames natively.
- **Environment variables go on the OpenNext build step** -- `NEXT_PUBLIC_*` vars are baked
  in at build time, not runtime. Set them as `env:` on the build step.

## Required GitHub Secrets

| Secret | Purpose | Example |
|--------|---------|---------|
| `CLOUDFLARE_API_TOKEN` | Cloudflare Workers deploy | Custom token with Pages Edit, Workers Edit, Account Settings Read |
| `CLOUDFLARE_ACCOUNT_ID` | Cloudflare account | `7674efd9381763796f39ea67fe5e0505` |
| `API_URL` | Production API URL | `https://api.real-estate-star.com` |
| `AZURE_CREDENTIALS` | Azure service principal JSON (for API deploy) | `{"clientId":"...","clientSecret":"...","subscriptionId":"...","tenantId":"..."}` |
| `ANTHROPIC_API_KEY` | Claude API key (for health checks) | `sk-ant-...` |
| `ANTHROPIC__APIKEY` | Claude API key (alternate format) | `sk-ant-...` |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID | `...apps.googleusercontent.com` |
| `GOOGLE_CLIENT_SECRET` | Google OAuth client secret | |
| `SCRAPER_API_KEY` | ScraperAPI key (for web scraping) | |
| `STRIPE_SECRET_KEY` | Stripe secret key | `sk_test_...` or `sk_live_...` |
| `STRIPE_PUBLISHABLE_KEY` | Stripe public key | `pk_live_...` or `pk_test_...` |

## API Deploy Workflow (Azure Container Apps)

The API uses a separate workflow (`deploy-api.yml`) that builds a Docker image and deploys
to Azure Container Apps.

```yaml
# .github/workflows/deploy-api.yml
name: Deploy API

on:
  push:
    branches: [main]
    paths:
      - 'apps/api/**'
      - '.github/workflows/deploy-api.yml'

env:
  ACR_NAME: realestatestar
  CONTAINER_APP_NAME: real-estate-star-api
  RESOURCE_GROUP: real-estate-star-rg
  IMAGE_NAME: real-estate-star-api

jobs:
  test:
    name: "API -- build and test"
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          dotnet-quality: 'preview'
      - run: dotnet restore apps/api/RealEstateStar.Api.sln
      - run: dotnet build apps/api/RealEstateStar.Api.sln --no-restore --configuration Release
      - run: >
          dotnet test apps/api/RealEstateStar.Api.sln
          --no-build --configuration Release
          --logger "console;verbosity=minimal"

  deploy:
    name: "Deploy to Azure Container Apps"
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    permissions:
      contents: read
      id-token: write
    steps:
      - uses: actions/checkout@v4
      - name: Azure Login
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      - name: Build and push image to ACR
        run: |
          az acr build \
            --registry ${{ env.ACR_NAME }} \
            --image ${{ env.IMAGE_NAME }}:${{ github.sha }} \
            --file apps/api/Dockerfile \
            apps/api
      - name: Deploy to Container App
        run: |
          az containerapp update \
            --name ${{ env.CONTAINER_APP_NAME }} \
            --resource-group ${{ env.RESOURCE_GROUP }} \
            --image ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:${{ github.sha }}
      - name: Verify deployment
        run: |
          FQDN=$(az containerapp show \
            --name ${{ env.CONTAINER_APP_NAME }} \
            --resource-group ${{ env.RESOURCE_GROUP }} \
            --query "properties.configuration.ingress.fqdn" --output tsv)
          echo "Deployed URL: https://${FQDN}"
          for i in $(seq 1 30); do
            STATUS=$(curl -s -o /dev/null -w "%{http_code}" "https://${FQDN}/health/live" || true)
            if [ "$STATUS" = "200" ]; then echo "Health check passed"; exit 0; fi
            echo "Attempt $i/30: $STATUS, retrying in 10s..."; sleep 10
          done
          echo "Health check failed after 30 attempts"; exit 1
      - name: Rollback on failure
        if: failure()
        run: |
          PREV=$(az containerapp revision list \
            --name ${{ env.CONTAINER_APP_NAME }} \
            --resource-group ${{ env.RESOURCE_GROUP }} \
            --query "[1].name" -o tsv)
          if [ -n "$PREV" ]; then
            az containerapp ingress traffic set \
              --name ${{ env.CONTAINER_APP_NAME }} \
              --resource-group ${{ env.RESOURCE_GROUP }} \
              --revision-weight "$PREV=100"
          fi
```

### Key Points -- API Deploy

- **No `workflow_dispatch` trigger** -- deploy only runs on push to `main`. You cannot manually
  re-trigger it from the GitHub UI or `gh workflow run`. To test, push a change to `apps/api/`
  on main.
- **`az acr build` uses ACR Tasks** -- requires ACR Basic tier or higher (NOT Free/F0). If on
  Free tier, switch to local `docker build` + `az acr login` + `docker push` on the runner.
- **AZURE_CREDENTIALS must be a service principal JSON** -- created with
  `az ad sp create-for-rbac`. The JSON contains clientId, clientSecret, subscriptionId, tenantId.
- **Deploy job has `if: github.ref == 'refs/heads/main'`** -- even if the workflow triggers,
  the deploy job is skipped on non-main branches.
- **Rollback step** -- on failure, traffic shifts to the previous Container App revision
  automatically. No manual intervention needed.
- **Health check verification** -- polls `/health/live` up to 30 times (5 min) before declaring
  failure.

## Common Failures

### 1. ESLint fails with thousands of errors
Generated build output is being linted. Add the output directory to `globalIgnores` in
`eslint.config.mjs`.

### 2. OpenNext build fails with "not compatible with Windows"
Only happens locally. CI uses Linux. For local builds, the deploy script at
`infra/cloudflare/deploy-platform.ps1` uses Docker to build inside a Linux container.

### 3. wrangler deploy fails with "non-interactive environment"
`CLOUDFLARE_API_TOKEN` secret is missing or has wrong permissions. Create a Custom Token
at https://dash.cloudflare.com/profile/api-tokens with Pages Edit, Workers Edit,
Account Settings Read.

### 4. Deploy succeeds but site returns 500
Check `wrangler tail --format pretty` for the actual error. Most common cause on Windows:
Turbopack bracket filenames not bundled. Should not happen in CI (Linux).

### 5. Deploy API fails at Azure Login
`AZURE_CREDENTIALS` secret is missing or malformed. Must be a JSON object with clientId,
clientSecret, subscriptionId, tenantId from `az ad sp create-for-rbac --sdk-auth`.

### 6. `az acr build` fails with "SKU does not support ACR Tasks"
ACR Free/F0 tier doesn't support ACR Tasks. Switch to local Docker build on the runner:
```yaml
- run: az acr login --name ${{ env.ACR_NAME }}
- run: docker build -t ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:${{ github.sha }} -f apps/api/Dockerfile apps/api
- run: docker push ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:${{ github.sha }}
```

### 7. Build bakes in localhost API URL
`NEXT_PUBLIC_API_URL` must be set as an env var on the build step. If `.env.local` exists
locally, the deploy script moves it aside during build.

## Testing a Pipeline Change

1. Make a small visible change (e.g., version tag in footer)
2. Push to main
3. Watch the workflow at: `gh run list --limit 3`
4. Verify the change appears on the live site
