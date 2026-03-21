using RealEstateStar.Domain.WhatsApp.Models;
namespace RealEstateStar.Domain.WhatsApp.Interfaces;

/// <summary>
/// Classifies the intent and scope of an inbound WhatsApp message.
/// Production implementation calls Claude Haiku (fast, cheap).
/// </summary>
public interface IIntentClassifier
{
    Task<IntentClassification> ClassifyAsync(string messageText, CancellationToken ct);
}

/// <summary>
/// Result of a two-stage intent classification: intent type, whether the message is
/// in-scope for the assistant, and (if out-of-scope) which deflection category applies.
/// </summary>
public record IntentClassification(IntentType Intent, bool InScope, OutOfScopeCategory Category);
