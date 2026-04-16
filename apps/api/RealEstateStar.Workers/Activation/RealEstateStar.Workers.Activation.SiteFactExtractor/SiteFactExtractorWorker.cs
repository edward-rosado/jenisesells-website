using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.SiteFactExtractor;

/// <summary>
/// Pure-compute worker that extracts a locale-neutral SiteFacts substrate from activation outputs.
/// No async, no external calls, no I/O — maps ActivationOutputs + AccountConfig → SiteFacts.
/// Consumed downstream by VoicedContentGenerator for per-locale resynthesis.
/// </summary>
public sealed class SiteFactExtractorWorker(ILogger<SiteFactExtractorWorker> logger)
{
    private const int MaxRecentSales = 6;
    private const int MaxTestimonials = 8;

    private static readonly IReadOnlyList<string> DefaultPipelineStages =
    [
        "Initial Contact",
        "Qualification",
        "Active Search",
        "Under Contract",
        "Closed"
    ];

    public SiteFacts Extract(ActivationOutputs outputs, AccountConfig account)
    {
        logger.LogDebug(
            "[SFX-001] Extracting SiteFacts for account {Handle}",
            account.Handle);

        var agent = ExtractAgentIdentity(outputs, account);
        var brokerage = ExtractBrokerageIdentity(account);
        var location = ExtractLocationFacts(outputs, account);
        var specialties = ExtractSpecialtiesFacts(outputs);
        var trust = ExtractTrustSignals(outputs);
        var recentSales = ExtractRecentSales(outputs);
        var testimonials = ExtractTestimonials(outputs);
        var credentials = ExtractCredentials(account);
        var stages = ExtractPipelineStages(outputs);
        var voicesByLocale = ExtractVoicesByLocale(outputs);

        var facts = new SiteFacts(
            agent,
            brokerage,
            location,
            specialties,
            trust,
            recentSales,
            testimonials,
            credentials,
            stages,
            voicesByLocale);

        var hash = ComputeFactsHash(facts);
        var result = facts with { FactsHash = hash };

        logger.LogDebug(
            "[SFX-002] SiteFacts extracted for account {Handle}: hash={Hash}, sales={SalesCount}, testimonials={TestimonialCount}",
            account.Handle, hash[..8], recentSales.Count, testimonials.Count);

        return result;
    }

    // ── Agent Identity ────────────────────────────────────────────────────────

    private static AgentIdentity ExtractAgentIdentity(ActivationOutputs outputs, AccountConfig account)
    {
        var accountAgent = account.Agent;

        // Prefer account config as source of truth; fall back to activation outputs
        var name = accountAgent?.Name ?? outputs.AgentName ?? "";
        var title = accountAgent?.Title ?? outputs.AgentTitle ?? "";
        var email = accountAgent?.Email ?? outputs.AgentEmail ?? "";
        var phone = accountAgent?.Phone ?? outputs.AgentPhone ?? "";
        var license = accountAgent?.LicenseNumber ?? outputs.AgentLicenseNumber;
        var languages = (accountAgent?.Languages is { Count: > 0 }
            ? accountAgent.Languages
            : (IReadOnlyList<string>?)outputs.Languages) ?? [];

        // Years of experience from discovery profiles — take the max value across profiles
        var yearsExperience = outputs.Discovery?.Profiles
            .Select(p => p.YearsExperience ?? 0)
            .DefaultIfEmpty(0)
            .Max() ?? 0;

        return new AgentIdentity(
            Name: name,
            LegalName: name,
            Title: title,
            Email: email,
            Phone: phone,
            LicenseNumber: license,
            YearsExperience: yearsExperience,
            Languages: languages);
    }

    // ── Brokerage Identity ────────────────────────────────────────────────────

    private static BrokerageIdentity ExtractBrokerageIdentity(AccountConfig account)
    {
        var b = account.Brokerage;
        return new BrokerageIdentity(
            Name: b?.Name ?? "",
            LegalName: b?.Name,
            LicenseNumber: b?.LicenseNumber,
            OfficeAddress: b?.OfficeAddress,
            OfficePhone: b?.OfficePhone,
            DomainHint: null);
    }

