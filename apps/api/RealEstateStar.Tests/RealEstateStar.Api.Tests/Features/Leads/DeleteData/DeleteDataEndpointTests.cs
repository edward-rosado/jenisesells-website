using Xunit;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Leads.DeleteData;

namespace RealEstateStar.Api.Tests.Features.Leads.DeleteData;

public class DeleteDataEndpointTests
{
    private readonly Mock<ILeadDataService> _leadStore = new();
    private readonly Mock<ILeadDeletionDataService> _deletion = new();
    private readonly ILogger<DeleteDataEndpoint> _logger = NullLogger<DeleteDataEndpoint>.Instance;
    private const string AgentId = "jenise-buckalew";

    private static Lead MakeLead(string email = "lead@example.com") => new()
    {
        Id = Guid.NewGuid(),
        AgentId = AgentId,
        LeadType = LeadType.Buyer,
        FirstName = "Jane",
        LastName = "Doe",
        Email = email,
        Phone = "555-1234",
        Timeline = "ASAP",
        // TODO: Pipeline redesign — LeadStatus.Enriched removed in Phase 1.5; using Received
        Status = LeadStatus.Received
    };

    private static DeleteLeadDataRequest MakeRequest(
        string email = "lead@example.com",
        string token = "valid-token",
        string reason = "gdpr_erasure") => new()
        {
            Email = email,
            Token = token,
            Reason = reason
        };

    [Fact]
    public async Task Handle_Returns200WithDeletedItems_WhenTokenIsValid()
    {
        var lead = MakeLead();
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var deletedItems = new List<string> { "lead_record", "consent_log_redacted" };
        _deletion.Setup(d => d.ExecuteDeletionAsync(AgentId, lead.Email, "valid-token", "gdpr_erasure", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult(true, deletedItems));

        var result = await DeleteDataEndpoint.Handle(
            AgentId, MakeRequest(lead.Email), _leadStore.Object, _deletion.Object, _logger, CancellationToken.None);

        var ok = result.Should().BeAssignableTo<Ok<DeleteLeadDataResponse>>().Subject;
        ok.Value!.DeletedItems.Should().BeEquivalentTo(deletedItems);
    }

    [Fact]
    public async Task Handle_Returns400_WhenReasonIsInvalid()
    {
        var result = await DeleteDataEndpoint.Handle(
            AgentId, MakeRequest(reason: "not_a_valid_reason"), _leadStore.Object, _deletion.Object, _logger, CancellationToken.None);

        // Results.ValidationProblem returns a ProblemHttpResult with HttpValidationProblemDetails
        var problem = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_Returns401_WhenTokenIsExpired()
    {
        var lead = MakeLead();
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _deletion.Setup(d => d.ExecuteDeletionAsync(AgentId, lead.Email, "expired-token", "gdpr_erasure", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult(false, [], "token expired"));

        var result = await DeleteDataEndpoint.Handle(
            AgentId, MakeRequest(lead.Email, "expired-token"), _leadStore.Object, _deletion.Object, _logger, CancellationToken.None);

        result.Should().BeAssignableTo<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_Returns401_WhenEmailMismatchesToken()
    {
        var lead = MakeLead("other@example.com");
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, "other@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _deletion.Setup(d => d.ExecuteDeletionAsync(AgentId, "other@example.com", "valid-token", "gdpr_erasure", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult(false, [], "token does not match email"));

        var result = await DeleteDataEndpoint.Handle(
            AgentId, MakeRequest("other@example.com"), _leadStore.Object, _deletion.Object, _logger, CancellationToken.None);

        result.Should().BeAssignableTo<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_Returns404_WhenLeadNotFound()
    {
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, "missing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var result = await DeleteDataEndpoint.Handle(
            AgentId, MakeRequest("missing@example.com"), _leadStore.Object, _deletion.Object, _logger, CancellationToken.None);

        result.Should().BeAssignableTo<NotFound>();
    }

    [Fact]
    public async Task Handle_Returns409_WhenAlreadyDeleted()
    {
        var lead = MakeLead();
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _deletion.Setup(d => d.ExecuteDeletionAsync(AgentId, lead.Email, "valid-token", "gdpr_erasure", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult(false, [], "already processed"));

        var result = await DeleteDataEndpoint.Handle(
            AgentId, MakeRequest(lead.Email), _leadStore.Object, _deletion.Object, _logger, CancellationToken.None);

        // Results.Conflict returns Conflict<T> where T is the anonymous body type
        result.GetType().Name.Should().StartWith("Conflict");
    }

    [Fact]
    public async Task Handle_DeletesAllArtifacts_WhenSuccessful()
    {
        var lead = MakeLead();
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var allArtifacts = new List<string>
        {
            "lead_record",
            "research_insights",
            "consent_log_redacted",
            "cma_artifacts"
        };

        _deletion.Setup(d => d.ExecuteDeletionAsync(AgentId, lead.Email, "valid-token", "user_request", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult(true, allArtifacts));

        var result = await DeleteDataEndpoint.Handle(
            AgentId, MakeRequest(lead.Email, reason: "user_request"), _leadStore.Object, _deletion.Object, _logger, CancellationToken.None);

        var ok = result.Should().BeAssignableTo<Ok<DeleteLeadDataResponse>>().Subject;
        ok.Value!.DeletedItems.Should().BeEquivalentTo(allArtifacts);

        _deletion.Verify(d => d.ExecuteDeletionAsync(
            AgentId, lead.Email, "valid-token", "user_request", It.IsAny<CancellationToken>()), Times.Once);
    }
}
