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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Leads.Export;
using RealEstateStar.Domain.Privacy;

namespace RealEstateStar.Api.Tests.Features.Leads.Export;

public class ExportLeadEndpointTests
{
    private readonly Mock<ILeadExportDataService> _dataExport = new();
    private readonly ILogger<ExportLeadEndpoint> _logger = NullLogger<ExportLeadEndpoint>.Instance;
    private const string AgentId = "jenise-buckalew";
    private const string Email = "lead@example.com";

    [Fact]
    public async Task Handle_Returns200WithExportData_WhenLeadExists()
    {
        // TODO: Pipeline redesign — LeadEnrichment removed in Phase 1.5; status updated in Phase 2/3/4
        var exportData = new LeadExportData(
            new Lead
            {
                Id = Guid.NewGuid(),
                AgentId = AgentId,
                LeadType = LeadType.Buyer,
                FirstName = "Jane",
                LastName = "Doe",
                Email = Email,
                Phone = "555-1234",
                Timeline = "ASAP",
                Status = LeadStatus.Received
            },
            []);

        _dataExport.Setup(e => e.GatherAsync(AgentId, Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exportData);

        var result = await ExportLeadEndpoint.Handle(
            AgentId, Email, _dataExport.Object, _logger, CancellationToken.None);

        var ok = result.Should().BeAssignableTo<Ok<LeadExportData>>().Subject;
        ok.Value.Should().Be(exportData);
    }

    [Fact]
    public async Task Handle_Returns404_WhenLeadNotFound()
    {
        _dataExport.Setup(e => e.GatherAsync(AgentId, Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadExportData?)null);

        var result = await ExportLeadEndpoint.Handle(
            AgentId, Email, _dataExport.Object, _logger, CancellationToken.None);

        result.Should().BeAssignableTo<NotFound>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_Returns400_WhenEmailIsMissing(string? email)
    {
        var result = await ExportLeadEndpoint.Handle(
            AgentId, email!, _dataExport.Object, _logger, CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
