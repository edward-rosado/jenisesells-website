using System.Text.Json;
using FluentAssertions;

namespace RealEstateStar.DataServices.Tests.WhatsApp;

public class WebhookQueueServiceTests
{
    [Fact]
    public void WebhookEnvelope_RoundTrips_JsonSerialization()
    {
        var envelope = new WebhookEnvelope("wamid.abc", "+12015551234", "Hi there",
            new DateTime(2026, 3, 19, 14, 0, 0, DateTimeKind.Utc), "PHONE_ID", "trace-123");

        var json = JsonSerializer.Serialize(envelope);
        var deserialized = JsonSerializer.Deserialize<WebhookEnvelope>(json);

        deserialized.Should().NotBeNull();
        deserialized!.MessageId.Should().Be("wamid.abc");
        deserialized.FromPhone.Should().Be("+12015551234");
        deserialized.Body.Should().Be("Hi there");
        deserialized.PhoneNumberId.Should().Be("PHONE_ID");
        deserialized.TraceId.Should().Be("trace-123");
    }

    [Fact]
    public void WebhookEnvelope_Base64RoundTrip()
    {
        var envelope = new WebhookEnvelope("wamid.abc", "+12015551234", "Hello",
            DateTime.UtcNow);
        var json = JsonSerializer.Serialize(envelope);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        var result = JsonSerializer.Deserialize<WebhookEnvelope>(decoded);

        result.Should().NotBeNull();
        result!.MessageId.Should().Be("wamid.abc");
    }

    [Fact]
    public void QueuedMessage_HoldsEnvelopeAndMetadata()
    {
        var envelope = new WebhookEnvelope("wamid.1", "123", "test", DateTime.UtcNow);
        var queued = new QueuedMessage<WebhookEnvelope>(envelope, "queue-msg-id", "pop-receipt", 3);

        queued.Value.MessageId.Should().Be("wamid.1");
        queued.QueueMessageId.Should().Be("queue-msg-id");
        queued.PopReceipt.Should().Be("pop-receipt");
        queued.DequeueCount.Should().Be(3);
    }

    [Fact]
    public void WebhookEnvelope_OptionalFields_DefaultToNull()
    {
        var envelope = new WebhookEnvelope("wamid.1", "123", "test", DateTime.UtcNow);
        envelope.PhoneNumberId.Should().BeNull();
        envelope.TraceId.Should().BeNull();
    }
}
