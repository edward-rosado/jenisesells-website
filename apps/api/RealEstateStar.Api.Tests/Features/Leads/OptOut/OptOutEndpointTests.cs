using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.OptOut;
using RealEstateStar.Api.Features.Leads.Services;

namespace RealEstateStar.Api.Tests.Features.Leads.OptOut;

public class OptOutEndpointTests
{
    private const string AgentId = "jenise-buckalew";

    private static Lead MakeLead(
        string email = "jane@example.com",
        string consentToken = "valid-token-abc123",
        bool? marketingOptedIn = true) => new()
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

    [Fact]
    public async Task Handle_ValidToken_Returns200AndUpdatesOptInAndAppendConsentLog()
    {
        var lead = MakeLead();
        var leadStore = new Mock<ILeadStore>();
        var consentLog = new Mock<IMarketingConsentLog>();

        leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        leadStore.Setup(s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        consentLog.Setup(c => c.RecordConsentAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        var result = await OptOutEndpoint.Handle(
            AgentId,
            new OptOutRequest { Email = lead.Email, Token = lead.ConsentToken! },
            leadStore.Object,
            consentLog.Object,
            httpContext,
            CancellationToken.None);

        var okResult = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        leadStore.Verify(s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, false, It.IsAny<CancellationToken>()), Times.Once);
        consentLog.Verify(c => c.RecordConsentAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidToken_Returns200WithoutWriting()
    {
        var lead = MakeLead();
        var leadStore = new Mock<ILeadStore>();
        var consentLog = new Mock<IMarketingConsentLog>();

        leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var httpContext = new DefaultHttpContext();
        var result = await OptOutEndpoint.Handle(
            AgentId,
            new OptOutRequest { Email = lead.Email, Token = "wrong-token" },
            leadStore.Object,
            consentLog.Object,
            httpContext,
            CancellationToken.None);

        var okResult = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        leadStore.Verify(s => s.UpdateMarketingOptInAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        consentLog.Verify(c => c.RecordConsentAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmailNotFound_Returns200WithoutWriting()
    {
        var leadStore = new Mock<ILeadStore>();
        var consentLog = new Mock<IMarketingConsentLog>();

        leadStore.Setup(s => s.GetByEmailAsync(AgentId, "unknown@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var httpContext = new DefaultHttpContext();
        var result = await OptOutEndpoint.Handle(
            AgentId,
            new OptOutRequest { Email = "unknown@example.com", Token = "any-token" },
            leadStore.Object,
            consentLog.Object,
            httpContext,
            CancellationToken.None);

        var okResult = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        leadStore.Verify(s => s.UpdateMarketingOptInAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        consentLog.Verify(c => c.RecordConsentAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyOptedOut_Returns200Idempotently()
    {
        var lead = MakeLead(marketingOptedIn: false);
        var leadStore = new Mock<ILeadStore>();
        var consentLog = new Mock<IMarketingConsentLog>();

        leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        leadStore.Setup(s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        consentLog.Setup(c => c.RecordConsentAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        var result = await OptOutEndpoint.Handle(
            AgentId,
            new OptOutRequest { Email = lead.Email, Token = lead.ConsentToken! },
            leadStore.Object,
            consentLog.Object,
            httpContext,
            CancellationToken.None);

        var okResult = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Handle_ValidToken_ConsentLogRecordsOptOutAction()
    {
        var lead = MakeLead();
        var leadStore = new Mock<ILeadStore>();
        var consentLog = new Mock<IMarketingConsentLog>();

        leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        leadStore.Setup(s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        MarketingConsent? captured = null;
        consentLog.Setup(c => c.RecordConsentAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()))
            .Callback<string, MarketingConsent, CancellationToken>((_, consent, _) => captured = consent)
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        await OptOutEndpoint.Handle(
            AgentId,
            new OptOutRequest { Email = lead.Email, Token = lead.ConsentToken! },
            leadStore.Object,
            consentLog.Object,
            httpContext,
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Action.Should().Be("opt-out");
        captured.OptedIn.Should().BeFalse();
        captured.Source.Should().Be("email-unsubscribe");
    }
}
