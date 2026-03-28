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
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Leads.RequestDeletion;

namespace RealEstateStar.Api.Tests.Features.Leads.RequestDeletion;

public class RequestDeletionEndpointTests
{
    private readonly Mock<ILeadDataService> _leadStore = new();
    private readonly Mock<ILeadDeletionDataService> _deletion = new();
    private readonly Mock<IDeletionAuditDataService> _auditLog = new();
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
