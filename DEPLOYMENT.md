#!/usr/bin/env pwsh

# RealEstateStar.Api Docker Deployment

## Files Created
- `./apps/api/Dockerfile` - Multi-stage build for .NET 10.0 ASP.NET Core API
- `./docker-compose.yml` - Service orchestration for local development
- `./.env.example` - Environment variable template
- `./.env` - Environment variables (update with real credentials)

## Quick Start

### 1. Configure Environment Variables
Edit `.env` and fill in the required API keys:
- Anthropic API key
- Attom API key
- Google OAuth credentials
- Stripe keys and webhook secret
- Platform base URL
- Optional: Cloudflare and ScraperApi keys

### 2. Build the Docker Image
```powershell
docker build -t realestatestar-api:latest ./apps/api
```
Image is already built and ready: `realestatestar-api:latest` (610MB)

### 3. Run with Docker Compose
```powershell
# Start the service
docker compose up -d

# View logs
docker compose logs -f api

# Stop the service
docker compose down
```

### 4. Verify Health
The API exposes two health endpoints:
- `/health/live` - Liveness probe (always passes if container running)
- `/health/ready` - Readiness probe (checks external dependencies: GWS CLI, Claude API)

```powershell
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
```

## Docker Image Details
- **Base Image**: mcr.microsoft.com/dotnet/aspnet:10.0 (runtime only)
- **Build Image**: mcr.microsoft.com/dotnet/sdk:10.0 (multi-stage)
- **Port**: 8080
- **User**: Non-root (appuser) for security
- **Size**: ~179MB (production image)
- **HEALTHCHECK**: Configured with 30s interval, 10s timeout

## Environment Variables (Required)
```
ANTHROPIC_API_KEY          - Anthropic Claude API key
ATTOM_API_KEY              - Attom Data API key
GOOGLE_CLIENT_ID           - Google OAuth client ID
GOOGLE_CLIENT_SECRET       - Google OAuth client secret
GOOGLE_REDIRECT_URI        - OAuth redirect (default: http://localhost:8080/auth/google/callback)
STRIPE_SECRET_KEY          - Stripe API secret
STRIPE_WEBHOOK_SECRET      - Stripe webhook secret
STRIPE_PRICE_ID            - Stripe price ID
PLATFORM_BASE_URL          - Base URL for API (default: http://localhost:8080)
```

## Environment Variables (Optional)
```
CLOUDFLARE_API_TOKEN       - Cloudflare API token for site deployment
CLOUDFLARE_ACCOUNT_ID      - Cloudflare account ID
SCRAPER_API_KEY            - ScraperApi key for profile scraping fallback
CORS_ORIGIN_1              - First allowed CORS origin (default: http://localhost:3000)
CORS_ORIGIN_2              - Second allowed CORS origin (default: http://localhost:3001)
```

## Deployment Architecture
```
┌─────────────────────────────────────────┐
│  Docker Compose (local development)     │
├─────────────────────────────────────────┤
│                                         │
│  ┌──────────────────────────────────┐  │
│  │   RealEstateStar.Api Container   │  │
│  ├──────────────────────────────────┤  │
│  │ .NET 10.0 ASP.NET Core Service   │  │
│  │ Port: 8080 (HTTP)                │  │
│  │ Volumes: Source code (dev bind)  │  │
│  │ Health: Liveness & Readiness     │  │
│  │ User: appuser (non-root)         │  │
│  └──────────────────────────────────┘  │
│                                         │
│  Network: realestatestar (bridge)       │
└─────────────────────────────────────────┘
```

## Production Deployment Notes
For production:
1. Remove volume bind mount from docker-compose (for code sync)
2. Use specific image version tags instead of `latest`
3. Configure proper CORS origins for your frontend domain
4. Use environment-specific .env files (e.g., .env.prod)
5. Consider using Kubernetes for multi-node deployments
6. Enable HTTPS with proper certificates (reverse proxy/ingress)
7. Set `ASPNETCORE_ENVIRONMENT=Production`
8. Configure external health check monitoring

## Dockerfile Overview
- **Stage 1 (builder)**: Compiles .NET 10.0 app using SDK image
- **Stage 2 (runtime)**: Copies published binaries to minimal runtime image
- **Optimizations**: 
  - Multi-stage reduces final image size by excluding build tools
  - Non-root user for security
  - Health check for container orchestration
  - curl installed for health checks

## Troubleshooting

### Container won't start
```powershell
docker compose logs api
# Check for missing environment variables or API key issues
```

### Port 8080 already in use
```powershell
# Change port mapping in docker-compose.yml
# From: "8080:8080"
# To:   "9999:8080"  (or another port)
docker compose up -d
```

### Health check failing
```powershell
# Check readiness probe details
curl http://localhost:8080/health/ready
# Verify Claude API and GWS CLI are accessible
```

### Rebuild image after code changes
```powershell
docker compose down
docker build -t realestatestar-api:latest ./apps/api
docker compose up -d
```
