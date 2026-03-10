using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Cma.Services.Gws;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class DriveFolderInitializerTests
{
    private static readonly string[] ExpectedTopLevelFolders =
    [
        "Real Estate Star/1 - Leads",
        "Real Estate Star/2 - Active Clients",
        "Real Estate Star/3 - Under Contract",
        "Real Estate Star/4 - Closed",
        "Real Estate Star/5 - Inactive",
        "Real Estate Star/6 - Referral Network",
    ];

    private static readonly string[] ExpectedSubfolders =
    [
        "Real Estate Star/5 - Inactive/Dead Leads",
        "Real Estate Star/5 - Inactive/Expired Clients",
        "Real Estate Star/6 - Referral Network/Agents",
        "Real Estate Star/6 - Referral Network/Brokerages",
        "Real Estate Star/6 - Referral Network/Summary",
    ];

    [Fact]
    public async Task EnsureFolderStructureAsync_CreatesAllTopLevelFolders()
    {
        var gws = new Mock<IGwsService>();
        var svc = new DriveFolderInitializer(gws.Object, NullLogger<DriveFolderInitializer>.Instance);

        await svc.EnsureFolderStructureAsync("agent@test.com", CancellationToken.None);

        foreach (var folder in ExpectedTopLevelFolders)
        {
            gws.Verify(g => g.CreateDriveFolderAsync(
                "agent@test.com",
                folder,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task EnsureFolderStructureAsync_CreatesAllSubfolders()
    {
        var gws = new Mock<IGwsService>();
        var svc = new DriveFolderInitializer(gws.Object, NullLogger<DriveFolderInitializer>.Instance);

        await svc.EnsureFolderStructureAsync("agent@test.com", CancellationToken.None);

        foreach (var folder in ExpectedSubfolders)
        {
            gws.Verify(g => g.CreateDriveFolderAsync(
                "agent@test.com",
                folder,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task EnsureFolderStructureAsync_Creates11FoldersTotal()
    {
        var gws = new Mock<IGwsService>();
        var svc = new DriveFolderInitializer(gws.Object, NullLogger<DriveFolderInitializer>.Instance);

        await svc.EnsureFolderStructureAsync("agent@test.com", CancellationToken.None);

        gws.Verify(g => g.CreateDriveFolderAsync(
            "agent@test.com",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(11));
    }

    [Fact]
    public async Task EnsureFolderStructureAsync_OnlyRunsOnce_ForSameAgent()
    {
        var gws = new Mock<IGwsService>();
        var svc = new DriveFolderInitializer(gws.Object, NullLogger<DriveFolderInitializer>.Instance);

        await svc.EnsureFolderStructureAsync("agent@test.com", CancellationToken.None);
        await svc.EnsureFolderStructureAsync("agent@test.com", CancellationToken.None);

        // Should only create folders once per agent
        gws.Verify(g => g.CreateDriveFolderAsync(
            "agent@test.com",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(11));
    }

    [Fact]
    public async Task EnsureFolderStructureAsync_RunsForDifferentAgents()
    {
        var gws = new Mock<IGwsService>();
        var svc = new DriveFolderInitializer(gws.Object, NullLogger<DriveFolderInitializer>.Instance);

        await svc.EnsureFolderStructureAsync("agent1@test.com", CancellationToken.None);
        await svc.EnsureFolderStructureAsync("agent2@test.com", CancellationToken.None);

        // 11 folders for each agent = 22 total
        gws.Verify(g => g.CreateDriveFolderAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(22));
    }

    [Fact]
    public async Task EnsureFolderStructureAsync_ContinuesOnPartialFailure()
    {
        var callCount = 0;
        var gws = new Mock<IGwsService>();
        gws.Setup(g => g.CreateDriveFolderAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((_, path, _) =>
            {
                callCount++;
                if (path.Contains("2 - Active Clients"))
                    throw new InvalidOperationException("Drive error");
                return Task.FromResult(path);
            });

        var svc = new DriveFolderInitializer(gws.Object, NullLogger<DriveFolderInitializer>.Instance);

        // Should not throw — continues on partial failure
        await svc.EnsureFolderStructureAsync("agent@test.com", CancellationToken.None);

        // Should have attempted all 11 folders despite one failing
        Assert.Equal(11, callCount);
    }

    [Fact]
    public async Task EnsureFolderStructureAsync_PassesCancellationToken()
    {
        var gws = new Mock<IGwsService>();
        var svc = new DriveFolderInitializer(gws.Object, NullLogger<DriveFolderInitializer>.Instance);
        using var cts = new CancellationTokenSource();

        await svc.EnsureFolderStructureAsync("agent@test.com", cts.Token);

        gws.Verify(g => g.CreateDriveFolderAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            cts.Token), Times.Exactly(11));
    }
}
