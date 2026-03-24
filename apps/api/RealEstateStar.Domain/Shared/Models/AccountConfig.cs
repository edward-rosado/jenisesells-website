using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Shared.Models;

/// <summary>
/// Maps to config/accounts/{handle}/account.json
/// </summary>
public class AccountConfig
{
    [JsonPropertyName("handle")]
    public string Handle { get; init; } = "";

    [JsonPropertyName("accountId")]
    public string? RawAccountId { get; init; }

    public string AccountId => RawAccountId ?? Handle;

    [JsonPropertyName("template")]
    public string? Template { get; init; }

    [JsonPropertyName("agent")]
    public AccountAgent? Agent { get; init; }

    [JsonPropertyName("brokerage")]
    public AccountBrokerage? Brokerage { get; init; }

    [JsonPropertyName("location")]
    public AccountLocation? Location { get; init; }

    [JsonPropertyName("branding")]
    public AccountBranding? Branding { get; init; }

    [JsonPropertyName("integrations")]
    public AccountIntegrations? Integrations { get; init; }

    [JsonPropertyName("compliance")]
    public AccountCompliance? Compliance { get; init; }

    [JsonPropertyName("contact_info")]
    public List<ContactInfo>? ContactInfo { get; init; }
}

public class AccountAgent
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("phone")]
    public string Phone { get; init; } = "";

    [JsonPropertyName("email")]
    public string Email { get; init; } = "";

    [JsonPropertyName("headshot_url")]
    public string? HeadshotUrl { get; init; }

    [JsonPropertyName("license_number")]
    public string? LicenseNumber { get; init; }

    [JsonPropertyName("languages")]
    public List<string> Languages { get; init; } = [];

    [JsonPropertyName("tagline")]
    public string? Tagline { get; init; }

    [JsonPropertyName("credentials")]
    public List<string>? Credentials { get; init; }
}

public class AccountBrokerage
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("license_number")]
    public string? LicenseNumber { get; init; }

    [JsonPropertyName("office_phone")]
    public string? OfficePhone { get; init; }

    [JsonPropertyName("office_address")]
    public string? OfficeAddress { get; init; }
}

public class AccountLocation
{
    [JsonPropertyName("state")]
    public string State { get; init; } = "";

    [JsonPropertyName("service_areas")]
    public List<string> ServiceAreas { get; init; } = [];
}

public class AccountBranding
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

public class AccountIntegrations
{
    [JsonPropertyName("email_provider")]
    public string? EmailProvider { get; init; }

    [JsonPropertyName("hosting")]
    public string? Hosting { get; init; }

    [JsonPropertyName("form_handler")]
    public string? FormHandler { get; init; }

    [JsonPropertyName("form_handler_id")]
    public string? FormHandlerId { get; init; }

    [JsonPropertyName("chat_webhook_url")]
    public string? ChatWebhookUrl { get; init; }

    [JsonPropertyName("whatsapp")]
    public AccountWhatsApp? WhatsApp { get; init; }
}

public class AccountWhatsApp
{
    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; init; } = "";

    [JsonPropertyName("opted_in")]
    public bool OptedIn { get; init; }

    [JsonPropertyName("notification_preferences")]
    public List<string> NotificationPreferences { get; init; } = ["new_lead", "cma_ready", "data_deletion"];

    [JsonPropertyName("status")]
    public string Status { get; set; } = "not_registered";

    [JsonPropertyName("welcome_sent")]
    public bool WelcomeSent { get; set; }

    [JsonPropertyName("retry_after")]
    public DateTime? RetryAfter { get; set; }
}

public class AccountCompliance
{
    [JsonPropertyName("state_form")]
    public string? StateForm { get; init; }

    [JsonPropertyName("licensing_body")]
    public string? LicensingBody { get; init; }

    [JsonPropertyName("disclosure_requirements")]
    public List<string> DisclosureRequirements { get; init; } = [];
}

public class ContactInfo
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("value")]
    public string Value { get; init; } = "";

    [JsonPropertyName("ext")]
    public string? Ext { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("is_preferred")]
    public bool IsPreferred { get; init; }
}
