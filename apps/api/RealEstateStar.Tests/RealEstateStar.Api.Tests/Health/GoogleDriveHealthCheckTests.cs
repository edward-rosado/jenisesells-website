using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using RealEstateStar.Api.Health;

namespace RealEstateStar.Api.Tests.Health;

public class GoogleDriveHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenListDocumentsSucceeds()
    {
        var provider = new Mock<IFileStorageProvider>();
        provider
            .Setup(p => p.ListDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var check = new GoogleDriveHealthCheck(provider.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("reachable");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenExceptionThrown()
    {
        var exception = new InvalidOperationException("Drive unavailable");
        var provider = new Mock<IFileStorageProvider>();
        provider
            .Setup(p => p.ListDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var check = new GoogleDriveHealthCheck(provider.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("unreachable");
        result.Exception.Should().BeSameAs(exception);
    }
}