    // ── Location Facts ────────────────────────────────────────────────────────

    private static LocationFacts ExtractLocationFacts(ActivationOutputs outputs, AccountConfig account)
    {
        var state = account.Location?.State ?? outputs.State ?? "";
        var serviceAreas = (account.Location?.ServiceAreas is { Count: > 0 }
            ? account.Location.ServiceAreas
            : (IReadOnlyList<string>?)outputs.ServiceAreas) ?? [];

        // Build listing frequency by city from closed transaction documents
        var freqByCity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (outputs.Discovery?.Profiles is { } profiles)
        {
            foreach (var profile in profiles)
            {
                foreach (var sale in profile.RecentSales)
                {
                    if (!string.IsNullOrWhiteSpace(sale.City))
                    {
                        var city = sale.City.Trim();
                        freqByCity[city] = freqByCity.TryGetValue(city, out var count) ? count + 1 : 1;
                    }
                }
            }
        }

        return new LocationFacts(
            State: state,
            ServiceAreas: serviceAreas,
            ListingFrequencyByCity: freqByCity);
    }

    // ── Specialties Facts ─────────────────────────────────────────────────────

    private static SpecialtiesFacts ExtractSpecialtiesFacts(ActivationOutputs outputs)
    {
        var specialtySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var evidenceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Collect from third-party profiles
        if (outputs.Discovery?.Profiles is { } profiles)
        {
            foreach (var profile in profiles)
            {
                foreach (var specialty in profile.Specialties)
                {
                    if (string.IsNullOrWhiteSpace(specialty)) continue;
                    specialtySet.Add(specialty.Trim());
                    evidenceCount[specialty.Trim()] =
                        evidenceCount.TryGetValue(specialty.Trim(), out var c) ? c + 1 : 1;
                }
            }
        }

        // Vibe hints from BrandingKit if available
        var vibeHints = new List<string>();
        if (outputs.BrandingKit?.Colors is { } colors)
        {
            foreach (var color in colors)
            {
                if (!string.IsNullOrWhiteSpace(color.Usage))
                    vibeHints.Add(color.Usage);
            }
        }

        return new SpecialtiesFacts(
            Specialties: specialtySet.OrderBy(s => s).ToList(),
            VibeHints: vibeHints.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            EvidenceCount: evidenceCount);
    }

    // ── Trust Signals ─────────────────────────────────────────────────────────

    private static TrustSignals ExtractTrustSignals(ActivationOutputs outputs)
    {
        var allReviews = CollectAllReviews(outputs);
        var reviewCount = allReviews.Count;
        var avgRating = reviewCount > 0
            ? (decimal)allReviews.Average(r => r.Rating)
            : 0m;

        // Transaction count from discovery profiles
        var transactionCount = outputs.Discovery?.Profiles
            .Sum(p => p.SalesCount ?? 0) ?? 0;

        // Average sale price from recent sales across profiles
        var salePrices = outputs.Discovery?.Profiles
            .SelectMany(p => p.RecentSales)
            .Select(s => ParsePrice(s.Price))
            .Where(p => p > 0)
            .ToList() ?? [];

        var avgSalePrice = salePrices.Count > 0
            ? (decimal)salePrices.Average()
            : 0m;

        return new TrustSignals(
            ReviewCount: reviewCount,
            AverageRating: Math.Round(avgRating, 1),
            TransactionCount: transactionCount,
            AverageResponseTime: TimeSpan.Zero,
            AverageSalePrice: Math.Round(avgSalePrice, 2));
    }

    // ── Recent Sales ──────────────────────────────────────────────────────────

    private static IReadOnlyList<RecentSale> ExtractRecentSales(ActivationOutputs outputs)
    {
        var sales = new List<RecentSale>();

        if (outputs.Discovery?.Profiles is not { } profiles)
            return sales;

        foreach (var profile in profiles)
        {
            foreach (var listing in profile.RecentSales)
            {
                if (sales.Count >= MaxRecentSales) break;

                var price = ParsePrice(listing.Price);
                if (price <= 0) continue;

                sales.Add(new RecentSale(
                    Address: listing.Address,
                    City: listing.City,
                    State: listing.State,
                    Price: price,
                    SoldDate: listing.Date ?? DateTime.MinValue,
                    SoldByAgent: true,
                    ListingCourtesyOf: null,
                    ImageUrl: listing.ImageUrl));

                if (sales.Count >= MaxRecentSales) break;
            }

            if (sales.Count >= MaxRecentSales) break;
        }

        return sales
            .OrderByDescending(s => s.SoldDate)
            .Take(MaxRecentSales)
            .ToList();
    }

