using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using RealEstateStar.Api.Hubs;

namespace RealEstateStar.Api.Tests.Hubs;

public class CmaProgressHubTests
{
    [Fact]
    public async Task JoinJob_AddsConnectionToGroup()
    {
        var hub = new CmaProgressHub();

        var mockGroups = new Mock<IGroupManager>();
        mockGroups
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("conn-123");

        hub.Groups = mockGroups.Object;
        hub.Context = mockContext.Object;

        await hub.JoinJob("job-456");

        mockGroups.Verify(
            g => g.AddToGroupAsync("conn-123", "job-456", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
