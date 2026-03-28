using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.WhatsApp.Models;

namespace RealEstateStar.Workers.WhatsApp;

/// <summary>
/// Noop implementation of IIntentClassifier used until the Claude Haiku classifier
/// is wired up. Classifies all messages as in-scope LeadQuestion so the handler
/// falls through to the response generator.
/// </summary>
public class NoopIntentClassifier : IIntentClassifier
{
    public Task<IntentClassification> ClassifyAsync(string messageText, CancellationToken ct) =>
        // InScope: true means Category is not evaluated by ConversationHandler.
        Task.FromResult(new IntentClassification(IntentType.LeadQuestion, InScope: true, OutOfScopeCategory.NonReTopic));
}
