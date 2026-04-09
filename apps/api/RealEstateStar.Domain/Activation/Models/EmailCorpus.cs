namespace RealEstateStar.Domain.Activation.Models;

public sealed record EmailCorpus(
    IReadOnlyList<EmailMessage> SentEmails,
    IReadOnlyList<EmailMessage> InboxEmails,
    EmailSignature? Signature,
    IReadOnlyList<string> DiscoveredProfileUrls = default!)
{
    /// <summary>Defaults to empty list when not provided (backward compat).</summary>
    public IReadOnlyList<string> DiscoveredProfileUrls { get; init; } = DiscoveredProfileUrls ?? [];
}

public sealed record EmailMessage(
    string Id,
    string Subject,
    string Body,
    string From,
    string[] To,
    DateTime Date,
    string? SignatureBlock,
    string? DetectedLocale = null);

public sealed record EmailSignature(
    string? Name,
    string? Title,
    string? Phone,
    string? LicenseNumber,
    string? BrokerageName,
    IReadOnlyList<string> SocialLinks,
    string? HeadshotUrl,
    string? WebsiteUrl,
    string? LogoUrl);
