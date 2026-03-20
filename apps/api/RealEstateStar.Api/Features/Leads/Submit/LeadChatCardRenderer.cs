using System.Text.Json;
using System.Text.Json.Nodes;

namespace RealEstateStar.Api.Features.Leads.Submit;

public static class LeadChatCardRenderer
{
    public static JsonObject RenderNewLeadCard(Lead lead, LeadEnrichment enrichment, LeadScore score)
    {
        var leadType = lead.LeadType.ToString();
        var scoreBadge = ScoreBadge(score.OverallScore);

        var header = new JsonObject
        {
            ["title"] = $"New Lead: {lead.FullName}",
            ["subtitle"] = $"{scoreBadge} • {enrichment.MotivationCategory} • {leadType}",
            ["imageType"] = "CIRCLE",
            ["imageUrl"] = "https://real-estate-star.com/icon-192.png"
        };

        // Section 1: Contact info
        var contactSection = new JsonObject
        {
            ["header"] = "Contact Info",
            ["widgets"] = new JsonArray
            {
                Widget("Email", lead.Email),
                Widget("Phone", lead.Phone)
            }
        };

        // Section 2: Motivation analysis snippet
        var motivationSnippet = enrichment.MotivationAnalysis.Length > 200
            ? enrichment.MotivationAnalysis[..200] + "…"
            : enrichment.MotivationAnalysis;

        var motivationSection = new JsonObject
        {
            ["header"] = "Motivation Analysis",
            ["widgets"] = new JsonArray
            {
                Widget("Analysis", motivationSnippet)
            }
        };

        // Section 3: Top cold call openers (first 2)
        var openers = enrichment.ColdCallOpeners.Take(2).ToList();
        var openersWidgets = new JsonArray();
        for (var i = 0; i < openers.Count; i++)
            openersWidgets.Add(Widget($"Opener {i + 1}", openers[i]));

        var openersSection = new JsonObject
        {
            ["header"] = "Cold Call Openers",
            ["widgets"] = openersWidgets
        };

        // Button: "View in Drive"
        var driveUrl = $"https://drive.google.com/drive/search?q={Uri.EscapeDataString(lead.FullName)}";
        var buttonSection = new JsonObject
        {
            ["widgets"] = new JsonArray
            {
                new JsonObject
                {
                    ["buttonList"] = new JsonObject
                    {
                        ["buttons"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["text"] = "View in Drive",
                                ["onClick"] = new JsonObject
                                {
                                    ["openLink"] = new JsonObject
                                    {
                                        ["url"] = driveUrl
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var card = new JsonObject
        {
            ["cardsV2"] = new JsonArray
            {
                new JsonObject
                {
                    ["cardId"] = $"lead-{lead.Id}",
                    ["card"] = new JsonObject
                    {
                        ["header"] = header,
                        ["sections"] = new JsonArray
                        {
                            contactSection,
                            motivationSection,
                            openersSection,
                            buttonSection
                        }
                    }
                }
            }
        };

        return card;
    }

    private static JsonObject Widget(string label, string value) => new()
    {
        ["decoratedText"] = new JsonObject
        {
            ["topLabel"] = label,
            ["text"] = value
        }
    };

    private static string ScoreBadge(int score) => score switch
    {
        >= 80 => $"🔥 Score: {score}",
        >= 60 => $"⭐ Score: {score}",
        >= 40 => $"Score: {score}",
        _ => $"Score: {score}"
    };
}
