namespace RealEstateStar.Domain.Leads.Models;

/// <summary>
/// Subset of agent account.json + content.json passed in dispatch payloads.
/// Sized to stay under 64KB for future Azure Queue compatibility.
/// </summary>
public record AgentNotificationConfig
{
    public required string AgentId { get; init; }
    public required string Handle { get; init; }
    public required string Name { get; init; }
    public required string FirstName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public required string LicenseNumber { get; init; }
    public required string BrokerageName { get; init; }
    public string? BrokerageLogo { get; init; }
    public required string PrimaryColor { get; init; }
    public required string AccentColor { get; init; }
    public required string State { get; init; }
    public IReadOnlyList<string> ServiceAreas { get; init; } = [];
    public string? Bio { get; init; }
    public IReadOnlyList<string> Specialties { get; init; } = [];
    public IReadOnlyList<string> Testimonials { get; init; } = [];
    public string? WhatsAppPhoneNumberId { get; init; }
}
