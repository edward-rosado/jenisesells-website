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
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Tests.Infrastructure;

public class LocalFirstImageResolverTests : IDisposable
{
    // ContentRootPath is set to {_root}/apps/api so that ../../../ resolves back to {_root},
    // keeping all test files inside the unique per-test temp directory.
    private readonly string _root;
    private readonly string _contentRoot;
    private readonly Mock<IWebHostEnvironment> _envMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

    public LocalFirstImageResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        // ContentRootPath mirrors the real path: apps/api/RealEstateStar.Api
        // so that ../../.. resolves back to _root (the monorepo root equivalent)
        _contentRoot = Path.Combine(_root, "apps", "api", "RealEstateStar.Api");
        Directory.CreateDirectory(_contentRoot);

        _envMock = new Mock<IWebHostEnvironment>();
        _envMock.Setup(e => e.ContentRootPath).Returns(_contentRoot);

        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
    }

    private LocalFirstImageResolver BuildResolver(HttpResponseMessage? httpResponse = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse ?? new HttpResponseMessage(HttpStatusCode.NotFound));

        var client = new HttpClient(handler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("image-resolver")).Returns(client);

        return new LocalFirstImageResolver(
            _httpClientFactoryMock.Object,
            _envMock.Object,
            NullLogger<LocalFirstImageResolver>.Instance);
    }

    private string DockerImagePath(string handle, string fileName) =>
        Path.Combine(_contentRoot, "config", "accounts", handle, fileName);

    private string LocalDevImagePath(string relativeWebPath) =>
        // ContentRootPath = {_root}/apps/api; ../../.. = {_root}; then apps/agent-site/public/{relativeWebPath}
        Path.Combine(_root, "apps", "agent-site", "public", relativeWebPath.TrimStart('/'));

    [Fact]
    public async Task ResolveAsync_ReturnsBytes_WhenDockerLocalFileExists()
    {
        // Arrange
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var dockerDir = Path.GetDirectoryName(DockerImagePath("jenise-buckalew", "logo.png"))!;
        Directory.CreateDirectory(dockerDir);
        await File.WriteAllBytesAsync(DockerImagePath("jenise-buckalew", "logo.png"), imageBytes);

        var resolver = BuildResolver();

        // Act
        var result = await resolver.ResolveAsync("jenise-buckalew", "/agents/jenise-buckalew/logo.png", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(imageBytes);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsBytes_WhenLocalDevFileExists()
    {
        // Arrange: place the file at the local dev path relative to ContentRootPath
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF }; // JPEG SOI marker
        var localDevFile = LocalDevImagePath("/agents/jenise-buckalew/headshot.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(localDevFile)!);
        await File.WriteAllBytesAsync(localDevFile, imageBytes);

        var resolver = BuildResolver();

        // Act
        var result = await resolver.ResolveAsync("jenise-buckalew", "/agents/jenise-buckalew/headshot.jpg", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(imageBytes);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsBytes_WhenHttpFallbackSucceeds()
    {
        // Arrange: no local file exists; HTTP returns success
        var imageBytes = new byte[] { 0x47, 0x49, 0x46, 0x38 }; // GIF header
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(imageBytes)
        };

        var resolver = BuildResolver(httpResponse);

        // Act
        var result = await resolver.ResolveAsync("jenise-buckalew", "/agents/jenise-buckalew/logo.png", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(imageBytes);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_WhenFileNotFoundAndHttpFails()
    {
        // Arrange: no local file, HTTP returns 404
        var resolver = BuildResolver(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var result = await resolver.ResolveAsync("jenise-buckalew", "/agents/jenise-buckalew/logo.png", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_WhenHttpThrows()
    {
        // Arrange: no local file; HTTP client throws
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new HttpClient(handler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("image-resolver")).Returns(client);

        var resolver = new LocalFirstImageResolver(
            _httpClientFactoryMock.Object,
            _envMock.Object,
            NullLogger<LocalFirstImageResolver>.Instance);

        // Act
        var result = await resolver.ResolveAsync("jenise-buckalew", "/agents/jenise-buckalew/logo.png", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAsync_ReturnsNull_WhenPathIsNullOrEmpty(string? relativePath)
    {
        // Arrange
        var resolver = BuildResolver();

        // Act
        var result = await resolver.ResolveAsync("jenise-buckalew", relativePath!, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_DockerPathTakesPriority_OverLocalDevPath()
    {
        // Arrange: both Docker and local dev paths exist with different content
        var dockerBytes = new byte[] { 0x01, 0x02, 0x03 };
        var localDevBytes = new byte[] { 0x04, 0x05, 0x06 };

        var dockerDir = Path.GetDirectoryName(DockerImagePath("jenise-buckalew", "logo.png"))!;
        Directory.CreateDirectory(dockerDir);
        await File.WriteAllBytesAsync(DockerImagePath("jenise-buckalew", "logo.png"), dockerBytes);

        var localDevFile = LocalDevImagePath("/agents/jenise-buckalew/logo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(localDevFile)!);
        await File.WriteAllBytesAsync(localDevFile, localDevBytes);

        var resolver = BuildResolver();

        // Act
        var result = await resolver.ResolveAsync("jenise-buckalew", "/agents/jenise-buckalew/logo.png", CancellationToken.None);

        // Assert: Docker path wins
        result.Should().BeEquivalentTo(dockerBytes);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }
}
