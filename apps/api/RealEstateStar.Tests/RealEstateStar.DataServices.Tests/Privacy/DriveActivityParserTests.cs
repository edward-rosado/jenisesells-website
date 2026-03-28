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
namespace RealEstateStar.DataServices.Tests.Privacy;

public class DriveActivityParserTests
{
    // ── Parse gws CLI JSON output ──────────────────────────────────────────────

    [Fact]
    public void Parse_ReturnsDriveActivityEvents_ForValidGwsJson()
    {
        var json = """
            [
              {
                "action": "Move",
                "fileName": "Lead Profile.md",
                "folderPath": "Real Estate Star/1 - Leads/Jane Doe",
                "destinationParent": "Real Estate Star/2 - Active Clients/Jane Doe",
                "timestamp": "2026-03-19T12:00:00Z"
              }
            ]
            """;

        var events = DriveActivityParser.Parse(json);

        events.Should().HaveCount(1);
        events[0].Action.Should().Be("Move");
        events[0].FileName.Should().Be("Lead Profile.md");
        events[0].FolderPath.Should().Be("Real Estate Star/1 - Leads/Jane Doe");
        events[0].DestinationParent.Should().Be("Real Estate Star/2 - Active Clients/Jane Doe");
        events[0].Timestamp.Should().Be(new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc));
    }

    // ── Handles empty activity ─────────────────────────────────────────────────

    [Fact]
    public void Parse_ReturnsEmpty_ForEmptyArray()
    {
        var events = DriveActivityParser.Parse("[]");

        events.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ReturnsEmpty_ForNullOrWhitespace()
    {
        DriveActivityParser.Parse("").Should().BeEmpty();
        DriveActivityParser.Parse("   ").Should().BeEmpty();
    }

    // ── Action type mapping ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Move", "Move")]
    [InlineData("Create", "Create")]
    [InlineData("Edit", "Edit")]
    [InlineData("Delete", "Delete")]
    [InlineData("Rename", "Rename")]
    [InlineData("move", "Move")]
    [InlineData("CREATE", "Create")]
    public void Parse_MapsActionTypes_CaseInsensitive(string rawAction, string expectedAction)
    {
        var json = $$"""
            [
              {
                "action": "{{rawAction}}",
                "fileName": "file.md",
                "folderPath": "Real Estate Star/1 - Leads/Jane Doe",
                "timestamp": "2026-03-19T12:00:00Z"
              }
            ]
            """;

        var events = DriveActivityParser.Parse(json);

        events.Should().HaveCount(1);
        events[0].Action.Should().Be(expectedAction);
    }

    // ── Destination parent extraction ──────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsDestinationParent_ForMoveEvent()
    {
        var json = """
            [
              {
                "action": "Move",
                "fileName": "Lead Profile.md",
                "folderPath": "Real Estate Star/1 - Leads/John Smith",
                "destinationParent": "Real Estate Star/3 - Under Contract/John Smith",
                "timestamp": "2026-03-19T12:00:00Z"
              },
              {
                "action": "Edit",
                "fileName": "Research.md",
                "folderPath": "Real Estate Star/2 - Active Clients/Jane Doe",
                "timestamp": "2026-03-19T13:00:00Z"
              }
            ]
            """;

        var events = DriveActivityParser.Parse(json);

        events.Should().HaveCount(2);
        events[0].DestinationParent.Should().Be("Real Estate Star/3 - Under Contract/John Smith");
        events[1].DestinationParent.Should().BeNull();
    }
}
