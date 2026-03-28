using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.Domain.Privacy;
using RealEstateStar.Api.Features.Leads.Subscribe;

namespace RealEstateStar.Api.Tests.Features.Leads.Subscribe;

public class SubscribeEndpointTests
{
    private const string AgentId = "jenise-buckalew";

    private static Lead MakeLead(
        string email = "jane@example.com",
        string consentToken = "valid-token-abc123",
        bool? marketingOptedIn = false) => new()
        {
            Id = Guid.NewGuid(),
            AgentId = AgentId,
            LeadType = LeadType.Buyer,
            FirstName = "Jane",
            LastName = "Doe",
            Email = email,
            Phone = "5551234",
            Timeline = "1-3months",
            ConsentToken = consentToken,
            MarketingOptedIn = marketingOptedIn,
        };

    private static (Mock<IConsentAuditService> audit, Mock<IComplianceConsentWriter> writer, IOptions<ConsentHmacOptions> opts) MakeTripleWriteMocks()
    {
        var audit = new Mock<IConsentAuditService>();
        audit.Setup(s => s.RecordAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var writer = new Mock<IComplianceConsentWriter>();
        writer.Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var opts = Options.Create(new ConsentHmacOptions { Secret = "test-hmac-secret-32-bytes-xxxxx!" });

        return (audit, writer, opts);
    }

    [Fact]
    public async Task Handle_ValidToken_Returns200AndUpdatesOptInAndAppendConsentLog()
    {
        var lead = MakeLead();
        var leadStore = new Mock<ILeadStore>();
        var consentLog = new Mock<IMarketingConsentLog>();
        var (consentAudit, complianceWriter, consentHmacOptions) = MakeTripleWriteMocks();

        leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        leadStore.Setup(s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        consentLog.Setup(c => c.RecordConsentAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        var result = await SubscribeEndpoint.Handle(
            AgentId,
            new SubscribeRequest { Email = lead.Email, Token = lead.ConsentToken! },
            leadStore.Object,
            consentLog.Object,
            httpContext,
            consentAudit.Object,
            complianceWriter.Object,
            consentHmacOptions,
            CancellationToken.None);

        var okResult = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        leadStore.Verify(s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, true, It.IsAny<CancellationToken>()), Times.Once);
        consentLog.Verify(c => c.RecordConsentAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidToken_TripleWritesConsent()
    {
        var lead = MakeLead();
        var leadStore = new Mock<ILeadStore>();
        var consentLog = new Mock<IMarketingConsentLog>();
        var (consentAudit, complianceWriter, consentHmacOptions) = MakeTripleWriteMocks();

        leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        leadStore.Setup(s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        consentLog.Setup(c => c.RecordConsentAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        await SubscribeEndpoint.Handle(
            AgentId,
            new SubscribeRequest { Email = lead.Email, Token = lead.ConsentToken! },
            leadStore.Object,
            consentLog.Object,
            httpContext,
            consentAudit.Object,
            complianceWriter.Object,
            consentHmacOptions,
            CancellationToken.None);

        complianceWriter.Verify(w => w.WriteAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        consentAudit.Verify(a => a.RecordAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidToken_Returns200WithoutWriting()
    {
        var lead = MakeLead();
        var leadStore = new Mock<ILeadStore>();
        var consentLog = new Mock<IMarketingConsentLog>();
        var (consentAudit, complianceWriter, consentHmacOptions) = MakeTripleWriteMocks();

        leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var httpContext = new DefaultHttpContext();
        var result = await SubscribeEndpoint.Handle(
            AgentId,
            new SubscribeRequest { Email = lead.Email, Token = "wrong-token" },
            leadStore.Object,
            consentLog.Object,
            httpContext,
            consentAudit.Object,
            complianceWriter.Object,
            consentHmacOptions,
            CancellationToken.None);

        var okResult = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        leadStore.Verify(s => s.UpdateMarketingOptInAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        consentLog.Verify(c => c.RecordConsentAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()), Times.Never);
        complianceWriter.Verify(w => w.WriteAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        consentAudit.Verify(a => a.RecordAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidToken_ConsentLogRecordsOptInActionWithReSubscribeSource()
    {
        var lead = MakeLead();
        var leadStore = new Mock<ILeadStore>();
        var consentLog = new Mock<IMarketingConsentLog>();
        var (consentAudit, complianceWriter, consentHmacOptions) = MakeTripleWriteMocks();

        leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        leadStore.Setup(s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        MarketingConsent? captured = null;
        consentLog.Setup(c => c.RecordConsentAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()))
            .Callback<string, MarketingConsent, CancellationToken>((_, consent, _) => captured = consent)
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        await SubscribeEndpoint.Handle(
            AgentId,
            new SubscribeRequest { Email = lead.Email, Token = lead.ConsentToken! },
            leadStore.Object,
            consentLog.Object,
            httpContext,
            consentAudit.Object,
            complianceWriter.Object,
            consentHmacOptions,
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Action.Should().Be(ConsentAction.Resubscribe);
        captured.Source.Should().Be(ConsentSource.EmailLink);
        captured.OptedIn.Should().BeTrue();
    }
}
