using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.Api.Features.Leads.RequestDeletion;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Privacy;

namespace RealEstateStar.Api.Tests.Features.Leads.RequestDeletion;

public class RequestDeletionEndpointTests
{
    private readonly Mock<ILeadStore> _leadStore = new();
    private readonly Mock<ILeadDataDeletion> _deletion = new();
    private readonly Mock<IDeletionAuditLog> _auditLog = new();
    private readonly ILogger<RequestDeletionEndpoint> _logger = NullLogger<RequestDeletionEndpoint>.Instance;
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

    [Fact]
    public async Task Handle_Returns202AndSendsVerificationEmail_WhenEmailIsKnown()
    {
        var lead = MakeLead();
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _deletion.Setup(d => d.InitiateDeletionRequestAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync("del-token-abc123");

        var result = await RequestDeletionEndpoint.Handle(
            AgentId,
            new RequestDeletionRequest { Email = lead.Email },
            _leadStore.Object,
            _deletion.Object,
            _auditLog.Object,
            _logger,
            CancellationToken.None);

        result.Should().BeAssignableTo<Accepted>();

        _deletion.Verify(d => d.InitiateDeletionRequestAsync(
            AgentId, lead.Email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Returns202_WhenEmailIsUnknown()
    {
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, "nobody@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var result = await RequestDeletionEndpoint.Handle(
            AgentId,
            new RequestDeletionRequest { Email = "nobody@example.com" },
            _leadStore.Object,
            _deletion.Object,
            _auditLog.Object,
            _logger,
            CancellationToken.None);

        // Always 202 — never reveal whether email exists
        result.Should().BeAssignableTo<Accepted>();

        // Should NOT call InitiateDeletionRequestAsync for unknown emails
        _deletion.Verify(d => d.InitiateDeletionRequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RecordsDeletionInitiation_InAuditLog()
    {
        var lead = MakeLead();
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _deletion.Setup(d => d.InitiateDeletionRequestAsync(AgentId, lead.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync("del-token-abc123");

        await RequestDeletionEndpoint.Handle(
            AgentId,
            new RequestDeletionRequest { Email = lead.Email },
            _leadStore.Object,
            _deletion.Object,
            _auditLog.Object,
            _logger,
            CancellationToken.None);

        _auditLog.Verify(a => a.RecordInitiationAsync(
            AgentId,
            lead.Id,
            lead.Email,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
