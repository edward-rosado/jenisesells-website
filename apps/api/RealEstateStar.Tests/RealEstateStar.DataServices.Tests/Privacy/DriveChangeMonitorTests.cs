using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Markdown;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.DataServices.Config;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.DataServices.WhatsApp;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.DataServices.Tests.Privacy;

public class DriveChangeMonitorTests
{
    private readonly Mock<IGwsService> _gws = new(MockBehavior.Strict);
    private readonly Mock<ILeadStore> _leadStore = new(MockBehavior.Strict);
    private readonly Mock<ILogger<DriveChangeMonitor>> _logger = new();
    private readonly DriveChangeMonitor _sut;

    private const string AgentId = "jenise-buckalew";
    private const string AgentEmail = "jenise@example.com";
    private static readonly DateTime Since = new(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc);

    public DriveChangeMonitorTests()
    {
        _sut = new DriveChangeMonitor(_gws.Object, _leadStore.Object, _logger.Object);
    }

    // ── Test data helpers ──────────────────────────────────────────────────────

    private static Lead MakeLead(string firstName = "Jane", string lastName = "Doe") => new()
    {
        Id = Guid.NewGuid(),
        AgentId = AgentId,
        LeadType = LeadType.Buyer,
        FirstName = firstName,
        LastName = lastName,
        Email = "jane@example.com",
        Phone = "5551234567",
        Timeline = "1-3months",
        Status = LeadStatus.Received,
        ReceivedAt = new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc),
    };

    private static string MakeMoveJson(string sourcePath, string destinationParent, string fileName = "Lead Profile.md") => $$"""
        [
          {
            "action": "Move",
            "fileName": "{{fileName}}",
            "folderPath": "{{sourcePath}}",
            "destinationParent": "{{destinationParent}}",
            "timestamp": "2026-03-19T12:00:00Z"
          }
        ]
        """;

    // ── Folder move → status mapping ───────────────────────────────────────────

    [Fact]
    public async Task PollAsync_UpdatesStatusToActiveClient_WhenFolderMovedTo2ActiveClients()
    {
        var lead = MakeLead();
        var json = MakeMoveJson(
            "Real Estate Star/1 - Leads/Jane Doe",
            "Real Estate Star/2 - Active Clients/Jane Doe");

        _gws.Setup(g => g.QueryDriveActivityAsync(AgentEmail, "Real Estate Star", Since, It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
        _leadStore.Setup(s => s.GetByNameAsync(AgentId, "Jane Doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        _leadStore.Setup(s => s.UpdateStatusAsync(lead, LeadStatus.ActiveClient, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.PollAsync(AgentId, AgentEmail, Since, CancellationToken.None);

        result.StatusUpdated.Should().Be(1);
        result.Errors.Should().Be(0);
        _leadStore.Verify(s => s.UpdateStatusAsync(lead, LeadStatus.ActiveClient, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollAsync_UpdatesStatusToUnderContract_WhenFolderMovedTo3UnderContract()
    {
        var lead = MakeLead();
        var json = MakeMoveJson(
            "Real Estate Star/2 - Active Clients/Jane Doe",
            "Real Estate Star/3 - Under Contract/Jane Doe");

        _gws.Setup(g => g.QueryDriveActivityAsync(AgentEmail, "Real Estate Star", Since, It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
        _leadStore.Setup(s => s.GetByNameAsync(AgentId, "Jane Doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        _leadStore.Setup(s => s.UpdateStatusAsync(lead, LeadStatus.UnderContract, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.PollAsync(AgentId, AgentEmail, Since, CancellationToken.None);

        result.StatusUpdated.Should().Be(1);
        _leadStore.Verify(s => s.UpdateStatusAsync(lead, LeadStatus.UnderContract, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollAsync_UpdatesStatusToClosed_WhenFolderMovedTo4Closed()
    {
        var lead = MakeLead();
        var json = MakeMoveJson(
            "Real Estate Star/3 - Under Contract/Jane Doe",
            "Real Estate Star/4 - Closed/Jane Doe");

        _gws.Setup(g => g.QueryDriveActivityAsync(AgentEmail, "Real Estate Star", Since, It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
        _leadStore.Setup(s => s.GetByNameAsync(AgentId, "Jane Doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        _leadStore.Setup(s => s.UpdateStatusAsync(lead, LeadStatus.Closed, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.PollAsync(AgentId, AgentEmail, Since, CancellationToken.None);

        result.StatusUpdated.Should().Be(1);
        _leadStore.Verify(s => s.UpdateStatusAsync(lead, LeadStatus.Closed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollAsync_UpdatesStatusToInactive_WhenFolderMovedTo5Inactive()
    {
        var lead = MakeLead();
        var json = MakeMoveJson(
            "Real Estate Star/2 - Active Clients/Jane Doe",
            "Real Estate Star/5 - Inactive/Jane Doe");

        _gws.Setup(g => g.QueryDriveActivityAsync(AgentEmail, "Real Estate Star", Since, It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
        _leadStore.Setup(s => s.GetByNameAsync(AgentId, "Jane Doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        _leadStore.Setup(s => s.UpdateStatusAsync(lead, LeadStatus.Inactive, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.PollAsync(AgentId, AgentEmail, Since, CancellationToken.None);

        result.StatusUpdated.Should().Be(1);
        _leadStore.Verify(s => s.UpdateStatusAsync(lead, LeadStatus.Inactive, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Delete event warning ───────────────────────────────────────────────────

    [Fact]
    public async Task PollAsync_LogsWarning_WhenFileDeleted()
    {
        var json = """
            [
              {
                "action": "Delete",
                "fileName": "Lead Profile.md",
                "folderPath": "Real Estate Star/1 - Leads/Jane Doe",
                "timestamp": "2026-03-19T12:00:00Z"
              }
            ]
            """;

        _gws.Setup(g => g.QueryDriveActivityAsync(AgentEmail, "Real Estate Star", Since, It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var result = await _sut.PollAsync(AgentId, AgentEmail, Since, CancellationToken.None);

        result.Processed.Should().Be(1);
        result.StatusUpdated.Should().Be(0);
        result.Errors.Should().Be(0);

        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[LEAD-041]")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ── Per-agent isolation ────────────────────────────────────────────────────

    [Fact]
    public async Task PollAsync_ReturnsErrorResult_WhenGwsQueryFails()
    {
        _gws.Setup(g => g.QueryDriveActivityAsync(AgentEmail, "Real Estate Star", Since, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Drive API unavailable"));

        var result = await _sut.PollAsync(AgentId, AgentEmail, Since, CancellationToken.None);

        result.Processed.Should().Be(0);
        result.Errors.Should().Be(1);
        result.ErrorDetails.Should().ContainSingle(d => d.Contains("[LEAD-040]"));
    }

    // ── DriveChangeResult.Merge ────────────────────────────────────────────────

    [Fact]
    public void Merge_AggregatesCountsAndErrors()
    {
        var r1 = new DriveChangeResult(5, 3, 1, ["err1"]);
        var r2 = new DriveChangeResult(10, 7, 2, ["err2", "err3"]);
        var r3 = new DriveChangeResult(0, 0, 0, []);

        var merged = DriveChangeResult.Merge([r1, r2, r3]);

        merged.Processed.Should().Be(15);
        merged.StatusUpdated.Should().Be(10);
        merged.Errors.Should().Be(3);
        merged.ErrorDetails.Should().BeEquivalentTo(["err1", "err2", "err3"]);
    }
}
