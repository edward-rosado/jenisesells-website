using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Leads;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Leads.Tests;

public class LeadProcessingWorkerTests
{
    private readonly LeadProcessingChannel _channel = new();
    private readonly Mock<ILeadStore> _leadStore = new();
    private readonly Mock<ILeadEnricher> _enricher = new();
    private readonly Mock<ILeadNotifier> _notifier = new();
    private readonly Mock<IFailedNotificationStore> _failedNotificationStore = new();
    private readonly BackgroundServiceHealthTracker _healthTracker = new();
    private readonly Mock<ILogger<LeadProcessingWorker>> _logger = new();

    /// <summary>
    /// Creates an IConfiguration that configures Pipeline:Lead:Retry with zero delays and the given MaxRetries.
    /// When maxRetries is null, no retry config is set so PipelineWorker uses its defaults.
    /// </summary>
    private static IConfiguration MakeConfig(int? maxRetries = null, int baseDelaySeconds = 0)
    {
        var pairs = new Dictionary<string, string?>();
        if (maxRetries.HasValue)
        {
            pairs["Pipeline:Lead:Retry:MaxRetries"] = maxRetries.Value.ToString();
            pairs["Pipeline:Lead:Retry:BaseDelaySeconds"] = baseDelaySeconds.ToString();
        }
        return new ConfigurationBuilder().AddInMemoryCollection(pairs).Build();
    }

    private LeadProcessingWorker CreateWorker(IConfiguration? config = null)
    {
        // Default mock setups
        _notifier.Setup(n => n.BuildSubject(It.IsAny<Lead>(), It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>()))
            .Returns("Test Subject");
        _notifier.Setup(n => n.BuildBody(It.IsAny<Lead>(), It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>()))
            .Returns("Test Body");

        return new(_channel, _leadStore.Object, _enricher.Object,
            _notifier.Object, _failedNotificationStore.Object,
            _healthTracker, _logger.Object, config ?? MakeConfig());
    }

    private static Lead MakeLead(LeadType type = LeadType.Seller) => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "test-agent",
        LeadType = type,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        Timeline = "1-3months",
        ReceivedAt = DateTime.UtcNow,
        SellerDetails = type is LeadType.Seller or LeadType.Both
            ? new SellerDetails { Address = "123 Main St", City = "Springfield", State = "NJ", Zip = "07081" }
            : null,
        BuyerDetails = type is LeadType.Buyer or LeadType.Both
            ? new BuyerDetails { City = "Springfield", State = "NJ" }
            : null,
    };

    private LeadProcessingRequest MakeRequest(Lead? lead = null) =>
        new("test-agent", lead ?? MakeLead(), "corr-123");

    [Fact]
    public async Task ProcessesLead_EnrichesAndNotifies()
    {
        _enricher
            .Setup(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadEnrichment.Empty(), LeadScore.Default("test")));
        _notifier
            .Setup(n => n.NotifyAgentAsync(It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        _enricher.Verify(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.NotifyAgentAsync(
            "test-agent", It.IsAny<Lead>(),
            It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnrichmentFailure_CausesWorkerToRetry_NotificationNotCalledOnFailedAttempt()
    {
        // Enrichment always fails — notification should never be called (pipeline never reaches notify step)
        _enricher
            .Setup(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("scraper down"));

        // Use zero-delay retries with MaxRetries=0 so it fails fast
        var worker = CreateWorker(MakeConfig(maxRetries: 0));
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Notification is NOT called — pipeline failed at enrichment step
        _notifier.Verify(n => n.NotifyAgentAsync(
            It.IsAny<string>(), It.IsAny<Lead>(),
            It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Notification_SucceedsOnFirstAttempt_DoesNotRetry()
    {
        _enricher
            .Setup(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadEnrichment.Empty(), LeadScore.Default("test")));
        _notifier
            .Setup(n => n.NotifyAgentAsync(It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Called exactly once — no retries
        _notifier.Verify(n => n.NotifyAgentAsync(
            It.IsAny<string>(), It.IsAny<Lead>(),
            It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Dead letter store is NOT called
        _failedNotificationStore.Verify(
            s => s.RecordAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Notification_SucceedsOnSecondAttempt_RetriesOnceOnly()
    {
        _enricher
            .Setup(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadEnrichment.Empty(), LeadScore.Default("test")));

        var callCount = 0;
        _notifier
            .Setup(n => n.NotifyAgentAsync(It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1
                    ? Task.FromException(new HttpRequestException("smtp timeout"))
                    : Task.CompletedTask;
            });

        // Zero-delay retries so the test doesn't wait
        var worker = CreateWorker(MakeConfig(maxRetries: 3));
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Called twice — original attempt + one retry
        _notifier.Verify(n => n.NotifyAgentAsync(
            It.IsAny<string>(), It.IsAny<Lead>(),
            It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        // Dead letter store is NOT called (succeeded on retry)
        _failedNotificationStore.Verify(
            s => s.RecordAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Notification_PermanentlyFailed_WritesToDeadLetterAfterAllRetries()
    {
        _enricher
            .Setup(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadEnrichment.Empty(), LeadScore.Default("test")));
        _notifier
            .Setup(n => n.NotifyAgentAsync(It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("smtp down"));
        _failedNotificationStore
            .Setup(s => s.RecordAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Zero-delay retries with MaxRetries=3 → 1 initial + 3 retries = 4 total attempts
        var lead = MakeLead();
        var worker = CreateWorker(MakeConfig(maxRetries: 3));
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Called 4 times: 1 initial + 3 retries
        _notifier.Verify(n => n.NotifyAgentAsync(
            It.IsAny<string>(), It.IsAny<Lead>(),
            It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(),
            It.IsAny<CancellationToken>()), Times.Exactly(4));

        // Dead letter store called once
        _failedNotificationStore.Verify(
            s => s.RecordAsync("test-agent", lead.Id,
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Notification_PermanentlyFailed_DoesNotCrashWorker()
    {
        _enricher
            .Setup(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadEnrichment.Empty(), LeadScore.Default("test")));
        _notifier
            .Setup(n => n.NotifyAgentAsync(It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("smtp down"));
        _failedNotificationStore
            .Setup(s => s.RecordAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = CreateWorker(MakeConfig(maxRetries: 1));
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        // Should complete without throwing
        await worker.StartAsync(cts.Token);
        var act = async () => await worker.ExecuteTask!;
        await act.Should().NotThrowAsync();
    }
}
