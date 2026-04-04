using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.BrandingDiscovery;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: discovers agent branding (colors, fonts, logos, template recommendation).
/// Delegates to <see cref="BrandingDiscoveryWorker"/>.
/// </summary>
public sealed class BrandingDiscoveryFunction(
    BrandingDiscoveryWorker worker,
    ILogger<BrandingDiscoveryFunction> logger)
{
    [Function(ActivityNames.BrandingDiscovery)]
    public async Task<BrandingDiscoveryOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-120] BrandingDiscovery for agentId={AgentId}", input.AgentId);

        var result = await worker.DiscoverAsync(
            agentName: input.AgentName,
            agentDiscovery: ActivationDtoMapper.ToDomain(input.Discovery),
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            ct: ct);

        var kitDto = result.Kit is null ? null : new BrandingKitDto
        {
            Colors = result.Kit.Colors.Select(c => new ColorEntryDto { Role = c.Role, Hex = c.Hex, Source = c.Source, Usage = c.Usage }).ToList(),
            Fonts = result.Kit.Fonts.Select(f => new FontEntryDto { Role = f.Role, Family = f.Family, Weight = f.Weight, Source = f.Source }).ToList(),
            Logos = result.Kit.Logos.Select(l => new LogoVariantDto { Variant = l.Variant, FileName = l.FileName, Bytes = l.Bytes, Source = l.Source }).ToList(),
            RecommendedTemplate = result.Kit.RecommendedTemplate,
            TemplateReason = result.Kit.TemplateReason,
        };

        return new BrandingDiscoveryOutput { BrandingKitMarkdown = result.BrandingKitMarkdown, Kit = kitDto };
    }
}
