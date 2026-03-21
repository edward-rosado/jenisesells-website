using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.DataServices.WhatsApp;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Services;

public class WebhookProcessorWorkerTests
{
    private readonly Mock<IWebhookQueueService> _queue = new();
    private readonly Mock<IConversationHandler> _handler = new();
    private readonly Mock<IWhatsAppSender> _whatsAppClient = new();
    private readonly Mock<IWhatsAppAuditService> _audit = new();
    private readonly Mock<ILogger<WebhookProcessorWorker>> _logger = new();

    [Fact]
    public async Task ProcessesMessage_AndDeletesFromQueue_OnSuccess()
    {
        var envelope = new WebhookEnvelope("wamid.1", "12015551234", "What's her budget?",
            DateTime.UtcNow, "PHONE_ID");
        _queue.SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueuedMessage<WebhookEnvelope>(envelope, "qid", "pop", 1))
            .ReturnsAsync((QueuedMessage<WebhookEnvelope>?)null);
        _handler.Setup(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            "What's her budget?", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Her budget is $650K.");

        var worker = CreateWorker();
        await worker.ProcessOnceAsync(CancellationToken.None);

        _queue.Verify(q => q.CompleteAsync("qid", "pop", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LeavesMessage_InQueue_OnFailure_ForRetry()
    {
        var envelope = new WebhookEnvelope("wamid.2", "12015551234", "Hi",
            DateTime.UtcNow, "PHONE_ID");
        _queue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueuedMessage<WebhookEnvelope>(envelope, "qid", "pop", 1));
        _handler.Setup(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Claude API down"));

        var worker = CreateWorker();
        await worker.ProcessOnceAsync(CancellationToken.None);

        // Message NOT deleted — becomes visible again after visibility timeout
        _queue.Verify(q => q.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogsPoisonMessage_WhenDequeueCountExceeds5()
    {
        var envelope = new WebhookEnvelope("wamid.3", "12015551234", "Hi",
            DateTime.UtcNow, "PHONE_ID");
        _queue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueuedMessage<WebhookEnvelope>(envelope, "qid", "pop", 6));

        var worker = CreateWorker();
        await worker.ProcessOnceAsync(CancellationToken.None);

        // Should complete (remove from main queue) and log poison warning
        _queue.Verify(q => q.CompleteAsync("qid", "pop", It.IsAny<CancellationToken>()),
            Times.Once);
        // Handler should never be called for poison messages
        _handler.Verify(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DoesNothing_WhenQueueEmpty()
    {
        _queue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueuedMessage<WebhookEnvelope>?)null);

        var worker = CreateWorker();
        await worker.ProcessOnceAsync(CancellationToken.None);

        _handler.Verify(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendsResponse_ViaWhatsApp_AfterHandling()
    {
        var envelope = new WebhookEnvelope("wamid.4", "12015551234", "Tell me about the lead",
            DateTime.UtcNow, "PHONE_ID");
        _queue.SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueuedMessage<WebhookEnvelope>(envelope, "qid", "pop", 1))
            .ReturnsAsync((QueuedMessage<WebhookEnvelope>?)null);
        _handler.Setup(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Budget is $500K, pre-approved.");

        var worker = CreateWorker();
        await worker.ProcessOnceAsync(CancellationToken.None);

        _whatsAppClient.Verify(c => c.SendFreeformAsync(
            It.Is<string>(s => s.Contains("12015551234")),
            "Budget is $500K, pre-approved.",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private WebhookProcessorWorker CreateWorker() =>
        new(_queue.Object, _handler.Object, _whatsAppClient.Object, _audit.Object, _logger.Object);
}
