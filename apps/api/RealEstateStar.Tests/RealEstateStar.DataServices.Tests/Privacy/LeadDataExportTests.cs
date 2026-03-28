using Moq;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.DataServices.Privacy;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.DataServices.Tests.Privacy;

public class LeadDataExportTests
{
    private readonly Mock<ILeadStore> _leadStore = new();
    private readonly Mock<ILogger<LeadDataExport>> _logger = new();
    private readonly LeadDataExport _sut;

    private const string AgentId = "jenise-buckalew";
    private const string LeadEmail = "jane.doe@example.com";

    public LeadDataExportTests()
    {
        _sut = new LeadDataExport(_leadStore.Object, _logger.Object);

        _leadStore.Setup(s => s.GetByEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);
    }

    private static Lead MakeLead(string email = LeadEmail) => new()
    {
        Id = Guid.NewGuid(),
        AgentId = AgentId,
        LeadType = LeadType.Buyer,
        FirstName = "Jane",
        LastName = "Doe",
        Email = email,
        Phone = "5551234567",
        Timeline = "1-3months",
        Status = LeadStatus.Received,
        ReceivedAt = new DateTime(2026, 3, 19, 14, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task GatherAsync_WhenLeadExists_ReturnsExportDataWithProfile()
    {
        var lead = MakeLead();
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var result = await _sut.GatherAsync(AgentId, LeadEmail, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(lead, result.Profile);
    }

    [Fact]
    public async Task GatherAsync_WhenLeadExists_ReturnsEmptyConsentHistory()
    {
        var lead = MakeLead();
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var result = await _sut.GatherAsync(AgentId, LeadEmail, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result.ConsentHistory);
    }

    // TODO: Pipeline redesign — GatherAsync_WhenLeadExists_ReturnsNullEnrichment removed in Phase 1.5
    // LeadExportData.Enrichment property removed; test will be rewritten in Phase 2/3/4

    [Fact]
    public async Task GatherAsync_WhenLeadNotFound_ReturnsNull()
    {
        var result = await _sut.GatherAsync(AgentId, "unknown@example.com", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GatherAsync_WhenLeadNotFound_LogsInformation()
    {
        await _sut.GatherAsync(AgentId, "unknown@example.com", CancellationToken.None);

        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[EXPORT-001]")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GatherAsync_WhenLeadNotFound_DoesNotLogWhenLeadExists()
    {
        var lead = MakeLead();
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        await _sut.GatherAsync(AgentId, LeadEmail, CancellationToken.None);

        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[EXPORT-001]")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task GatherAsync_PassesAgentIdAndEmailToLeadStore()
    {
        await _sut.GatherAsync(AgentId, LeadEmail, CancellationToken.None);

        _leadStore.Verify(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()), Times.Once);
    }
}
