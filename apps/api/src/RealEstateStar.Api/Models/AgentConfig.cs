using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Models;

/// <summary>
/// Mirrors TypeScript AgentConfig from apps/agent-site/lib/types.ts.
/// Source of truth: config/agent.schema.json
/// </summary>
public sealed record AgentConfig
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("identity")]
    public required AgentIdentity Identity { get; init; }

    [JsonPropertyName("location")]
    public required AgentLocation Location { get; init; }

    [JsonPropertyName("branding")]
    public required AgentBranding Branding { get; init; }

    [JsonPropertyName("integrations")]
    public AgentIntegrations? Integrations { get; init; }

    [JsonPropertyName("compliance")]
    public AgentCompliance? Compliance { get; init; }
}

public sealed record AgentIdentity
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("license_id")]
    public string? LicenseId { get; init; }

    [JsonPropertyName("brokerage")]
    public string? Brokerage { get; init; }

    [JsonPropertyName("brokerage_id")]
    public string? BrokerageId { get; init; }

    [JsonPropertyName("phone")]
    public required string Phone { get; init; }

    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("website")]
    public string? Website { get; init; }

    [JsonPropertyName("headshot_url")]
    public string? HeadshotUrl { get; init; }

    [JsonPropertyName("languages")]
    public string[]? Languages { get; init; }

    [JsonPropertyName("tagline")]
    public string? Tagline { get; init; }
}

public sealed record AgentLocation
{
    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("office_address")]
    public string? OfficeAddress { get; init; }

    [JsonPropertyName("service_areas")]
    public string[]? ServiceAreas { get; init; }
}

public sealed record AgentBranding
{
    [JsonPropertyName("primary_color")]
    public string? PrimaryColor { get; init; }

    [JsonPropertyName("secondary_color")]
    public string? SecondaryColor { get; init; }

    [JsonPropertyName("accent_color")]
    public string? AccentColor { get; init; }

    [JsonPropertyName("font_family")]
    public string? FontFamily { get; init; }

    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EmailProvider { Gmail, Outlook, Smtp }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormHandler { Formspree, Custom }

public sealed record AgentIntegrations
{
    [JsonPropertyName("email_provider")]
    public EmailProvider? EmailProvider { get; init; }

    [JsonPropertyName("hosting")]
    public string? Hosting { get; init; }

    [JsonPropertyName("form_handler")]
    public FormHandler? FormHandler { get; init; }

    [JsonPropertyName("form_handler_id")]
    public string? FormHandlerId { get; init; }

    [JsonPropertyName("tracking")]
    public AgentTracking? Tracking { get; init; }
}

public sealed record AgentTracking
{
    [JsonPropertyName("google_analytics_id")]
    public string? GoogleAnalyticsId { get; init; }

    [JsonPropertyName("google_ads_id")]
    public string? GoogleAdsId { get; init; }

    [JsonPropertyName("google_ads_conversion_label")]
    public string? GoogleAdsConversionLabel { get; init; }

    [JsonPropertyName("meta_pixel_id")]
    public string? MetaPixelId { get; init; }

    [JsonPropertyName("gtm_container_id")]
    public string? GtmContainerId { get; init; }
}

public sealed record AgentCompliance
{
    [JsonPropertyName("state_form")]
    public string? StateForm { get; init; }

    [JsonPropertyName("licensing_body")]
    public string? LicensingBody { get; init; }

    [JsonPropertyName("disclosure_requirements")]
    public string[]? DisclosureRequirements { get; init; }
}
