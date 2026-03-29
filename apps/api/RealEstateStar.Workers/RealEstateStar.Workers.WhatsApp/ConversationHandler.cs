using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.WhatsApp.Models;

namespace RealEstateStar.Workers.WhatsApp;

/// <summary>
/// Classifies inbound WhatsApp messages by intent (Stage 1, Haiku) then routes
/// to a static deflection or a response generator (Stage 2, Sonnet) based on
/// the classification result.
/// </summary>
public class ConversationHandler(
    IIntentClassifier classifier,
    IResponseGenerator generator,
    ILogger<ConversationHandler>? logger = null,
    IAgentContextLoader? agentContextLoader = null) : IConversationHandler
{
    // Static deflections — no LLM cost for out-of-scope messages.
    // {0} = agentFirstName where applicable.
    private static readonly Dictionary<OutOfScopeCategory, string> Deflections = new()
    {
        [OutOfScopeCategory.GeneralReQuestion] =
            "I can only answer questions about your specific leads, {0}. Try asking about a lead by name.",
        [OutOfScopeCategory.LegalFinancial] =
            "I can't provide legal or financial advice. Please consult a licensed professional.",
        [OutOfScopeCategory.NonReTopic] =
            "I'm focused on your real estate leads and pipeline, {0}. How can I help with a lead?",
        [OutOfScopeCategory.NoLeadData] =
            "I don't have data on that person. I can only answer about leads submitted through your site.",
        [OutOfScopeCategory.PromptInjection] =
            "I can only help with your leads and pipeline. What lead can I help you with?"
    };

    internal static string GetHelpText() =>
        "Here's what I can help with:\n" +
        "- Ask about a lead by name (e.g. \"Tell me about Jane Smith\")\n" +
        "- Acknowledge a lead (e.g. \"Got it, Jane\")\n" +
        "- Request an action (e.g. \"Schedule a follow-up for Jane\")";

    public async Task<string> HandleMessageAsync(
        string agentId,
        string agentFirstName,
        string body,
        string? leadName,
        CancellationToken ct)
    {
        // Load agent context for voice + personality + brand voice application
        if (agentContextLoader is not null && !string.IsNullOrEmpty(agentId))
        {
            try
            {
                var agentContext = await agentContextLoader.LoadAsync(agentId, agentId, ct);
                if (agentContext is not null)
                {
                    logger?.LogInformation(
                        "[CTX-030] Agent context loaded for WhatsApp conversation. AgentId: {AgentId}, " +
                        "HasVoice={HasVoice}, HasPersonality={HasPersonality}, HasBrandVoice={HasBrandVoice}",
                        agentId,
                        !string.IsNullOrEmpty(agentContext.VoiceSkill),
                        !string.IsNullOrEmpty(agentContext.PersonalitySkill),
                        !string.IsNullOrEmpty(agentContext.BrandVoice));
                }
                else
                {
                    logger?.LogInformation(
                        "[CTX-031] Agent context unavailable for WhatsApp — using generic responses. AgentId: {AgentId}",
                        agentId);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "[CTX-032] Agent context load failed for WhatsApp; continuing without. AgentId: {AgentId}",
                    agentId);
            }
        }

        // Stage 1: Classify intent with Haiku (fast + cheap)
        var classification = await classifier.ClassifyAsync(body, ct);

        if (!classification.InScope)
        {
            logger?.LogInformation("[WA-007] Out-of-scope message from {AgentId}: {Category}",
                agentId, classification.Category);
            var template = Deflections.GetValueOrDefault(
                classification.Category,
                Deflections[OutOfScopeCategory.NonReTopic]);
            return string.Format(template, agentFirstName);
        }

        // Stage 2: Route by intent type
        return classification.Intent switch
        {
            IntentType.Help => GetHelpText(),
            IntentType.Acknowledge =>
                $"Got it — I've marked {leadName ?? "the lead"} as acknowledged.",
            IntentType.LeadQuestion =>
                await generator.GenerateAsync(agentFirstName, body, leadName, ct),
            IntentType.ActionRequest =>
                await generator.GenerateAsync(agentFirstName, body, leadName, ct),
            _ => "I'm not sure how to help with that. Try asking about a lead by name."
        };
    }
}
