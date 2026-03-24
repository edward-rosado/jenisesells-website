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
COPY ["RealEstateStar.Workers.Shared/RealEstateStar.Workers.Shared.csproj", "RealEstateStar.Workers.Shared/"]
COPY ["RealEstateStar.Workers.Leads/RealEstateStar.Workers.Leads.csproj", "RealEstateStar.Workers.Leads/"]
COPY ["RealEstateStar.Workers.Cma/RealEstateStar.Workers.Cma.csproj", "RealEstateStar.Workers.Cma/"]
COPY ["RealEstateStar.Workers.HomeSearch/RealEstateStar.Workers.HomeSearch.csproj", "RealEstateStar.Workers.HomeSearch/"]
COPY ["RealEstateStar.Workers.WhatsApp/RealEstateStar.Workers.WhatsApp.csproj", "RealEstateStar.Workers.WhatsApp/"]
COPY ["RealEstateStar.Clients.Anthropic/RealEstateStar.Clients.Anthropic.csproj", "RealEstateStar.Clients.Anthropic/"]
COPY ["RealEstateStar.Clients.Scraper/RealEstateStar.Clients.Scraper.csproj", "RealEstateStar.Clients.Scraper/"]
COPY ["RealEstateStar.Clients.WhatsApp/RealEstateStar.Clients.WhatsApp.csproj", "RealEstateStar.Clients.WhatsApp/"]
COPY ["RealEstateStar.Clients.GDrive/RealEstateStar.Clients.GDrive.csproj", "RealEstateStar.Clients.GDrive/"]
COPY ["RealEstateStar.Clients.Gmail/RealEstateStar.Clients.Gmail.csproj", "RealEstateStar.Clients.Gmail/"]
COPY ["RealEstateStar.Clients.GoogleOAuth/RealEstateStar.Clients.GoogleOAuth.csproj", "RealEstateStar.Clients.GoogleOAuth/"]
COPY ["RealEstateStar.Clients.GDocs/RealEstateStar.Clients.GDocs.csproj", "RealEstateStar.Clients.GDocs/"]
COPY ["RealEstateStar.Clients.GSheets/RealEstateStar.Clients.GSheets.csproj", "RealEstateStar.Clients.GSheets/"]
COPY ["RealEstateStar.Clients.Stripe/RealEstateStar.Clients.Stripe.csproj", "RealEstateStar.Clients.Stripe/"]
COPY ["RealEstateStar.Clients.Cloudflare/RealEstateStar.Clients.Cloudflare.csproj", "RealEstateStar.Clients.Cloudflare/"]
COPY ["RealEstateStar.Clients.Turnstile/RealEstateStar.Clients.Turnstile.csproj", "RealEstateStar.Clients.Turnstile/"]
COPY ["RealEstateStar.Clients.Azure/RealEstateStar.Clients.Azure.csproj", "RealEstateStar.Clients.Azure/"]
COPY ["RealEstateStar.Clients.Gws/RealEstateStar.Clients.Gws.csproj", "RealEstateStar.Clients.Gws/"]

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
