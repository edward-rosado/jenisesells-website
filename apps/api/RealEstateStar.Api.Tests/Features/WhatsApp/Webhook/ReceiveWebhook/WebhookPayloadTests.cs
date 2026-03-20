using System.Text.Json;
using FluentAssertions;
using RealEstateStar.Api.Features.WhatsApp.Webhook.ReceiveWebhook;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Webhook.ReceiveWebhook;

public class WebhookPayloadTests
{
    private const string SamplePayload = """
    {
      "object": "whatsapp_business_account",
      "entry": [{
        "id": "WABA_ID",
        "changes": [{
          "value": {
            "messaging_product": "whatsapp",
            "metadata": {
              "phone_number_id": "PHONE_ID",
              "display_phone_number": "+15551234567"
            },
            "messages": [{
              "id": "wamid.abc123",
              "from": "12015551234",
              "timestamp": "1700000000",
              "type": "text",
              "text": { "body": "What's her budget?" }
            }]
          },
          "field": "messages"
        }]
      }]
    }
    """;

    [Fact]
    public void Deserializes_TextMessage_FromMetaPayload()
    {
        var payload = JsonSerializer.Deserialize<WebhookPayload>(SamplePayload);
        payload.Should().NotBeNull();
        payload!.Object.Should().Be("whatsapp_business_account");
        var message = payload.GetFirstMessage();
        message.Should().NotBeNull();
        message!.Id.Should().Be("wamid.abc123");
        message.From.Should().Be("12015551234");
        message.Type.Should().Be("text");
        message.Text!.Body.Should().Be("What's her budget?");
    }

    [Fact]
    public void GetFirstMessage_ReturnsNull_WhenNoMessages()
    {
        var json = """{"object":"whatsapp_business_account","entry":[{"id":"x","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"phone_number_id":"x","display_phone_number":"x"},"statuses":[{"id":"wamid.1","status":"delivered","timestamp":"1700000000","recipient_id":"12345"}]},"field":"messages"}]}]}""";
        var payload = JsonSerializer.Deserialize<WebhookPayload>(json);
        payload!.GetFirstMessage().Should().BeNull();
    }

    [Fact]
    public void GetPhoneNumberId_ReturnsMetadataValue()
    {
        var payload = JsonSerializer.Deserialize<WebhookPayload>(SamplePayload);
        payload!.GetPhoneNumberId().Should().Be("PHONE_ID");
    }

    [Fact]
    public void GetFirstStatus_ReturnsStatus_WhenPresent()
    {
        var json = """{"object":"whatsapp_business_account","entry":[{"id":"x","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"phone_number_id":"x","display_phone_number":"x"},"statuses":[{"id":"wamid.1","status":"delivered","timestamp":"1700000000","recipient_id":"12345"}]},"field":"messages"}]}]}""";
        var payload = JsonSerializer.Deserialize<WebhookPayload>(json);
        var status = payload!.GetFirstStatus();
        status.Should().NotBeNull();
        status!.Status.Should().Be("delivered");
        status.Id.Should().Be("wamid.1");
        status.RecipientId.Should().Be("12345");
    }

    [Fact]
    public void GetFirstStatus_ReturnsNull_WhenNoStatuses()
    {
        var payload = JsonSerializer.Deserialize<WebhookPayload>(SamplePayload);
        payload!.GetFirstStatus().Should().BeNull();
    }
}
