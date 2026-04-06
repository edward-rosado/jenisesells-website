namespace RealEstateStar.Domain.Activation.Services;

using RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Pure C# pattern-matching service that answers ~95% of WhatsApp pipeline queries
/// without needing a Claude API call. Returns null when Claude fallback is needed.
/// All methods are static pure functions — no DI, no state, no external deps.
/// </summary>
public static class PipelineQueryService
{
    private static readonly string[] ClosingKeywords = ["closing", "close"];
    private static readonly string[] NewLeadKeywords = ["new lead", "new contact", "recent"];
    private static readonly string[] ActiveKeywords = ["active", "active deal"];
    private static readonly string[] SummaryKeywords =
        ["how's my pipeline", "how is my pipeline", "pipeline overview", "how does it look", "pipeline summary", "how are things"];
    private static readonly string[] TerminalStages = ["closed", "lost"];
    private static readonly HashSet<string> StopWords =
        ["st", "rd", "dr", "ave", "ln", "ct", "blvd", "way", "pl", "cir", "the", "at", "on", "with", "for", "and"];

    public static string? TryAnswer(AgentPipeline pipeline, string question)
    {
        if (pipeline.Leads.Count == 0)
            return "Your pipeline is empty \u2014 no active leads yet.";

        var q = question.Trim().ToLowerInvariant();

        // Priority 1: Property lookup
        var propertyMatches = new List<PipelineLead>();
        foreach (var lead in pipeline.Leads)
        {
            if (!string.IsNullOrWhiteSpace(lead.Property) && ContainsAddressTokens(q, lead.Property))
                propertyMatches.Add(lead);
        }
        if (propertyMatches.Count > 0)
            return FormatLeadList(propertyMatches);

        // Priority 2: Name lookup
        var nameMatches = new List<PipelineLead>();
        foreach (var lead in pipeline.Leads)
        {
            if (ContainsNameTokens(q, lead.Name))
                nameMatches.Add(lead);
        }
        if (nameMatches.Count > 0)
            return FormatLeadList(nameMatches);

        // Priority 3: Stage filter
        if (ClosingKeywords.Any(k => q.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            var closing = pipeline.Leads
                .Where(l => l.Stage.Contains("closing", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return closing.Count > 0
                ? FormatLeadList(closing)
                : "No leads are in the closing stage right now.";
        }

        if (NewLeadKeywords.Any(k => q.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var recent = pipeline.Leads
                .Where(l => l.FirstSeen >= cutoff)
                .OrderByDescending(l => l.FirstSeen)
                .ToList();
            return recent.Count > 0
                ? FormatLeadList(recent)
                : "No new leads in the last 7 days.";
        }

        if (ActiveKeywords.Any(k => q.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            var active = pipeline.Leads
                .Where(l => !TerminalStages.Contains(l.Stage.ToLowerInvariant()))
                .ToList();
            return active.Count > 0
                ? FormatLeadList(active)
                : "No active deals right now.";
        }

        // Priority 4: Summary
        if (SummaryKeywords.Any(k => q.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return FormatSummary(pipeline);

        // No match — Claude fallback
        return null;
    }

    public static string FormatLead(PipelineLead lead)
    {
        var property = string.IsNullOrWhiteSpace(lead.Property) ? "no property" : lead.Property;
        var next = string.IsNullOrWhiteSpace(lead.Next) ? "none" : lead.Next;
        var lastActivity = lead.LastActivity.ToString("MMM d");
        return $"{lead.Name} | {lead.Type} | {property} | {lead.Stage} | last activity {lastActivity} | next: {next}";
    }

    public static string FormatLeadList(IReadOnlyList<PipelineLead> leads)
    {
        if (leads.Count == 1)
            return FormatLead(leads[0]);

        return string.Join("\n", leads.Select((l, i) => $"{i + 1}. {FormatLead(l)}"));
    }

    public static string FormatSummary(AgentPipeline pipeline)
    {
        var stageCounts = pipeline.Leads
            .GroupBy(l => l.Stage, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();

        return $"Pipeline: {pipeline.Leads.Count} leads\n{string.Join(" | ", stageCounts)}";
    }

    public static bool ContainsAddressTokens(string question, string property)
    {
        var addressTokens = Tokenize(property);
        var significantTokens = addressTokens
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .ToList();

        if (significantTokens.Count == 0)
            return false;

        var questionTokens = new HashSet<string>(Tokenize(question));

        // Match if question contains the street number OR any significant street name token
        var numberTokens = significantTokens.Where(t => t.All(char.IsDigit)).ToList();
        var nameTokens = significantTokens.Where(t => !t.All(char.IsDigit)).ToList();

        // If we have a street number match + any name token, strong match
        if (numberTokens.Any(n => questionTokens.Contains(n))
            && nameTokens.Any(n => questionTokens.Contains(n)))
            return true;

        // If we only have name tokens, require at least one match (e.g., "spruce st" → match on "spruce")
        if (numberTokens.Count == 0 && nameTokens.Any(n => questionTokens.Contains(n)))
            return true;

        // Also check if question contains the name token as a substring (e.g., "spruce" in "what about spruce?")
        if (nameTokens.Any(n => question.ToLowerInvariant().Contains(n)))
            return true;

        return false;
    }

    public static bool ContainsNameTokens(string question, string name)
    {
        var nameTokens = Tokenize(name)
            .Where(t => t.Length > 1)
            .ToList();

        if (nameTokens.Count == 0)
            return false;

        var q = question.ToLowerInvariant();

        // Match if any first/last name token appears in the question
        return nameTokens.Any(t => q.Contains(t));
    }

    private static List<string> Tokenize(string input)
    {
        return input
            .ToLowerInvariant()
            .Split([' ', ',', '.', '#', '-', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }
}
