# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /src

# Copy solution and all project files for restore layer caching
COPY ["RealEstateStar.Api.sln", "./"]
COPY ["RealEstateStar.Api/RealEstateStar.Api.csproj", "RealEstateStar.Api/"]
COPY ["RealEstateStar.Domain/RealEstateStar.Domain.csproj", "RealEstateStar.Domain/"]
COPY ["RealEstateStar.Data/RealEstateStar.Data.csproj", "RealEstateStar.Data/"]
COPY ["RealEstateStar.DataServices/RealEstateStar.DataServices.csproj", "RealEstateStar.DataServices/"]
COPY ["RealEstateStar.Notifications/RealEstateStar.Notifications.csproj", "RealEstateStar.Notifications/"]
COPY ["workers/RealEstateStar.Workers.Shared/RealEstateStar.Workers.Shared.csproj", "workers/RealEstateStar.Workers.Shared/"]
COPY ["workers/RealEstateStar.Workers.Leads/RealEstateStar.Workers.Leads.csproj", "workers/RealEstateStar.Workers.Leads/"]
COPY ["workers/RealEstateStar.Workers.Cma/RealEstateStar.Workers.Cma.csproj", "workers/RealEstateStar.Workers.Cma/"]
COPY ["workers/RealEstateStar.Workers.HomeSearch/RealEstateStar.Workers.HomeSearch.csproj", "workers/RealEstateStar.Workers.HomeSearch/"]
COPY ["workers/RealEstateStar.Workers.WhatsApp/RealEstateStar.Workers.WhatsApp.csproj", "workers/RealEstateStar.Workers.WhatsApp/"]
COPY ["clients/RealEstateStar.Clients.Anthropic/RealEstateStar.Clients.Anthropic.csproj", "clients/RealEstateStar.Clients.Anthropic/"]
COPY ["clients/RealEstateStar.Clients.Scraper/RealEstateStar.Clients.Scraper.csproj", "clients/RealEstateStar.Clients.Scraper/"]
COPY ["clients/RealEstateStar.Clients.WhatsApp/RealEstateStar.Clients.WhatsApp.csproj", "clients/RealEstateStar.Clients.WhatsApp/"]
COPY ["clients/RealEstateStar.Clients.GDrive/RealEstateStar.Clients.GDrive.csproj", "clients/RealEstateStar.Clients.GDrive/"]
COPY ["clients/RealEstateStar.Clients.Gmail/RealEstateStar.Clients.Gmail.csproj", "clients/RealEstateStar.Clients.Gmail/"]
COPY ["clients/RealEstateStar.Clients.GoogleOAuth/RealEstateStar.Clients.GoogleOAuth.csproj", "clients/RealEstateStar.Clients.GoogleOAuth/"]
COPY ["clients/RealEstateStar.Clients.GDocs/RealEstateStar.Clients.GDocs.csproj", "clients/RealEstateStar.Clients.GDocs/"]
COPY ["clients/RealEstateStar.Clients.GSheets/RealEstateStar.Clients.GSheets.csproj", "clients/RealEstateStar.Clients.GSheets/"]
COPY ["clients/RealEstateStar.Clients.Stripe/RealEstateStar.Clients.Stripe.csproj", "clients/RealEstateStar.Clients.Stripe/"]
COPY ["clients/RealEstateStar.Clients.Cloudflare/RealEstateStar.Clients.Cloudflare.csproj", "clients/RealEstateStar.Clients.Cloudflare/"]
COPY ["clients/RealEstateStar.Clients.Turnstile/RealEstateStar.Clients.Turnstile.csproj", "clients/RealEstateStar.Clients.Turnstile/"]
COPY ["clients/RealEstateStar.Clients.Azure/RealEstateStar.Clients.Azure.csproj", "clients/RealEstateStar.Clients.Azure/"]
COPY ["clients/RealEstateStar.Clients.Gws/RealEstateStar.Clients.Gws.csproj", "clients/RealEstateStar.Clients.Gws/"]
COPY ["clients/RealEstateStar.Clients.RentCast/RealEstateStar.Clients.RentCast.csproj", "clients/RealEstateStar.Clients.RentCast/"]

# Restore dependencies (cached layer — only invalidated when csproj files change)
RUN dotnet restore "RealEstateStar.Api/RealEstateStar.Api.csproj"

# Copy source code (tests excluded by .dockerignore)
COPY . .

# Build and publish
RUN dotnet publish "RealEstateStar.Api/RealEstateStar.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=builder /app/publish .

# Copy agent account configs (placed in build context by CI)
COPY config-accounts/ /app/config/accounts/

# Non-root user for security
RUN useradd -m appuser && chown -R appuser:appuser /app
USER appuser

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "RealEstateStar.Api.dll"]
