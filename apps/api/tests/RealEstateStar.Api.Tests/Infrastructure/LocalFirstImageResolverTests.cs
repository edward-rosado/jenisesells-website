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
    private readonly string _tempDir;
    private readonly Mock<IWebHostEnvironment> _envMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

    public LocalFirstImageResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _envMock = new Mock<IWebHostEnvironment>();
        _envMock.Setup(e => e.ContentRootPath).Returns(_tempDir);

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

    [Fact]
    public async Task ResolveAsync_ReturnsBytes_WhenDockerLocalFileExists()
    {
        // Arrange
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var dockerPath = Path.Combine(_tempDir, "config", "accounts", "jenise-buckalew");
        Directory.CreateDirectory(dockerPath);
        await File.WriteAllBytesAsync(Path.Combine(dockerPath, "logo.png"), imageBytes);

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
        // ContentRootPath/../../../apps/agent-site/public/{relativePath}
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF }; // JPEG SOI marker
        var agentSitePublicPath = Path.Combine(_tempDir, "..", "..", "..", "apps", "agent-site", "public", "agents", "jenise-buckalew");
        Directory.CreateDirectory(agentSitePublicPath);
        await File.WriteAllBytesAsync(Path.Combine(agentSitePublicPath, "headshot.jpg"), imageBytes);

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

        var dockerPath = Path.Combine(_tempDir, "config", "accounts", "jenise-buckalew");
        Directory.CreateDirectory(dockerPath);
        await File.WriteAllBytesAsync(Path.Combine(dockerPath, "logo.png"), dockerBytes);

        var localDevPath = Path.Combine(_tempDir, "..", "..", "..", "apps", "agent-site", "public", "agents", "jenise-buckalew");
        Directory.CreateDirectory(localDevPath);
        await File.WriteAllBytesAsync(Path.Combine(localDevPath, "logo.png"), localDevBytes);

        var resolver = BuildResolver();

        // Act
        var result = await resolver.ResolveAsync("jenise-buckalew", "/agents/jenise-buckalew/logo.png", CancellationToken.None);

        // Assert: Docker path wins
        result.Should().BeEquivalentTo(dockerBytes);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }
}
