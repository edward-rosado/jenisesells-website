using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.WhatsApp.Webhook.ReceiveWebhook;

public class WebhookPayload
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = "";

    [JsonPropertyName("entry")]
    public List<WebhookEntry> Entry { get; init; } = [];

    public WebhookMessage? GetFirstMessage() =>
        Entry.FirstOrDefault()?.Changes.FirstOrDefault()?.Value.Messages?.FirstOrDefault();

    public WebhookStatus? GetFirstStatus() =>
        Entry.FirstOrDefault()?.Changes.FirstOrDefault()?.Value.Statuses?.FirstOrDefault();

    public string? GetPhoneNumberId() =>
        Entry.FirstOrDefault()?.Changes.FirstOrDefault()?.Value.Metadata?.PhoneNumberId;
}

public class WebhookEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("changes")]
    public List<WebhookChange> Changes { get; init; } = [];
}

public class WebhookChange
{
    [JsonPropertyName("value")]
    public WebhookValue Value { get; init; } = new();

    [JsonPropertyName("field")]
    public string Field { get; init; } = "";
}

public class WebhookValue
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; init; } = "";

    [JsonPropertyName("metadata")]
    public WebhookMetadata? Metadata { get; init; }

    [JsonPropertyName("messages")]
    public List<WebhookMessage>? Messages { get; init; }

    [JsonPropertyName("statuses")]
    public List<WebhookStatus>? Statuses { get; init; }
}

public class WebhookStatus
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = "";

    [JsonPropertyName("recipient_id")]
    public string RecipientId { get; init; } = "";
}

public class WebhookMetadata
{
    [JsonPropertyName("phone_number_id")]
    public string PhoneNumberId { get; init; } = "";

    [JsonPropertyName("display_phone_number")]
    public string DisplayPhoneNumber { get; init; } = "";
}

public class WebhookMessage
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("from")]
    public string From { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("text")]
    public WebhookText? Text { get; init; }
}

public class WebhookText
{
    [JsonPropertyName("body")]
    public string Body { get; init; } = "";
}
