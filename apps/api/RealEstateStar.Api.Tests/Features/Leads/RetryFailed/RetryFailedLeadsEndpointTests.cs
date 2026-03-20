using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.RetryFailed;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Features.Leads.Services.Enrichment;

namespace RealEstateStar.Api.Tests.Features.Leads.RetryFailed;

public class RetryFailedLeadsEndpointTests
{
    private static RealEstateStar.Api.Features.Leads.Lead MakeLead(string agentId, LeadStatus status) => new()
    {
        Id = Guid.NewGuid(),
        AgentId = agentId,
        LeadTypes = ["buying"],
        FirstName = "Jane",
        LastName = "Smith",
        Email = "jane@example.com",
        Phone = "555-0000",
        Timeline = "3-6 months",
        Status = status
    };

    private static LeadEnrichment MakeEnrichment() => new()
    {
        MotivationCategory = "relocation",
        MotivationAnalysis = "job move",
        ProfessionalBackground = "engineer",
        FinancialIndicators = "stable",
        TimelinePressure = "moderate",
        ConversationStarters = [],
        ColdCallOpeners = []
    };

    private static LeadScore MakeScore() => new()
    {
        OverallScore = 75,
        Factors = [],
        Explanation = "good lead"
    };

    [Fact]
    public async Task Handle_ReEnrichesLeads_WithEnrichmentFailedStatus()
    {
        var lead = MakeLead("agent1", LeadStatus.EnrichmentFailed);
        var enrichment = MakeEnrichment();
        var score = MakeScore();

        var store = new Mock<ILeadStore>();
        store.Setup(s => s.ListByStatusAsync("agent1", LeadStatus.EnrichmentFailed, It.IsAny<CancellationToken>()))
             .ReturnsAsync([lead]);
        store.Setup(s => s.ListByStatusAsync("agent1", LeadStatus.NotificationFailed, It.IsAny<CancellationToken>()))
             .ReturnsAsync([]);

        var enricher = new Mock<ILeadEnricher>();
        enricher.Setup(e => e.EnrichAsync(lead, It.IsAny<CancellationToken>()))
                .ReturnsAsync((enrichment, score));

        var notifier = new Mock<ILeadNotifier>();

        var result = await RetryFailedLeadsEndpoint.Handle(
            "agent1", store.Object, enricher.Object, notifier.Object,
            NullLogger<RetryFailedLeadsEndpoint>.Instance, CancellationToken.None);

        var ok = result.Should().BeAssignableTo<Ok<RetryFailedLeadsResponse>>().Subject;
        ok.Value!.Retried.Should().Be(1);
        ok.Value.StillFailing.Should().Be(0);

        enricher.Verify(e => e.EnrichAsync(lead, It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.UpdateEnrichmentAsync("agent1", lead.Id, enrichment, score, It.IsAny<CancellationToken>()), Times.Once);
        notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ReNotifiesLeads_WithNotificationFailedStatus()
    {
        var lead = MakeLead("agent1", LeadStatus.NotificationFailed);
        lead.Enrichment = MakeEnrichment();
        lead.Score = MakeScore();

        var store = new Mock<ILeadStore>();
        store.Setup(s => s.ListByStatusAsync("agent1", LeadStatus.EnrichmentFailed, It.IsAny<CancellationToken>()))
             .ReturnsAsync([]);
        store.Setup(s => s.ListByStatusAsync("agent1", LeadStatus.NotificationFailed, It.IsAny<CancellationToken>()))
             .ReturnsAsync([lead]);

        var enricher = new Mock<ILeadEnricher>();
        var notifier = new Mock<ILeadNotifier>();
        notifier.Setup(n => n.NotifyAgentAsync(
            "agent1", lead, It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await RetryFailedLeadsEndpoint.Handle(
            "agent1", store.Object, enricher.Object, notifier.Object,
            NullLogger<RetryFailedLeadsEndpoint>.Instance, CancellationToken.None);

        var ok = result.Should().BeAssignableTo<Ok<RetryFailedLeadsResponse>>().Subject;
        ok.Value!.Retried.Should().Be(1);
        ok.Value.StillFailing.Should().Be(0);

        notifier.Verify(n => n.NotifyAgentAsync(
            "agent1", lead, It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(),
            It.IsAny<CancellationToken>()), Times.Once);
        enricher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_SkipsLeads_InOtherStatuses()
    {
        // ListByStatusAsync only returns EnrichmentFailed and NotificationFailed leads,
        // so other-status leads are never in the candidates list — verify nothing is called
        var store = new Mock<ILeadStore>();
        store.Setup(s => s.ListByStatusAsync("agent1", LeadStatus.EnrichmentFailed, It.IsAny<CancellationToken>()))
             .ReturnsAsync([]);
        store.Setup(s => s.ListByStatusAsync("agent1", LeadStatus.NotificationFailed, It.IsAny<CancellationToken>()))
             .ReturnsAsync([]);

        var enricher = new Mock<ILeadEnricher>();
        var notifier = new Mock<ILeadNotifier>();

        var result = await RetryFailedLeadsEndpoint.Handle(
            "agent1", store.Object, enricher.Object, notifier.Object,
            NullLogger<RetryFailedLeadsEndpoint>.Instance, CancellationToken.None);

        var ok = result.Should().BeAssignableTo<Ok<RetryFailedLeadsResponse>>().Subject;
        ok.Value!.Retried.Should().Be(0);
        ok.Value.StillFailing.Should().Be(0);

        enricher.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ContinuesProcessing_WhenOneLeadFails()
    {
        var failingLead = MakeLead("agent1", LeadStatus.EnrichmentFailed);
        var succeedingLead = MakeLead("agent1", LeadStatus.EnrichmentFailed);
        var enrichment = MakeEnrichment();
        var score = MakeScore();

        var store = new Mock<ILeadStore>();
        store.Setup(s => s.ListByStatusAsync("agent1", LeadStatus.EnrichmentFailed, It.IsAny<CancellationToken>()))
             .ReturnsAsync([failingLead, succeedingLead]);
        store.Setup(s => s.ListByStatusAsync("agent1", LeadStatus.NotificationFailed, It.IsAny<CancellationToken>()))
             .ReturnsAsync([]);

        var enricher = new Mock<ILeadEnricher>();
        enricher.Setup(e => e.EnrichAsync(failingLead, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("enrichment service unavailable"));
        enricher.Setup(e => e.EnrichAsync(succeedingLead, It.IsAny<CancellationToken>()))
                .ReturnsAsync((enrichment, score));

        var notifier = new Mock<ILeadNotifier>();

        var result = await RetryFailedLeadsEndpoint.Handle(
            "agent1", store.Object, enricher.Object, notifier.Object,
            NullLogger<RetryFailedLeadsEndpoint>.Instance, CancellationToken.None);

        var ok = result.Should().BeAssignableTo<Ok<RetryFailedLeadsResponse>>().Subject;
        ok.Value!.Retried.Should().Be(1);
        ok.Value.StillFailing.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReturnsRetriedAndStillFailing_Counts()
    {
        var enrichFail1 = MakeLead("agent1", LeadStatus.EnrichmentFailed);
        var enrichFail2 = MakeLead("agent1", LeadStatus.EnrichmentFailed);
        var notifyFail1 = MakeLead("agent1", LeadStatus.NotificationFailed);
        var enrichment = MakeEnrichment();
        var score = MakeScore();

        var store = new Mock<ILeadStore>();
        store.Setup(s => s.ListByStatusAsync("agent1", LeadStatus.EnrichmentFailed, It.IsAny<CancellationToken>()))
             .ReturnsAsync([enrichFail1, enrichFail2]);
        store.Setup(s => s.ListByStatusAsync("agent1", LeadStatus.NotificationFailed, It.IsAny<CancellationToken>()))
             .ReturnsAsync([notifyFail1]);

        var enricher = new Mock<ILeadEnricher>();
        enricher.Setup(e => e.EnrichAsync(enrichFail1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((enrichment, score));
        enricher.Setup(e => e.EnrichAsync(enrichFail2, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("downstream error"));

        var notifier = new Mock<ILeadNotifier>();
        notifier.Setup(n => n.NotifyAgentAsync(
            "agent1", notifyFail1, It.IsAny<LeadEnrichment>(), It.IsAny<LeadScore>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await RetryFailedLeadsEndpoint.Handle(
            "agent1", store.Object, enricher.Object, notifier.Object,
            NullLogger<RetryFailedLeadsEndpoint>.Instance, CancellationToken.None);

        var ok = result.Should().BeAssignableTo<Ok<RetryFailedLeadsResponse>>().Subject;
        ok.Value!.Retried.Should().Be(2);     // enrichFail1 + notifyFail1
        ok.Value.StillFailing.Should().Be(1); // enrichFail2
    }
}
