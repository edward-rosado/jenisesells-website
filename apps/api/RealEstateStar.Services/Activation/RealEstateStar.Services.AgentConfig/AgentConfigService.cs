using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Services.AgentConfig;

/// <summary>
/// Generates account.json and content.json from gathered activation data.
/// Single agent: writes config/accounts/{handle}/account.json + content.json.
/// Brokerage first agent: bootstraps brokerage config, then writes agent config.json + content.json.
/// Brokerage subsequent agent: skips brokerage bootstrap, just writes agent config files.
/// Does NOT overwrite existing configs.
/// </summary>
public sealed class AgentConfigService(
    IAccountConfigService accountConfigService,
    IFileStorageProvider storage,
    ILogger<AgentConfigService> logger) : IAgentConfigService
{
    // Config sub-folder for brokerage per-agent configs
    internal const string AgentsSubfolder = "agents";
    internal const string AccountJsonFile = "account.json";
    internal const string ContentJsonFile = "content.json";
    internal const string AgentConfigJsonFile = "config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task GenerateAsync(
        string accountId,
        string agentId,
        string handle,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        var isSingleAgent = accountId == agentId;

        logger.LogInformation(
            "[CFG-010] Generating config for agentId={AgentId}, accountId={AccountId}, handle={Handle}, isSingleAgent={IsSingleAgent}",
            agentId, accountId, handle, isSingleAgent);

        if (isSingleAgent)
        {
            await GenerateSingleAgentConfigAsync(handle, outputs, ct);
        }
        else
        {
            await GenerateBrokerageConfigAsync(accountId, agentId, outputs, ct);
        }

        logger.LogInformation(
            "[CFG-090] Config generation complete for agentId={AgentId}", agentId);
    }

    // ── Single agent ──────────────────────────────────────────────────────────

    private async Task GenerateSingleAgentConfigAsync(
        string handle,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        // Check if config already exists — do not overwrite
        var existing = await accountConfigService.GetAccountAsync(handle, ct);
        if (existing is not null)
        {
            logger.LogInformation("[CFG-020] account.json already exists for handle={Handle}, skipping", handle);
        }
        else
        {
            var accountJson = BuildSingleAgentAccountJson(handle, outputs);
            await WriteConfigFileAsync(handle, AccountJsonFile, accountJson, ct);
            logger.LogInformation("[CFG-021] Wrote account.json for handle={Handle}", handle);
        }

        // Write content.json — check via storage
        var contentFolder = $"config/accounts/{handle}";
        var existingContent = await storage.ReadDocumentAsync(contentFolder, ContentJsonFile, ct);
        if (existingContent is not null)
        {
            logger.LogInformation("[CFG-022] content.json already exists for handle={Handle}, skipping", handle);
        }
        else
        {
            var contentJson = BuildContentJson(handle, outputs);
            await storage.WriteDocumentAsync(contentFolder, ContentJsonFile, contentJson, ct);
            logger.LogInformation("[CFG-023] Wrote content.json for handle={Handle}", handle);
        }
    }

    // ── Brokerage ─────────────────────────────────────────────────────────────

    private async Task GenerateBrokerageConfigAsync(
        string accountId,
        string agentId,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        // Bootstrap brokerage account.json if not exists
        var existingBrokerage = await accountConfigService.GetAccountAsync(accountId, ct);
        if (existingBrokerage is null)
        {
            var brokerageJson = BuildBrokerageAccountJson(accountId, outputs);
            await WriteConfigFileAsync(accountId, AccountJsonFile, brokerageJson, ct);
            logger.LogInformation("[CFG-030] Bootstrapped brokerage account.json for accountId={AccountId}", accountId);

            // Also write brokerage content.json
            var brokerageContentFolder = $"config/accounts/{accountId}";
            var brokerageContent = BuildContentJson(accountId, outputs);
            await storage.WriteDocumentAsync(brokerageContentFolder, ContentJsonFile, brokerageContent, ct);
            logger.LogInformation("[CFG-031] Wrote brokerage content.json for accountId={AccountId}", accountId);
        }

        // Write per-agent config.json + content.json
        var agentFolder = $"config/accounts/{accountId}/{AgentsSubfolder}/{agentId}";

        var existingAgentConfig = await storage.ReadDocumentAsync(agentFolder, AgentConfigJsonFile, ct);
        if (existingAgentConfig is not null)
        {
            logger.LogInformation(
                "[CFG-032] Agent config.json already exists for agentId={AgentId}, skipping", agentId);
        }
        else
        {
            var agentConfigJson = BuildAgentConfigJson(agentId, outputs);
            await storage.WriteDocumentAsync(agentFolder, AgentConfigJsonFile, agentConfigJson, ct);
            logger.LogInformation("[CFG-033] Wrote agent config.json for agentId={AgentId}", agentId);
        }

        var existingAgentContent = await storage.ReadDocumentAsync(agentFolder, ContentJsonFile, ct);
        if (existingAgentContent is not null)
        {
            logger.LogInformation(
                "[CFG-034] Agent content.json already exists for agentId={AgentId}, skipping", agentId);
        }
        else
        {
            var agentContentJson = BuildContentJson(agentId, outputs);
            await storage.WriteDocumentAsync(agentFolder, ContentJsonFile, agentContentJson, ct);
            logger.LogInformation("[CFG-035] Wrote agent content.json for agentId={AgentId}", agentId);
        }
    }

    // ── JSON builders ─────────────────────────────────────────────────────────

    internal static string BuildSingleAgentAccountJson(string handle, ActivationOutputs outputs)
    {
        var branding = outputs.BrandingKit;
        var primaryColor = branding?.Colors.FirstOrDefault(c =>
            c.Role.Equals("primary", StringComparison.OrdinalIgnoreCase))?.Hex ?? "#1E3A5F";
        var secondaryColor = branding?.Colors.FirstOrDefault(c =>
            c.Role.Equals("secondary", StringComparison.OrdinalIgnoreCase))?.Hex;
        var accentColor = branding?.Colors.FirstOrDefault(c =>
            c.Role.Equals("accent", StringComparison.OrdinalIgnoreCase))?.Hex;
        var fontFamily = branding?.Fonts.FirstOrDefault(f =>
            f.Role.Equals("body", StringComparison.OrdinalIgnoreCase) ||
            f.Role.Equals("primary", StringComparison.OrdinalIgnoreCase))?.Family ?? "Segoe UI";

        var obj = new JsonObject
        {
            ["handle"] = handle,
            ["accountId"] = handle,
            ["template"] = branding?.RecommendedTemplate ?? "emerald-classic",
            ["branding"] = new JsonObject
            {
                ["primary_color"] = primaryColor,
                ["secondary_color"] = secondaryColor,
                ["accent_color"] = accentColor,
                ["font_family"] = fontFamily,
                ["logo_url"] = $"/agents/{handle}/logo.png",
            },
            ["agent"] = new JsonObject
            {
                ["enabled"] = true,
                ["id"] = handle,
                ["name"] = outputs.AgentName ?? "",
                ["title"] = outputs.AgentTitle ?? "REALTOR\u00ae",
                ["phone"] = outputs.AgentPhone ?? "",
                ["email"] = outputs.AgentEmail ?? "",
                ["headshot_url"] = $"/agents/{handle}/headshot.jpg",
                ["license_number"] = outputs.AgentLicenseNumber,
                ["languages"] = BuildJsonArray(outputs.Languages ?? ["English"]),
                ["tagline"] = outputs.AgentTagline,
            },
            ["location"] = new JsonObject
            {
                ["state"] = outputs.State ?? "",
                ["service_areas"] = BuildJsonArray(outputs.ServiceAreas ?? []),
            },
            ["integrations"] = new JsonObject
            {
                ["email_provider"] = "gmail",
            },
            ["compliance"] = BuildComplianceJson(outputs),
            ["contact_info"] = BuildContactInfoJson(outputs),
        };

        return obj.ToJsonString(JsonOptions);
    }

    internal static string BuildBrokerageAccountJson(string accountId, ActivationOutputs outputs)
    {
        var branding = outputs.BrandingKit;
        var primaryColor = branding?.Colors.FirstOrDefault(c =>
            c.Role.Equals("primary", StringComparison.OrdinalIgnoreCase))?.Hex ?? "#1E3A5F";
        var secondaryColor = branding?.Colors.FirstOrDefault(c =>
            c.Role.Equals("secondary", StringComparison.OrdinalIgnoreCase))?.Hex;
        var accentColor = branding?.Colors.FirstOrDefault(c =>
            c.Role.Equals("accent", StringComparison.OrdinalIgnoreCase))?.Hex;
        var fontFamily = branding?.Fonts.FirstOrDefault(f =>
            f.Role.Equals("body", StringComparison.OrdinalIgnoreCase) ||
            f.Role.Equals("primary", StringComparison.OrdinalIgnoreCase))?.Family ?? "Segoe UI";

        var obj = new JsonObject
        {
            ["handle"] = accountId,
            ["accountId"] = accountId,
            ["template"] = branding?.RecommendedTemplate ?? "emerald-classic",
            ["branding"] = new JsonObject
            {
                ["primary_color"] = primaryColor,
                ["secondary_color"] = secondaryColor,
                ["accent_color"] = accentColor,
                ["font_family"] = fontFamily,
            },
            ["brokerage"] = new JsonObject
            {
                ["name"] = outputs.AgentName ?? "",
            },
            ["location"] = new JsonObject
            {
                ["state"] = outputs.State ?? "",
                ["service_areas"] = BuildJsonArray(outputs.ServiceAreas ?? []),
            },
        };

        return obj.ToJsonString(JsonOptions);
    }

    internal static string BuildAgentConfigJson(string agentId, ActivationOutputs outputs)
    {
        var obj = new JsonObject
        {
            ["id"] = agentId,
            ["name"] = outputs.AgentName ?? "",
            ["title"] = outputs.AgentTitle ?? "REALTOR\u00ae",
            ["phone"] = outputs.AgentPhone ?? "",
            ["email"] = outputs.AgentEmail ?? "",
            ["headshot_url"] = $"/agents/{agentId}/headshot.jpg",
            ["license_number"] = outputs.AgentLicenseNumber,
            ["languages"] = BuildJsonArray(outputs.Languages ?? ["English"]),
            ["tagline"] = outputs.AgentTagline,
        };

        return obj.ToJsonString(JsonOptions);
    }

    internal static string BuildContentJson(string handle, ActivationOutputs outputs)
    {
        // Build hero section from VoiceSkill / third-party bios
        var firstName = outputs.AgentName?.Split(' ').FirstOrDefault() ?? handle;
        var profiles = outputs.Discovery?.Profiles ?? [];

        var bio = profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.Bio))
            .Select(p => p.Bio!)
            .FirstOrDefault() ?? $"I'm {outputs.AgentName}, a dedicated REALTOR\u00ae serving {string.Join(", ", outputs.ServiceAreas ?? [])}.";

        var salesCount = profiles.Where(p => p.SalesCount.HasValue).Select(p => p.SalesCount!.Value).FirstOrDefault();
        var yearsExp = profiles.Where(p => p.YearsExperience.HasValue).Select(p => p.YearsExperience!.Value).FirstOrDefault();

        // Reviews from all platforms
        var allReviews = profiles.SelectMany(p => p.Reviews).ToList();

        // Recent sold listings
        var soldItems = profiles
            .SelectMany(p => p.RecentSales)
            .Take(6)
            .Select(l => new JsonObject
            {
                ["address"] = l.Address,
                ["city"] = l.City,
                ["state"] = l.State,
                ["price"] = l.Price,
                ["image_url"] = l.ImageUrl ?? $"/agents/{handle}/sold/placeholder.jpg",
            })
            .ToList();

        var statsItems = new JsonArray();
        if (salesCount > 0)
            statsItems.Add(new JsonObject { ["value"] = $"{salesCount}+", ["label"] = "Homes Sold" });
        if (yearsExp > 0)
            statsItems.Add(new JsonObject { ["value"] = $"{yearsExp}+", ["label"] = "Years of Experience" });

        var testimonialItems = allReviews
            .Take(4)
            .Select(r => new JsonObject
            {
                ["text"] = r.Text,
                ["reviewer"] = r.Reviewer,
                ["rating"] = r.Rating,
                ["source"] = r.Source,
            })
            .ToList();

        var galleryItems = soldItems.Count > 0
            ? new JsonArray(soldItems.Select(i => (JsonNode)i).ToArray())
            : new JsonArray();

        var obj = new JsonObject
        {
            ["navigation"] = new JsonObject
            {
                ["items"] = new JsonArray(
                    new JsonObject { ["label"] = "Why Choose Me", ["href"] = "#features", ["enabled"] = true },
                    new JsonObject { ["label"] = "How It Works", ["href"] = "#steps", ["enabled"] = true },
                    new JsonObject { ["label"] = "Recent Sales", ["href"] = "#gallery", ["enabled"] = true },
                    new JsonObject { ["label"] = "Testimonials", ["href"] = "#testimonials", ["enabled"] = true },
                    new JsonObject { ["label"] = "Ready to Move?", ["href"] = "#contact_form", ["enabled"] = true },
                    new JsonObject { ["label"] = "About", ["href"] = "#about", ["enabled"] = true }
                ),
            },
            ["pages"] = new JsonObject
            {
                ["home"] = new JsonObject
                {
                    ["sections"] = new JsonObject
                    {
                        ["hero"] = new JsonObject
                        {
                            ["enabled"] = true,
                            ["data"] = new JsonObject
                            {
                                ["headline"] = "Buy or Sell with Confidence",
                                ["highlight_word"] = "Confidence",
                                ["tagline"] = outputs.AgentTagline ?? "Your trusted REALTOR\u00ae.",
                                ["body"] = bio,
                                ["cta_text"] = "Get Your Free Home Value Report in Minutes",
                                ["cta_link"] = "#contact_form",
                            },
                        },
                        ["stats"] = new JsonObject
                        {
                            ["enabled"] = statsItems.Count > 0,
                            ["data"] = new JsonObject { ["items"] = statsItems },
                        },
                        ["gallery"] = new JsonObject
                        {
                            ["enabled"] = soldItems.Count > 0,
                            ["data"] = new JsonObject
                            {
                                ["title"] = $"Recently Sold by {firstName}",
                                ["subtitle"] = "Real results — verified sales.",
                                ["items"] = galleryItems,
                            },
                        },
                        ["testimonials"] = new JsonObject
                        {
                            ["enabled"] = testimonialItems.Count > 0,
                            ["data"] = new JsonObject
                            {
                                ["title"] = "What My Clients Say",
                                ["subtitle"] = "Real reviews from real clients.",
                                ["items"] = new JsonArray(testimonialItems.Select(i => (JsonNode)i).ToArray()),
                            },
                        },
                        ["contact_form"] = new JsonObject
                        {
                            ["enabled"] = true,
                            ["data"] = new JsonObject
                            {
                                ["title"] = "Ready to Make a Move?",
                                ["subtitle"] = $"Selling? Get your free Home Value Report in minutes.\u00ae Buying? Tell me what you're looking for.",
                                ["description"] = $"Selling your home? Enter your address below to receive a free Market Analysis in minutes! Looking to buy? Tell me what you're looking for and I'll help you find it. **100% free, no obligation.**",
                            },
                        },
                        ["about"] = new JsonObject
                        {
                            ["enabled"] = true,
                            ["data"] = new JsonObject
                            {
                                ["title"] = $"About {firstName}",
                                ["bio"] = new JsonArray(new JsonValue[] { JsonValue.Create(bio)! }),
                                ["credentials"] = new JsonArray(),
                            },
                        },
                        ["steps"] = new JsonObject
                        {
                            ["enabled"] = true,
                            ["data"] = new JsonObject
                            {
                                ["title"] = "How It Works",
                                ["subtitle"] = "Three simple steps to get started.",
                                ["steps"] = new JsonArray(
                                    new JsonObject
                                    {
                                        ["number"] = 1,
                                        ["title"] = "Submit Your Info",
                                        ["description"] = "Fill out the quick form below. It takes less than 2 minutes.",
                                    },
                                    new JsonObject
                                    {
                                        ["number"] = 2,
                                        ["title"] = "Get Your Free Home Value Report",
                                        ["description"] = "I'll send you a personalized Home Value Report within minutes.",
                                    },
                                    new JsonObject
                                    {
                                        ["number"] = 3,
                                        ["title"] = "Schedule a Walkthrough",
                                        ["description"] = "I'll visit your home to give you the most accurate price.",
                                    }
                                ),
                            },
                        },
                    },
                },
                ["thank_you"] = new JsonObject
                {
                    ["heading"] = "Thank You!",
                    ["subheading"] = "Your Free Home Value Report Is Being Prepared Now!",
                    ["body"] = "{firstName} will send your personalized Comparative Market Analysis to your email shortly.",
                    ["disclaimer"] = "This home value report is a Comparative Market Analysis (CMA) and is not an appraisal.",
                    ["cta_call"] = $"Call {{firstName}}: {{phone}}",
                    ["cta_back"] = "{firstName}'s Site",
                },
            },
        };

        return obj.ToJsonString(JsonOptions);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JsonObject BuildComplianceJson(ActivationOutputs outputs)
    {
        var state = outputs.State ?? "US";
        var (stateForm, licensingBody) = GetStateComplianceDefaults(state);

        return new JsonObject
        {
            ["state_form"] = stateForm,
            ["licensing_body"] = licensingBody,
            ["disclosure_requirements"] = new JsonArray(),
        };
    }

    private static JsonArray BuildContactInfoJson(ActivationOutputs outputs)
    {
        var items = new JsonArray();

        if (!string.IsNullOrWhiteSpace(outputs.AgentEmail))
        {
            items.Add(new JsonObject
            {
                ["type"] = "email",
                ["value"] = outputs.AgentEmail,
                ["label"] = "Personal Email",
                ["is_preferred"] = false,
            });
        }

        if (!string.IsNullOrWhiteSpace(outputs.AgentPhone))
        {
            items.Add(new JsonObject
            {
                ["type"] = "phone",
                ["value"] = outputs.AgentPhone,
                ["label"] = "Cell Phone",
                ["is_preferred"] = true,
            });
        }

        return items;
    }

    private static JsonArray BuildJsonArray(IEnumerable<string> items) =>
        new(items.Select(i => (JsonNode)JsonValue.Create(i)!).ToArray());

    internal static (string stateForm, string licensingBody) GetStateComplianceDefaults(string state) =>
        state.ToUpperInvariant() switch
        {
            "NJ" => ("NJ-REALTORS-118", "NJ Real Estate Commission"),
            "NY" => ("NY-DOS-1736", "NY Department of State"),
            "CA" => ("CAR-RPA-CA", "CA Department of Real Estate"),
            "FL" => ("FAR-2016", "FL Division of Real Estate"),
            "TX" => ("TREC-1-4", "TX Real Estate Commission"),
            _ => ($"{state.ToUpperInvariant()}-STANDARD", $"{state.ToUpperInvariant()} Real Estate Commission"),
        };

    private async Task WriteConfigFileAsync(
        string handle,
        string fileName,
        string content,
        CancellationToken ct)
    {
        var folder = $"config/accounts/{handle}";
        await storage.EnsureFolderExistsAsync(folder, ct);
        await storage.WriteDocumentAsync(folder, fileName, content, ct);
    }
}
