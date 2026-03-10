using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public partial class SiteDeployService(
    ILogger<SiteDeployService> logger,
    IProcessRunner processRunner,
    CloudflareOptions cloudflareOptions,
    string configDirectory) : ISiteDeployService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly TimeSpan DeployTimeout = TimeSpan.FromSeconds(60);

    [GeneratedRegex(@"https://[a-z0-9\-]+\.real-estate-star-agents\.pages\.dev")]
    private static partial Regex PreviewUrlPattern();

    [GeneratedRegex(@"^[a-z0-9\-]+$")]
    private static partial Regex SlugPattern();

    public async Task<string> DeployAsync(OnboardingSession session, CancellationToken ct)
    {
        var profile = session.Profile
            ?? throw new InvalidOperationException("Cannot deploy site without a scraped profile");

        var agentSlug = GenerateSlug(profile.Name);

        // Step 1: Write agent config JSON
        await WriteAgentConfigAsync(agentSlug, profile, ct);
        session.AgentConfigId = agentSlug;

        // Step 2: Run Wrangler to deploy to Cloudflare Pages
        var siteUrl = await RunWranglerDeployAsync(agentSlug, ct);

        session.SiteUrl = siteUrl;
        logger.LogInformation("Deployed site for {AgentSlug} at {SiteUrl}", agentSlug, siteUrl);

        return siteUrl;
    }

    internal static string GenerateSlug(string? name)
    {
        var slug = (name ?? "agent").ToLowerInvariant().Replace(" ", "-");
        // Sanitize: only allow lowercase letters, digits, hyphens
        slug = string.Concat(slug.Where(c => char.IsLetterOrDigit(c) || c == '-'));
        return string.IsNullOrEmpty(slug) ? "agent" : slug;
    }

    private async Task WriteAgentConfigAsync(string agentSlug, ScrapedProfile profile, CancellationToken ct)
    {
        Directory.CreateDirectory(configDirectory);

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

        var configPath = Path.GetFullPath(Path.Combine(configDirectory, $"{agentSlug}.json"));

        // Path traversal protection: ensure config path stays within config directory
        var canonicalConfigDir = Path.GetFullPath(configDirectory);
        if (!configPath.StartsWith(canonicalConfigDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid agent slug — path traversal detected");

        var json = JsonSerializer.Serialize(agentConfig, JsonOptions);
        await File.WriteAllTextAsync(configPath, json, ct);

        logger.LogInformation("Wrote agent config for {AgentSlug} at {ConfigPath}", agentSlug, configPath);
    }

    private async Task<string> RunWranglerDeployAsync(string agentSlug, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("npx");

        psi.ArgumentList.Add("wrangler");
        psi.ArgumentList.Add("pages");
        psi.ArgumentList.Add("deploy");
        psi.ArgumentList.Add("apps/agent-site/.next");
        psi.ArgumentList.Add("--project-name");
        psi.ArgumentList.Add("real-estate-star-agents");
        psi.ArgumentList.Add("--branch");
        psi.ArgumentList.Add(agentSlug);

        // Pass Cloudflare credentials via environment variables (not CLI args)
        psi.Environment["CLOUDFLARE_API_TOKEN"] = cloudflareOptions.ApiToken;
        psi.Environment["CLOUDFLARE_ACCOUNT_ID"] = cloudflareOptions.AccountId;

        var result = await processRunner.RunAsync(psi, DeployTimeout, ct);

        if (result.ExitCode != 0)
        {
            logger.LogError("Wrangler deploy failed with exit code {ExitCode}: {Stderr}",
                result.ExitCode, result.Stderr);
            throw new InvalidOperationException(
                $"Site deploy failed (exit code {result.ExitCode}). Check logs for details.");
        }

        // Parse preview URL from Wrangler output
        var match = PreviewUrlPattern().Match(result.Stdout);
        if (match.Success)
            return match.Value;

        // Fallback to convention-based URL
        var fallbackUrl = $"https://{agentSlug}.real-estate-star-agents.pages.dev";
        logger.LogWarning("Could not parse preview URL from Wrangler output, falling back to {FallbackUrl}", fallbackUrl);
        return fallbackUrl;
    }
}
