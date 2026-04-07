namespace RealEstateStar.Domain.Leads.Models;

/// <summary>
/// Tracks a communication (email draft, WhatsApp message) through the pipeline.
/// Used for dedup: same ContentHash + Sent = skip on retry.
/// Persisted to the lead's storage folder by the PersistActivity.
/// </summary>
public record CommunicationRecord
{
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }

    /// <summary>"email", "whatsapp", "email-fallback"</summary>
    public required string Channel { get; init; }

    public required DateTimeOffset DraftedAt { get; init; }
    public DateTimeOffset? SentAt { get; set; }
    public bool Sent { get; set; }
    public string? Error { get; set; }

    /// <summary>SHA256 of draft inputs — dedup key. Same hash = same content, skip re-draft.</summary>
    public required string ContentHash { get; init; }

    /// <summary>BCP 47 locale code of the language used when drafting this communication (e.g., "en", "es").</summary>
    public string? Locale { get; init; }
}
