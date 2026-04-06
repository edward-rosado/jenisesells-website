using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Services.LeadCommunicator.Tests;

public class LeadCommunicatorServiceTests
{
    private static readonly AgentNotificationConfig DefaultAgent = new()
    {
        AgentId = "jenise-buckalew",
        Handle = "jenise",
        Name = "Jenise Buckalew",
        FirstName = "Jenise",
        Email = "jenise@example.com",
        Phone = "555-123-4567",
        LicenseNumber = "NJ-123456",
        BrokerageName = "Coldwell Banker",
        PrimaryColor = "#1a73e8",
        AccentColor = "#f4b400",
        State = "NJ",
        Specialties = [],
        Testimonials = []
    };

    private static Lead MakeSellerLead() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "jenise-buckalew",
        LeadType = LeadType.Seller,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-000-1111",
        Timeline = "1-3months",
        Status = LeadStatus.Received,
        ReceivedAt = DateTime.UtcNow,
        SellerDetails = new SellerDetails
        {
            Address = "123 Oak Ave",
            City = "Springfield",
            State = "NJ",
            Zip = "07081"
        }
    };

    private static LeadPipelineContext MakeContext(Lead? lead = null) => new()
    {
        Lead = lead ?? MakeSellerLead(),
        AgentConfig = DefaultAgent,
        CorrelationId = Guid.NewGuid().ToString(),
        Score = new LeadScore { OverallScore = 75, Factors = [], Explanation = "Test" }
    };

    private static (LeadCommunicatorService service, Mock<ILeadEmailDrafter> drafterMock, Mock<IGmailSender> gmailMock)
        CreateService(
            LeadEmail? emailResult = null,
            Exception? gmailException = null,
            IIdempotencyStore? idempotencyStore = null)
    {
        var drafterMock = new Mock<ILeadEmailDrafter>();
        var email = emailResult ?? new LeadEmail("Test Subject", "<p>Hello</p>", null);

        drafterMock
            .Setup(d => d.DraftAsync(
                It.IsAny<Lead>(), It.IsAny<LeadScore>(),
                It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
                It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>(),
                It.IsAny<AgentContext?>()))
            .ReturnsAsync(email);

        var gmailMock = new Mock<IGmailSender>();

        if (gmailException is not null)
        {
            gmailMock
                .Setup(g => g.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(gmailException);
        }

        var logger = new Mock<ILogger<LeadCommunicatorService>>();
        var store = idempotencyStore ?? new NullIdempotencyStore();
        var service = new LeadCommunicatorService(drafterMock.Object, gmailMock.Object, store, logger.Object);
        return (service, drafterMock, gmailMock);
    }

    // -----------------------------------------------------------------------
    // DraftAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DraftAsync_CallsDrafterWithLeadAndAgentConfig()
    {
        var (service, drafterMock, _) = CreateService();
        var ctx = MakeContext();

        await service.DraftAsync(ctx, CancellationToken.None);

        drafterMock.Verify(d => d.DraftAsync(
            ctx.Lead, ctx.Score!,
            It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
            ctx.AgentConfig, It.IsAny<CancellationToken>(),
            It.IsAny<AgentContext?>()), Times.Once);
    }

    [Fact]
    public async Task DraftAsync_ReturnsCommunicationRecordWithChannelEmail()
    {
        var (service, _, _) = CreateService();
        var ctx = MakeContext();

        var record = await service.DraftAsync(ctx, CancellationToken.None);

        record.Channel.Should().Be("email");
    }

    [Fact]
    public async Task DraftAsync_ReturnsCommunicationRecordWithSubjectAndHtmlBody()
    {
        var (service, _, _) = CreateService(new LeadEmail("My Subject", "<p>Body</p>", null));
        var ctx = MakeContext();

        var record = await service.DraftAsync(ctx, CancellationToken.None);

        record.Subject.Should().Be("My Subject");
        record.HtmlBody.Should().Be("<p>Body</p>");
    }

    [Fact]
    public async Task DraftAsync_ReturnsCommunicationRecordWithSentFalse()
    {
        var (service, _, _) = CreateService();
        var ctx = MakeContext();

        var record = await service.DraftAsync(ctx, CancellationToken.None);

        record.Sent.Should().BeFalse();
        record.SentAt.Should().BeNull();
    }

    [Fact]
    public async Task DraftAsync_ReturnsCommunicationRecordWithNonEmptyContentHash()
    {
        var (service, _, _) = CreateService();
        var ctx = MakeContext();

        var record = await service.DraftAsync(ctx, CancellationToken.None);

        record.ContentHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DraftAsync_ReturnsCommunicationRecordWithDraftedAtSet()
    {
        var (service, _, _) = CreateService();
        var ctx = MakeContext();

        var before = DateTimeOffset.UtcNow;
        var record = await service.DraftAsync(ctx, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        record.DraftedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task DraftAsync_NullScore_UsesFallbackScore()
    {
        var (service, drafterMock, _) = CreateService();
        var lead = MakeSellerLead();
        var ctx = new LeadPipelineContext
        {
            Lead = lead,
            AgentConfig = DefaultAgent,
            CorrelationId = Guid.NewGuid().ToString(),
            Score = null  // no score yet
        };

        await service.DraftAsync(ctx, CancellationToken.None);

        // Should not throw — service substitutes a zero-score fallback
        drafterMock.Verify(d => d.DraftAsync(
            It.IsAny<Lead>(), It.Is<LeadScore>(s => s.OverallScore == 0),
            It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>(),
            It.IsAny<AgentContext?>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // SendAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_CallsGmailSenderWithLeadEmailAndAgentId()
    {
        var (service, _, gmailMock) = CreateService();
        var ctx = MakeContext();
        var draft = new CommunicationRecord
        {
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            Channel = "email",
            DraftedAt = DateTimeOffset.UtcNow,
            ContentHash = "abc123"
        };

        await service.SendAsync(draft, ctx, CancellationToken.None);

        gmailMock.Verify(g => g.SendAsync(
            DefaultAgent.AgentId,
            DefaultAgent.AgentId,
            ctx.Lead.Email,
            draft.Subject,
            draft.HtmlBody,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_OnSuccess_SetsSentTrueAndSentAt()
    {
        var (service, _, _) = CreateService();
        var ctx = MakeContext();
        var draft = new CommunicationRecord
        {
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            Channel = "email",
            DraftedAt = DateTimeOffset.UtcNow,
            ContentHash = "abc123"
        };

        var before = DateTimeOffset.UtcNow;
        var result = await service.SendAsync(draft, ctx, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        result.Sent.Should().BeTrue();
        result.SentAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task SendAsync_WhenGmailFails_SentRemainsFlaseAndErrorIsSet()
    {
        var (service, _, _) = CreateService(gmailException: new HttpRequestException("Gmail down"));
        var ctx = MakeContext();
        var draft = new CommunicationRecord
        {
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            Channel = "email",
            DraftedAt = DateTimeOffset.UtcNow,
            ContentHash = "abc123"
        };

        var result = await service.SendAsync(draft, ctx, CancellationToken.None);

        result.Sent.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SendAsync_WhenGmailFails_DoesNotThrow()
    {
        var (service, _, _) = CreateService(gmailException: new HttpRequestException("Gmail down"));
        var ctx = MakeContext();
        var draft = new CommunicationRecord
        {
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            Channel = "email",
            DraftedAt = DateTimeOffset.UtcNow,
            ContentHash = "abc123"
        };

        var act = async () => await service.SendAsync(draft, ctx, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // -----------------------------------------------------------------------
    // ContentHash (shared Domain utility)
    // -----------------------------------------------------------------------

    [Fact]
    public void ContentHash_SameInputProducesSameHash()
    {
        var hash1 = ContentHash.Compute("Subject", "<p>Body</p>");
        var hash2 = ContentHash.Compute("Subject", "<p>Body</p>");
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ContentHash_DifferentSubjectProducesDifferentHash()
    {
        var hash1 = ContentHash.Compute("Subject A", "<p>Body</p>");
        var hash2 = ContentHash.Compute("Subject B", "<p>Body</p>");
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ContentHash_DifferentBodyProducesDifferentHash()
    {
        var hash1 = ContentHash.Compute("Subject", "<p>Body A</p>");
        var hash2 = ContentHash.Compute("Subject", "<p>Body B</p>");
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ContentHash_ReturnsLowercaseHexString()
    {
        var hash = ContentHash.Compute("Subject", "<p>Body</p>");
        hash.Should().MatchRegex("^[0-9a-f]+$");
    }

    // -----------------------------------------------------------------------
    // Idempotency guard
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WhenAlreadyCompleted_SkipsSendAndReturnsSentTrue()
    {
        var store = new InMemoryIdempotencyStore();
        var (service, _, gmailMock) = CreateService(idempotencyStore: store);
        var ctx = MakeContext();
        var draft = new CommunicationRecord
        {
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            Channel = "email",
            DraftedAt = DateTimeOffset.UtcNow,
            ContentHash = "abc123"
        };

        // Pre-mark the key as completed
        var key = $"lead:{DefaultAgent.AgentId}-{ctx.Lead.Id}:email-send";
        await store.MarkCompletedAsync(key, CancellationToken.None);

        var result = await service.SendAsync(draft, ctx, CancellationToken.None);

        result.Sent.Should().BeTrue();
        gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WhenNotCompleted_SendsProceedsAndMarksCompleted()
    {
        var store = new InMemoryIdempotencyStore();
        var (service, _, _) = CreateService(idempotencyStore: store);
        var ctx = MakeContext();
        var draft = new CommunicationRecord
        {
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            Channel = "email",
            DraftedAt = DateTimeOffset.UtcNow,
            ContentHash = "abc123"
        };

        var result = await service.SendAsync(draft, ctx, CancellationToken.None);

        result.Sent.Should().BeTrue();
        var key = $"lead:{DefaultAgent.AgentId}-{ctx.Lead.Id}:email-send";
        var completed = await store.HasCompletedAsync(key, CancellationToken.None);
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_WhenSendFails_MarkCompletedIsNotCalled()
    {
        var store = new InMemoryIdempotencyStore();
        var (service, _, _) = CreateService(
            gmailException: new HttpRequestException("Gmail down"),
            idempotencyStore: store);
        var ctx = MakeContext();
        var draft = new CommunicationRecord
        {
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            Channel = "email",
            DraftedAt = DateTimeOffset.UtcNow,
            ContentHash = "abc123"
        };

        await service.SendAsync(draft, ctx, CancellationToken.None);

        var key = $"lead:{DefaultAgent.AgentId}-{ctx.Lead.Id}:email-send";
        var completed = await store.HasCompletedAsync(key, CancellationToken.None);
        completed.Should().BeFalse();
    }
}
