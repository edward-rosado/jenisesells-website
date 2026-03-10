using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Common;

public class AgentConfig
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("identity")]
    public AgentIdentity? Identity { get; init; }

    [JsonPropertyName("location")]
    public AgentLocation? Location { get; init; }

    [JsonPropertyName("branding")]
    public AgentBranding? Branding { get; init; }

    [JsonPropertyName("integrations")]
    public AgentIntegrations? Integrations { get; init; }

    [JsonPropertyName("compliance")]
    public AgentCompliance? Compliance { get; init; }
}

public class AgentIdentity
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("license_id")]
    public string? LicenseId { get; init; }

    [JsonPropertyName("brokerage")]
    public string? Brokerage { get; init; }

    [JsonPropertyName("brokerage_id")]
    public string? BrokerageId { get; init; }

    [JsonPropertyName("phone")]
    public string Phone { get; init; } = "";

    [JsonPropertyName("email")]
    public string Email { get; init; } = "";

    [JsonPropertyName("website")]
    public string? Website { get; init; }

    [JsonPropertyName("languages")]
    public List<string> Languages { get; init; } = [];

    [JsonPropertyName("tagline")]
    public string? Tagline { get; init; }
}

public class AgentLocation
{
    [JsonPropertyName("state")]
    public string State { get; init; } = "";

    [JsonPropertyName("office_address")]
    public string? OfficeAddress { get; init; }

    [JsonPropertyName("service_areas")]
    public List<string> ServiceAreas { get; init; } = [];
}

public class AgentBranding
{
    [JsonPropertyName("primary_color")]
    public string? PrimaryColor { get; init; }

    [JsonPropertyName("secondary_color")]
    public string? SecondaryColor { get; init; }

    [JsonPropertyName("accent_color")]
    public string? AccentColor { get; init; }

    [JsonPropertyName("font_family")]
    public string? FontFamily { get; init; }
}

public class AgentIntegrations
{
    [JsonPropertyName("email_provider")]
    public string? EmailProvider { get; init; }

    [JsonPropertyName("hosting")]
    public string? Hosting { get; init; }

    [JsonPropertyName("form_handler")]
    public string? FormHandler { get; init; }

    [JsonPropertyName("form_handler_id")]
    public string? FormHandlerId { get; init; }
}

public class AgentCompliance
{
    [JsonPropertyName("state_form")]
    public string? StateForm { get; init; }

    [JsonPropertyName("licensing_body")]
    public string? LicensingBody { get; init; }

    [JsonPropertyName("disclosure_requirements")]
    public List<string> DisclosureRequirements { get; init; } = [];
}
