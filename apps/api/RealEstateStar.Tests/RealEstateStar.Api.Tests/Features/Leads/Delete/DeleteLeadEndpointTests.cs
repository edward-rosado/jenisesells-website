using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Leads.Delete;

namespace RealEstateStar.Api.Tests.Features.Leads.Delete;

public class DeleteLeadEndpointTests
{
    private readonly Mock<ILeadDataDeletion> _deletion = new();
    private readonly ILogger<DeleteLeadEndpoint> _logger = NullLogger<DeleteLeadEndpoint>.Instance;
    private const string AgentId = "jenise-buckalew";
    private const string Email = "lead@example.com";

    [Fact]
    public async Task Handle_Returns204_WhenDeletionSucceeds()
    {
        _deletion.Setup(d => d.ExecuteDeletionAsync(AgentId, Email, "", "gdpr_erasure", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult(true, ["lead_record", "consent_log_redacted"]));

        var result = await DeleteLeadEndpoint.Handle(
            AgentId, Email, _deletion.Object, _logger, CancellationToken.None);

        result.Should().BeAssignableTo<NoContent>();
    }

    [Fact]
    public async Task Handle_Returns404_WhenLeadNotFound()
    {
        _deletion.Setup(d => d.ExecuteDeletionAsync(AgentId, Email, "", "gdpr_erasure", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult(false, [], "not found"));

        var result = await DeleteLeadEndpoint.Handle(
            AgentId, Email, _deletion.Object, _logger, CancellationToken.None);

        result.Should().BeAssignableTo<NotFound>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_Returns400_WhenEmailIsMissing(string? email)
    {
        var result = await DeleteLeadEndpoint.Handle(
            AgentId, email!, _deletion.Object, _logger, CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
