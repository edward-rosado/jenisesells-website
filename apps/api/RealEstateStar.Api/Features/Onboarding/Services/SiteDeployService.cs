using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class SiteDeployService(ILogger<SiteDeployService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<string> DeployAsync(OnboardingSession session, CancellationToken ct)
    {
        var profile = session.Profile
            ?? throw new InvalidOperationException("Cannot deploy site without a scraped profile");

        var agentSlug = (profile.Name ?? "agent").ToLowerInvariant().Replace(" ", "-");
        var configDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config", "agents");
        Directory.CreateDirectory(configDir);

        var agentConfig = new
        {
            identity = new
            {
                name = profile.Name,
                phone = profile.Phone,
                email = profile.Email,
                brokerage = profile.Brokerage,
                licenseId = profile.LicenseId,
            },
            location = new
            {
                state = profile.State,
                serviceAreas = profile.ServiceAreas ?? [],
                officeAddress = profile.OfficeAddress,
            },
            branding = new
            {
                primaryColor = profile.PrimaryColor ?? "#1e40af",
                accentColor = profile.AccentColor ?? "#10b981",
                logoUrl = profile.LogoUrl,
            },
        };

        var configPath = Path.Combine(configDir, $"{agentSlug}.json");
        var json = JsonSerializer.Serialize(agentConfig, JsonOptions);
        await File.WriteAllTextAsync(configPath, json, ct);

        var siteUrl = $"https://{agentSlug}.realestatestar.com";
        session.SiteUrl = siteUrl;
        session.AgentConfigId = agentSlug;

        logger.LogInformation("Deployed site config for {AgentSlug} at {ConfigPath}", agentSlug, configPath);

        return siteUrl;
    }
}