    // ── Testimonials ──────────────────────────────────────────────────────────

    private static IReadOnlyList<SiteTestimonial> ExtractTestimonials(ActivationOutputs outputs)
    {
        var reviews = CollectAllReviews(outputs);

        return reviews
            .Where(r => !string.IsNullOrWhiteSpace(r.Text))
            .OrderByDescending(r => r.Rating)
            .Take(MaxTestimonials)
            .Select(r => new SiteTestimonial(
                Text: r.Text,
                Rating: r.Rating,
                Reviewer: r.Reviewer,
                Source: r.Source,
                Date: r.Date ?? DateTime.MinValue))
            .ToList();
    }

    // ── Credentials ───────────────────────────────────────────────────────────

    private static IReadOnlyList<AgentCredential> ExtractCredentials(AccountConfig account)
    {
        var credentials = account.Agent?.Credentials;
        if (credentials is null or { Count: 0 })
            return [];

        return credentials
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => new AgentCredential(
                Name: c.Trim(),
                Issuer: null,
                IssuedAt: null))
            .ToList();
    }

    // ── Pipeline Stages ───────────────────────────────────────────────────────

    private static PipelineStages ExtractPipelineStages(ActivationOutputs outputs)
    {
        // PipelineJson contains the agent's pipeline as structured JSON.
        // Parse stage names from "stages" array if present; else use defaults.
        var stageNames = TryParsePipelineStages(outputs.PipelineJson)
                         ?? DefaultPipelineStages;

        return new PipelineStages(
            StageNames: stageNames,
            LeadCountByStage: new Dictionary<string, int>());
    }

    private static IReadOnlyList<string>? TryParsePipelineStages(string? pipelineJson)
    {
        if (string.IsNullOrWhiteSpace(pipelineJson)) return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(pipelineJson);
            if (doc.RootElement.TryGetProperty("stages", out var stagesEl)
                && stagesEl.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var stages = new List<string>();
                foreach (var item in stagesEl.EnumerateArray())
                {
                    // Support both string elements and { "name": "..." } objects
                    if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) stages.Add(s);
                    }
                    else if (item.ValueKind == System.Text.Json.JsonValueKind.Object
                             && item.TryGetProperty("name", out var nameEl))
                    {
                        var s = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) stages.Add(s);
                    }
                }
                return stages.Count > 0 ? stages : null;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Malformed pipeline JSON — fall back to defaults
        }

        return null;
    }

    // ── Voices By Locale ──────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, LocaleVoice> ExtractVoicesByLocale(ActivationOutputs outputs)
    {
        var voices = new Dictionary<string, LocaleVoice>(StringComparer.OrdinalIgnoreCase);

        // Always include English if voice skill is present
        var enVoice = outputs.VoiceSkill ?? "";
        var enPersonality = outputs.PersonalitySkill ?? "";
        if (enVoice.Length > 0 || enPersonality.Length > 0)
        {
            voices["en"] = BuildLocaleVoice("en", enVoice, enPersonality);
        }

        // Add localized variants from LocalizedSkills dictionary
        // Keys are "{skillName}.{locale}" e.g. "VoiceSkill.es", "PersonalitySkill.es"
        if (outputs.LocalizedSkills is { } localizedSkills)
        {
            var locales = localizedSkills.Keys
                .Select(k =>
                {
                    var dot = k.LastIndexOf('.');
                    return dot > 0 ? k[(dot + 1)..] : null;
                })
                .Where(l => l is not null && !string.Equals(l, "en", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var locale in locales)
            {
                var voiceKey = $"VoiceSkill.{locale}";
                var personalityKey = $"PersonalitySkill.{locale}";

                localizedSkills.TryGetValue(voiceKey, out var voiceMd);
                localizedSkills.TryGetValue(personalityKey, out var personalityMd);

                if (!string.IsNullOrWhiteSpace(voiceMd) || !string.IsNullOrWhiteSpace(personalityMd))
                {
                    voices[locale!] = BuildLocaleVoice(locale!, voiceMd ?? "", personalityMd ?? "");
                }
            }
        }

        return voices;
    }

    private static LocaleVoice BuildLocaleVoice(string locale, string voiceMd, string personalityMd)
    {
        var hash = ComputeShortHash(voiceMd + personalityMd);
        return new LocaleVoice(
            Locale: locale,
            VoiceSkillMarkdown: voiceMd,
            PersonalitySkillMarkdown: personalityMd,
            VoiceHash: hash);
    }

    // ── Facts Hash ────────────────────────────────────────────────────────────

    private static string ComputeFactsHash(SiteFacts facts)
    {
        var sb = new StringBuilder();

        // Agent
        sb.Append(facts.Agent.Name).Append('|')
          .Append(facts.Agent.LegalName).Append('|')
          .Append(facts.Agent.Title).Append('|')
          .Append(facts.Agent.Email).Append('|')
          .Append(facts.Agent.Phone).Append('|')
          .Append(facts.Agent.LicenseNumber).Append('|')
          .Append(facts.Agent.YearsExperience).Append('|')
          .Append(string.Join(",", facts.Agent.Languages)).Append('|');

        // Brokerage
        sb.Append(facts.Brokerage.Name).Append('|')
          .Append(facts.Brokerage.LicenseNumber).Append('|')
          .Append(facts.Brokerage.OfficeAddress).Append('|')
          .Append(facts.Brokerage.OfficePhone).Append('|');

        // Location
        sb.Append(facts.Location.State).Append('|')
          .Append(string.Join(",", facts.Location.ServiceAreas)).Append('|')
          .Append(string.Join(",", facts.Location.ListingFrequencyByCity
              .OrderBy(kv => kv.Key)
              .Select(kv => $"{kv.Key}:{kv.Value}"))).Append('|');

        // Specialties
        sb.Append(string.Join(",", facts.Specialties.Specialties)).Append('|')
          .Append(string.Join(",", facts.Specialties.VibeHints)).Append('|');

        // Trust
        sb.Append(facts.Trust.ReviewCount).Append('|')
          .Append(facts.Trust.AverageRating).Append('|')
          .Append(facts.Trust.TransactionCount).Append('|')
          .Append(facts.Trust.AverageSalePrice).Append('|');

        // Recent sales
        foreach (var sale in facts.RecentSales)
            sb.Append(sale.Address).Append('|')
              .Append(sale.Price).Append('|')
              .Append(sale.SoldDate.ToString("yyyyMMdd")).Append('|');

        // Testimonials
        foreach (var t in facts.Testimonials)
            sb.Append(t.Reviewer).Append('|')
              .Append(t.Text.Length > 64 ? t.Text[..64] : t.Text).Append('|');

        // Credentials
        foreach (var c in facts.Credentials)
            sb.Append(c.Name).Append('|');

        // Pipeline
        sb.Append(string.Join(",", facts.Stages.StageNames)).Append('|');

        // Voices — include voice hashes in deterministic key order
        foreach (var kv in facts.VoicesByLocale.OrderBy(kv => kv.Key))
            sb.Append(kv.Key).Append(':').Append(kv.Value.VoiceHash).Append('|');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<Review> CollectAllReviews(ActivationOutputs outputs)
    {
        if (outputs.Discovery is null) return [];

        var reviews = new List<Review>(outputs.Discovery.Reviews);
        foreach (var profile in outputs.Discovery.Profiles)
            reviews.AddRange(profile.Reviews);

        return reviews;
    }

    private static decimal ParsePrice(string? priceStr)
    {
        if (string.IsNullOrWhiteSpace(priceStr)) return 0m;

        // Strip common price formatting: "$1,200,000" → 1200000
        var cleaned = priceStr
            .Replace("$", "")
            .Replace(",", "")
            .Trim();

        return decimal.TryParse(cleaned, out var value) ? value : 0m;
    }

    private static string ComputeShortHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
