using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Workers.Cma;
using RealEstateStar.Workers.HomeSearch;
using RealEstateStar.Workers.Leads;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Leads.Tests;

public class LeadProcessingWorkerTests
{
    private readonly LeadProcessingChannel _channel = new();
    private readonly CmaProcessingChannel _cmaChannel = new();
    private readonly HomeSearchProcessingChannel _homeSearchChannel = new();
    private readonly Mock<ILeadStore> _leadStore = new();
    private readonly Mock<ILeadEnricher> _enricher = new();
    private readonly Mock<ILeadNotifier> _notifier = new();
    private readonly Mock<IFailedNotificationStore> _failedNotificationStore = new();
    private readonly Mock<IFileStorageProvider> _storage = new();
    private readonly BackgroundServiceHealthTracker _healthTracker = new();
    private readonly Mock<ILogger<LeadProcessingWorker>> _logger = new();

    // Zero-delay retries so tests don't wait 30–90 seconds in production delays
    private static readonly TimeSpan[] ZeroDelays =
    [
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
    ];

    private LeadProcessingWorker CreateWorker()
    {
        // Default mock setups for checkpoint/resume pattern
        _notifier.Setup(n => n.BuildSubject(It.IsAny<Lead>(), It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>()))
            .Returns("Test Subject");
        _notifier.Setup(n => n.BuildBody(It.IsAny<Lead>(), It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>()))
            .Returns("Test Body");

        // Storage: no checkpoint files exist by default (steps run fresh)
        _storage.Setup(s => s.ReadDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new(_channel, _leadStore.Object, _enricher.Object,
            _notifier.Object, _failedNotificationStore.Object,
            _storage.Object,
            _cmaChannel, _homeSearchChannel, _healthTracker, _logger.Object)
        {
            RetryDelays = ZeroDelays,
        };
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
    public async Task ContinuesNotification_WhenEnrichmentFails()
    {
        _enricher
            .Setup(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("scraper down"));
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

        // Notification still fires even when enrichment failed
        _notifier.Verify(n => n.NotifyAgentAsync(
            It.IsAny<string>(), It.IsAny<Lead>(),
            It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Error was logged with [WORKER-021]
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[WORKER-021]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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

        var worker = CreateWorker();
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

        // Retry logged with [WORKER-032]
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[WORKER-032]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

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

        var lead = MakeLead();
        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Called 4 times: 1 initial + 3 retries (ZeroDelays has 3 entries)
        _notifier.Verify(n => n.NotifyAgentAsync(
            It.IsAny<string>(), It.IsAny<Lead>(),
            It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(),
            It.IsAny<CancellationToken>()), Times.Exactly(4));

        // Dead letter store called once with correct agentId and leadId
        _failedNotificationStore.Verify(
            s => s.RecordAsync("test-agent", lead.Id,
                It.Is<string>(msg => msg.Contains("smtp down")),
                4, It.IsAny<CancellationToken>()),
            Times.Once);

        // Permanent failure logged with [WORKER-034]
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[WORKER-034]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
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

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        // Should complete without throwing
        await worker.StartAsync(cts.Token);
        var act = async () => await worker.ExecuteTask!;
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DispatchesCma_ForSellerLead()
    {
        _enricher
            .Setup(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadEnrichment.Empty(), LeadScore.Default("test")));
        _notifier
            .Setup(n => n.NotifyAgentAsync(It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var lead = MakeLead(LeadType.Seller);
        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // CMA channel should have a request
        _cmaChannel.Reader.TryRead(out var cmaRequest).Should().BeTrue();
        cmaRequest!.AgentId.Should().Be("test-agent");
        cmaRequest.Lead.Id.Should().Be(lead.Id);
        cmaRequest.CorrelationId.Should().Be("corr-123");

        // Home search channel should be empty
        _homeSearchChannel.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task DispatchesHomeSearch_ForBuyerLead()
    {
        _enricher
            .Setup(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadEnrichment.Empty(), LeadScore.Default("test")));
        _notifier
            .Setup(n => n.NotifyAgentAsync(It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var lead = MakeLead(LeadType.Buyer);
        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Home search channel should have a request
        _homeSearchChannel.Reader.TryRead(out var hsRequest).Should().BeTrue();
        hsRequest!.AgentId.Should().Be("test-agent");
        hsRequest.Lead.Id.Should().Be(lead.Id);

        // CMA channel should be empty (no seller details)
        _cmaChannel.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task DispatchesBothPipelines_ForBothLead()
    {
        _enricher
            .Setup(e => e.EnrichAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadEnrichment.Empty(), LeadScore.Default("test")));
        _notifier
            .Setup(n => n.NotifyAgentAsync(It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var lead = MakeLead(LeadType.Both);
        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Both channels should have requests
        _cmaChannel.Reader.TryRead(out var cmaRequest).Should().BeTrue();
        cmaRequest!.Lead.Id.Should().Be(lead.Id);

        _homeSearchChannel.Reader.TryRead(out var hsRequest).Should().BeTrue();
        hsRequest!.Lead.Id.Should().Be(lead.Id);
    }

    [Fact]
    public async Task SkipsBothPipelines_ForSellerOnly_NoHomeSearch()
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

        await _channel.Writer.WriteAsync(MakeRequest(MakeLead(LeadType.Seller)), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // CMA dispatched, home search not
        _cmaChannel.Reader.TryRead(out _).Should().BeTrue();
        _homeSearchChannel.Reader.TryRead(out _).Should().BeFalse();
    }
}
