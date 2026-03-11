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
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly TimeSpan BuildTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan DeployTimeout = TimeSpan.FromSeconds(60);

    [GeneratedRegex(@"https://[a-z0-9\-]+\.real-estate-star-agents\.pages\.dev")]
    private static partial Regex PreviewUrlPattern();

    [GeneratedRegex(@"^[a-z0-9\-]+$")]
    private static partial Regex SlugPattern();

    public async Task<string> DeployAsync(OnboardingSession session, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cloudflareOptions.ApiToken))
            throw new InvalidOperationException("Cloudflare:ApiToken is not configured — cannot deploy site");
        if (string.IsNullOrWhiteSpace(cloudflareOptions.AccountId))
            throw new InvalidOperationException("Cloudflare:AccountId is not configured — cannot deploy site");

        var profile = session.Profile
            ?? throw new InvalidOperationException("Cannot deploy site without a scraped profile");

        var agentSlug = OnboardingHelpers.GenerateSlug(profile.Name);

        // Step 1: Write agent config JSON (config/agents/{slug}.json)
        await WriteAgentConfigAsync(agentSlug, profile, ct);
        session.AgentConfigId = agentSlug;

        // Step 2: Write agent content JSON (config/agents/{slug}.content.json)
        await WriteAgentContentAsync(agentSlug, profile, ct);

        // Step 3: Build Next.js for Cloudflare using OpenNext adapter
        await RunNextBuildAsync(ct);

        // Step 4: Build OpenNext worker + assets bundle
        await RunOpenNextBuildAsync(ct);

        // Step 5: Deploy to Cloudflare Pages via Wrangler
        var siteUrl = await RunWranglerDeployAsync(agentSlug, ct);

        session.SiteUrl = siteUrl;
        logger.LogInformation("Deployed site for {AgentSlug} at {SiteUrl}", agentSlug, siteUrl);

        return siteUrl;
    }

    private async Task WriteAgentConfigAsync(string agentSlug, ScrapedProfile profile, CancellationToken ct)
    {
        var configPath = ValidateAndGetPath(agentSlug, $"{agentSlug}.json");
        var agentConfig = OnboardingMappers.ToAgentConfig(agentSlug, profile);
        var json = JsonSerializer.Serialize(agentConfig, JsonOptions);
        await File.WriteAllTextAsync(configPath, json, ct);

        logger.LogInformation("[DEPLOY-001] Wrote agent config for {AgentSlug} at {ConfigPath}", agentSlug, configPath);
    }

    private async Task WriteAgentContentAsync(string agentSlug, ScrapedProfile profile, CancellationToken ct)
    {
        var contentPath = ValidateAndGetPath(agentSlug, $"{agentSlug}.content.json");
        var agentContent = OnboardingMappers.ToAgentContent(agentSlug, profile);
        var json = JsonSerializer.Serialize(agentContent, JsonOptions);
        await File.WriteAllTextAsync(contentPath, json, ct);

        logger.LogInformation("[DEPLOY-002] Wrote agent content for {AgentSlug} at {ContentPath}", agentSlug, contentPath);
    }

    /// <summary>
    /// Validates the path stays within the config directory (path traversal protection)
    /// and ensures the directory exists.
    /// </summary>
    private string ValidateAndGetPath(string agentSlug, string fileName)
    {
        Directory.CreateDirectory(configDirectory);

        var filePath = Path.GetFullPath(Path.Combine(configDirectory, fileName));
        var canonicalConfigDir = Path.GetFullPath(configDirectory);

        if (!filePath.StartsWith(canonicalConfigDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid agent slug — path traversal detected");

        return filePath;
    }

    private async Task RunNextBuildAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo("npx")
        {
            WorkingDirectory = Path.GetFullPath("apps/agent-site"),
        };
        psi.ArgumentList.Add("next");
        psi.ArgumentList.Add("build");

        var result = await processRunner.RunAsync(psi, BuildTimeout, ct);

        if (result.ExitCode != 0)
        {
            logger.LogError("[DEPLOY-005] Next.js build failed with exit code {ExitCode}: {Stderr}",
                result.ExitCode, result.Stderr);
            throw new InvalidOperationException(
                $"Next.js build failed (exit code {result.ExitCode}). Check logs for details.");
        }

        logger.LogInformation("[DEPLOY-006] Next.js build completed successfully");
    }

    private async Task RunOpenNextBuildAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo("npx")
        {
            WorkingDirectory = Path.GetFullPath("apps/agent-site"),
        };
        psi.ArgumentList.Add("opennextjs-cloudflare");
        psi.ArgumentList.Add("build");

        var result = await processRunner.RunAsync(psi, BuildTimeout, ct);

        if (result.ExitCode != 0)
        {
            logger.LogError("[DEPLOY-007] OpenNext build failed with exit code {ExitCode}: {Stderr}",
                result.ExitCode, result.Stderr);
            throw new InvalidOperationException(
                $"OpenNext build failed (exit code {result.ExitCode}). Check logs for details.");
        }

        logger.LogInformation("[DEPLOY-008] OpenNext Cloudflare build completed successfully");
    }

    private async Task<string> RunWranglerDeployAsync(string agentSlug, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("npx")
        {
            WorkingDirectory = Path.GetFullPath("apps/agent-site"),
        };

        psi.ArgumentList.Add("wrangler");
        psi.ArgumentList.Add("pages");
        psi.ArgumentList.Add("deploy");
        psi.ArgumentList.Add(".open-next/assets");
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
            logger.LogError("[DEPLOY-003] Wrangler deploy failed with exit code {ExitCode}: {Stderr}",
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
        logger.LogWarning("[DEPLOY-004] Could not parse preview URL from Wrangler output, falling back to {FallbackUrl}", fallbackUrl);
        return fallbackUrl;
    }
}
