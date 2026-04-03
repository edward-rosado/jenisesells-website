using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Functions.WhatsApp;

namespace RealEstateStar.Functions.Tests.WhatsApp;

public class ProcessWebhookFunctionTests
{
    private readonly Mock<IConversationHandler> _handler = new();
    private readonly Mock<IWhatsAppSender> _sender = new();
    private readonly Mock<IWhatsAppAuditService> _audit = new();
    private readonly Mock<ILogger<ProcessWebhookFunction>> _logger = new();

    private ProcessWebhookFunction CreateSut() =>
        new(_handler.Object, _sender.Object, _audit.Object, _logger.Object);

    private static string MakeJsonBody(WebhookEnvelope envelope) =>
        JsonSerializer.Serialize(envelope);

    private static string MakeBase64Body(WebhookEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    // ── DeserializeEnvelope tests ────────────────────────────────────────────

    [Fact]
    public void DeserializeEnvelope_ParsesDirectJson()
    {
        var envelope = new WebhookEnvelope("wamid.1", "12015551234", "Hello!", DateTime.UtcNow, "PHONE_ID");
        var json = JsonSerializer.Serialize(envelope);

        var result = ProcessWebhookFunction.DeserializeEnvelope(json);

        Assert.Equal("wamid.1", result.MessageId);
        Assert.Equal("12015551234", result.FromPhone);
        Assert.Equal("Hello!", result.Body);
    }

    [Fact]
    public void DeserializeEnvelope_ParsesBase64EncodedJson()
    {
        var envelope = new WebhookEnvelope("wamid.2", "12015559999", "Base64 message", DateTime.UtcNow);
        var base64 = MakeBase64Body(envelope);

        var result = ProcessWebhookFunction.DeserializeEnvelope(base64);

        Assert.Equal("wamid.2", result.MessageId);
        Assert.Equal("Base64 message", result.Body);
    }

    [Fact]
    public void DeserializeEnvelope_ThrowsOnMalformedJson()
    {
        Assert.Throws<JsonException>(() => ProcessWebhookFunction.DeserializeEnvelope("{not-valid-json"));
    }

    [Fact]
    public void DeserializeEnvelope_ThrowsOnMalformedBase64()
    {
        Assert.Throws<FormatException>(() => ProcessWebhookFunction.DeserializeEnvelope("!!!not-base64!!!"));
    }

    // ── RunAsync happy path ──────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_DelegatesTo_ConversationHandler()
    {
        var envelope = new WebhookEnvelope("wamid.10", "12015551111", "What's her budget?",
            DateTime.UtcNow, "PHONE_ID");
        var json = MakeJsonBody(envelope);

        _handler.Setup(h => h.HandleMessageAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                "What's her budget?", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Her budget is $650K.");

        var sut = CreateSut();
        await sut.RunAsync(json, dequeueCount: 1, id: "q1", CancellationToken.None);

        _handler.Verify(h => h.HandleMessageAsync(
            string.Empty, string.Empty, "What's her budget?", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_SendsFreeform_WhenHandlerReturnsResponse()
    {
        var envelope = new WebhookEnvelope("wamid.11", "12015552222", "Tell me about the lead",
            DateTime.UtcNow, "PHONE_ID");
        var json = MakeJsonBody(envelope);

        _handler.Setup(h => h.HandleMessageAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Jane Smith, budget $500K.");

        var sut = CreateSut();
        await sut.RunAsync(json, dequeueCount: 1, id: "q1", CancellationToken.None);

        _sender.Verify(s => s.SendFreeformAsync(
            "+12015552222", "Jane Smith, budget $500K.", It.IsAny<CancellationToken>()),
            Times.Once);
        _sender.Verify(s => s.MarkReadAsync("wamid.11", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_DoesNotSend_WhenHandlerReturnsEmpty()
    {
        var envelope = new WebhookEnvelope("wamid.12", "12015553333", "Ignore me",
            DateTime.UtcNow, "PHONE_ID");
        var json = MakeJsonBody(envelope);

        _handler.Setup(h => h.HandleMessageAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var sut = CreateSut();
        await sut.RunAsync(json, dequeueCount: 1, id: "q1", CancellationToken.None);

        _sender.Verify(s => s.SendFreeformAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sender.Verify(s => s.MarkReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_UpdatesAudit_OnSuccess()
    {
        var envelope = new WebhookEnvelope("wamid.13", "12015554444", "Hello",
            DateTime.UtcNow, "PHONE_ID");
        var json = MakeJsonBody(envelope);

        _handler.Setup(h => h.HandleMessageAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Hi there!");

        var sut = CreateSut();
        await sut.RunAsync(json, dequeueCount: 1, id: "q1", CancellationToken.None);

        _audit.Verify(a => a.UpdateProcessingAsync("wamid.13", string.Empty, It.IsAny<CancellationToken>()),
            Times.Once);
        _audit.Verify(a => a.UpdateCompletedAsync("wamid.13", string.Empty, string.Empty, "Hi there!", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Error handling ───────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AuditsFailure_AndRethrows_OnHandlerException()
    {
        var envelope = new WebhookEnvelope("wamid.20", "12015555555", "Crash me",
            DateTime.UtcNow, "PHONE_ID");
        var json = MakeJsonBody(envelope);
        var ex = new Exception("Claude API down");

        _handler.Setup(h => h.HandleMessageAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ex);

        var sut = CreateSut();
        var thrown = await Assert.ThrowsAsync<Exception>(() =>
            sut.RunAsync(json, dequeueCount: 1, id: "q1", CancellationToken.None));

        Assert.Same(ex, thrown);
        _audit.Verify(a => a.UpdateFailedAsync("wamid.20", null, "Claude API down", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ThrowsAndAuditsPoison_WhenDequeueCountAtThreshold()
    {
        var envelope = new WebhookEnvelope("wamid.30", "12015556666", "Bad message",
            DateTime.UtcNow, "PHONE_ID");
        var json = MakeJsonBody(envelope);

        var sut = CreateSut();
        // dequeueCount >= PoisonThreshold (5) → audit poison + re-throw so host routes to -poison queue
        // (returning would tell the host the message was successfully processed and it would delete it)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RunAsync(json, dequeueCount: 5, id: "q1", CancellationToken.None));

        Assert.Contains("wamid.30", ex.Message);
        Assert.Contains("-poison routing", ex.Message);

        _audit.Verify(a => a.UpdatePoisonAsync(
            "wamid.30",
            It.Is<string>(s => s.Contains("5")),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Handler must NOT be invoked for poison messages
        _handler.Verify(h => h.HandleMessageAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_ThrowsDeserializationException_WhenMessageMalformed()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<JsonException>(() =>
            sut.RunAsync("{invalid", dequeueCount: 1, id: "q1", CancellationToken.None));

        // Handler must NOT be called if deserialization fails
        _handler.Verify(h => h.HandleMessageAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_AcceptsBase64EncodedMessage()
    {
        var envelope = new WebhookEnvelope("wamid.40", "12015557777", "Base64 test",
            DateTime.UtcNow, "PHONE_ID");
        var base64 = MakeBase64Body(envelope);

        _handler.Setup(h => h.HandleMessageAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                "Base64 test", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Got it!");

        var sut = CreateSut();
        await sut.RunAsync(base64, dequeueCount: 1, id: "q1", CancellationToken.None);

        _handler.Verify(h => h.HandleMessageAsync(
            string.Empty, string.Empty, "Base64 test", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
