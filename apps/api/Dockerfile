# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder

WORKDIR /src

# Copy solution and all project files for restore layer caching
COPY ["RealEstateStar.Api.sln", "./"]
COPY ["RealEstateStar.Api/RealEstateStar.Api.csproj", "RealEstateStar.Api/"]
COPY ["RealEstateStar.Domain/RealEstateStar.Domain.csproj", "RealEstateStar.Domain/"]
COPY ["RealEstateStar.Data/RealEstateStar.Data.csproj", "RealEstateStar.Data/"]
COPY ["RealEstateStar.DataServices/RealEstateStar.DataServices.csproj", "RealEstateStar.DataServices/"]
COPY ["RealEstateStar.Workers/RealEstateStar.Workers.Shared/RealEstateStar.Workers.Shared.csproj", "RealEstateStar.Workers/RealEstateStar.Workers.Shared/"]
COPY ["RealEstateStar.Workers/Lead/RealEstateStar.Workers.Lead.CMA/RealEstateStar.Workers.Lead.CMA.csproj", "RealEstateStar.Workers/Lead/RealEstateStar.Workers.Lead.CMA/"]
COPY ["RealEstateStar.Workers/Lead/RealEstateStar.Workers.Lead.HomeSearch/RealEstateStar.Workers.Lead.HomeSearch.csproj", "RealEstateStar.Workers/Lead/RealEstateStar.Workers.Lead.HomeSearch/"]
COPY ["RealEstateStar.Workers/RealEstateStar.Workers.WhatsApp/RealEstateStar.Workers.WhatsApp.csproj", "RealEstateStar.Workers/RealEstateStar.Workers.WhatsApp/"]
COPY ["RealEstateStar.Activities/RealEstateStar.Activities.Pdf/RealEstateStar.Activities.Pdf.csproj", "RealEstateStar.Activities/RealEstateStar.Activities.Pdf/"]
COPY ["RealEstateStar.Activities/Lead/RealEstateStar.Activities.Lead.Persist/RealEstateStar.Activities.Lead.Persist.csproj", "RealEstateStar.Activities/Lead/RealEstateStar.Activities.Lead.Persist/"]
COPY ["RealEstateStar.Services/RealEstateStar.Services.AgentNotifier/RealEstateStar.Services.AgentNotifier.csproj", "RealEstateStar.Services/RealEstateStar.Services.AgentNotifier/"]
COPY ["RealEstateStar.Services/RealEstateStar.Services.LeadCommunicator/RealEstateStar.Services.LeadCommunicator.csproj", "RealEstateStar.Services/RealEstateStar.Services.LeadCommunicator/"]
COPY ["RealEstateStar.Workers/Lead/RealEstateStar.Workers.Lead.Orchestrator/RealEstateStar.Workers.Lead.Orchestrator.csproj", "RealEstateStar.Workers/Lead/RealEstateStar.Workers.Lead.Orchestrator/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.Anthropic/RealEstateStar.Clients.Anthropic.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.Anthropic/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.Scraper/RealEstateStar.Clients.Scraper.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.Scraper/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.WhatsApp/RealEstateStar.Clients.WhatsApp.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.WhatsApp/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.GDrive/RealEstateStar.Clients.GDrive.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.GDrive/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.Gmail/RealEstateStar.Clients.Gmail.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.Gmail/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.GoogleOAuth/RealEstateStar.Clients.GoogleOAuth.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.GoogleOAuth/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.GDocs/RealEstateStar.Clients.GDocs.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.GDocs/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.GSheets/RealEstateStar.Clients.GSheets.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.GSheets/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.Stripe/RealEstateStar.Clients.Stripe.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.Stripe/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.Cloudflare/RealEstateStar.Clients.Cloudflare.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.Cloudflare/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.Turnstile/RealEstateStar.Clients.Turnstile.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.Turnstile/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.Azure/RealEstateStar.Clients.Azure.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.Azure/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.Gws/RealEstateStar.Clients.Gws.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.Gws/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.RentCast/RealEstateStar.Clients.RentCast.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.RentCast/"]
COPY ["RealEstateStar.Clients/RealEstateStar.Clients.Zillow/RealEstateStar.Clients.Zillow.csproj", "RealEstateStar.Clients/RealEstateStar.Clients.Zillow/"]
# Activation workers
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.Orchestrator/RealEstateStar.Workers.Activation.Orchestrator.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.Orchestrator/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.AgentDiscovery/RealEstateStar.Workers.Activation.AgentDiscovery.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.AgentDiscovery/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.BrandExtraction/RealEstateStar.Workers.Activation.BrandExtraction.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.BrandExtraction/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.BrandingDiscovery/RealEstateStar.Workers.Activation.BrandingDiscovery.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.BrandingDiscovery/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.BrandVoice/RealEstateStar.Workers.Activation.BrandVoice.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.BrandVoice/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.CmaStyle/RealEstateStar.Workers.Activation.CmaStyle.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.CmaStyle/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.Coaching/RealEstateStar.Workers.Activation.Coaching.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.Coaching/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.ComplianceAnalysis/RealEstateStar.Workers.Activation.ComplianceAnalysis.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.ComplianceAnalysis/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.DriveIndex/RealEstateStar.Workers.Activation.DriveIndex.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.DriveIndex/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.EmailFetch/RealEstateStar.Workers.Activation.EmailFetch.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.EmailFetch/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.FeeStructure/RealEstateStar.Workers.Activation.FeeStructure.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.FeeStructure/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.MarketingStyle/RealEstateStar.Workers.Activation.MarketingStyle.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.MarketingStyle/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.Personality/RealEstateStar.Workers.Activation.Personality.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.Personality/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.PipelineAnalysis/RealEstateStar.Workers.Activation.PipelineAnalysis.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.PipelineAnalysis/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.VoiceExtraction/RealEstateStar.Workers.Activation.VoiceExtraction.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.VoiceExtraction/"]
COPY ["RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.WebsiteStyle/RealEstateStar.Workers.Activation.WebsiteStyle.csproj", "RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.WebsiteStyle/"]
# Activation activities and services
COPY ["RealEstateStar.Activities/Activation/RealEstateStar.Activities.Activation.PersistAgentProfile/RealEstateStar.Activities.Activation.PersistAgentProfile.csproj", "RealEstateStar.Activities/Activation/RealEstateStar.Activities.Activation.PersistAgentProfile/"]
COPY ["RealEstateStar.Activities/Activation/RealEstateStar.Activities.Activation.BrandMerge/RealEstateStar.Activities.Activation.BrandMerge.csproj", "RealEstateStar.Activities/Activation/RealEstateStar.Activities.Activation.BrandMerge/"]
COPY ["RealEstateStar.Activities/Activation/RealEstateStar.Activities.Activation.ContactImportPersist/RealEstateStar.Activities.Activation.ContactImportPersist.csproj", "RealEstateStar.Activities/Activation/RealEstateStar.Activities.Activation.ContactImportPersist/"]
COPY ["RealEstateStar.Activities/Lead/RealEstateStar.Activities.Lead.ContactDetection/RealEstateStar.Activities.Lead.ContactDetection.csproj", "RealEstateStar.Activities/Lead/RealEstateStar.Activities.Lead.ContactDetection/"]
COPY ["RealEstateStar.Services/Activation/RealEstateStar.Services.AgentConfig/RealEstateStar.Services.AgentConfig.csproj", "RealEstateStar.Services/Activation/RealEstateStar.Services.AgentConfig/"]
COPY ["RealEstateStar.Services/Activation/RealEstateStar.Services.BrandMerge/RealEstateStar.Services.BrandMerge.csproj", "RealEstateStar.Services/Activation/RealEstateStar.Services.BrandMerge/"]
COPY ["RealEstateStar.Services/Activation/RealEstateStar.Services.WelcomeNotification/RealEstateStar.Services.WelcomeNotification.csproj", "RealEstateStar.Services/Activation/RealEstateStar.Services.WelcomeNotification/"]

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
FROM mcr.microsoft.com/dotnet/aspnet:9.0

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
