using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Functions.Activation.Activities;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Functions.Tests.Activation;

/// <summary>
/// Unit tests for <see cref="RehostAssetsToR2Function"/>.
///
/// Approach:
/// - <see cref="ICloudflareR2Client"/> is mocked (Moq) to verify upload calls.
/// - HTTP responses are simulated via <see cref="MockHttpMessageHandler"/> wrapped in
///   a custom <see cref="IHttpClientFactory"/> that returns the configured client.
/// - <see cref="RehostAssetsToR2Function.RehostSingleAssetAsync"/> is tested directly
///   (internal via InternalsVisibleTo) for fine-grained coverage of each path.
/// </summary>
public sealed class RehostAssetsToR2FunctionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string Bucket = "agent-assets";
    private const string AccountId = "acc1";
    private const string AgentId = "agent1";

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static RehostAssetsToR2Function BuildFunction(
        HttpMessageHandler handler,
        Mock<ICloudflareR2Client>? r2Mock = null,
        string bucket = Bucket)
    {
        r2Mock ??= new Mock<ICloudflareR2Client>(MockBehavior.Loose);
        var httpClient = new HttpClient(handler) { BaseAddress = null };
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factory.Setup(f => f.CreateClient("RehostAssets")).Returns(httpClient);

        var options = Options.Create(new RehostAssetsOptions { BucketName = bucket });

        return new RehostAssetsToR2Function(
            r2Mock.Object,
            factory.Object,
            options,
            NullLogger<RehostAssetsToR2Function>.Instance);
    }

    private static HttpResponseMessage OkImageResponse(string contentType = "image/jpeg", int size = 1024)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var bytes = new byte[size];
        response.Content = new ByteArrayContent(bytes);
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return response;
    }

    // ── RunAsync: null URLs ─────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AllUrlsNull_ReturnsZeroRehosted()
    {
        var handler = new MockHttpMessageHandler();
        var fn = BuildFunction(handler);

        var result = await fn.RunAsync(
            new RehostAssetsInput { AccountId = AccountId, AgentId = AgentId },
            Ct);

        result.AssetsRehosted.Should().Be(0);
        result.HeadshotR2Url.Should().BeNull();
        result.LogoR2Url.Should().BeNull();
        result.IconR2Url.Should().BeNull();
        // No HTTP requests should have been made
        handler.Requests.Should().BeEmpty();
    }

    // ── RunAsync: successful rehosting ─────────────────────────────────────────

    [Fact]
    public async Task RunAsync_HeadshotOnly_ReturnsCorrectR2Url()
    {
        var r2Mock = new Mock<ICloudflareR2Client>(MockBehavior.Strict);
        r2Mock.Setup(r => r.PutObjectAsync(
                Bucket,
                $"agents/{AccountId}/{AgentId}/headshot.jpg",
                It.IsAny<Stream>(),
                "image/jpeg",
                Ct))
            .Returns(Task.CompletedTask);

        var handler = new MockHttpMessageHandler
        {
            ResponseToReturn = OkImageResponse("image/jpeg"),
        };

        var fn = BuildFunction(handler, r2Mock);

        var result = await fn.RunAsync(new RehostAssetsInput
        {
            AccountId = AccountId,
            AgentId = AgentId,
            HeadshotUrl = "https://example.com/headshot.jpg",
        }, Ct);

        result.AssetsRehosted.Should().Be(1);
        result.HeadshotR2Url.Should().Be($"https://assets.real-estate-star.com/agents/{AccountId}/{AgentId}/headshot.jpg");
        result.LogoR2Url.Should().BeNull();
        result.IconR2Url.Should().BeNull();

        r2Mock.VerifyAll();
    }

    [Fact]
    public async Task RunAsync_AllThreeAssets_ReturnsThreeR2Urls()
    {
        var r2Mock = new Mock<ICloudflareR2Client>(MockBehavior.Loose);
        r2Mock.Setup(r => r.PutObjectAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Return a different content type for each request so we can verify all get called
        var callCount = 0;
        var handler = new CallbackHttpMessageHandler(req =>
        {
            callCount++;
            return OkImageResponse("image/png");
        });

        var fn = BuildFunction(handler, r2Mock);

        var result = await fn.RunAsync(new RehostAssetsInput
        {
            AccountId = AccountId,
            AgentId = AgentId,
            HeadshotUrl = "https://example.com/headshot.png",
            LogoUrl = "https://example.com/logo.png",
            IconUrl = "https://example.com/icon.png",
        }, Ct);

        result.AssetsRehosted.Should().Be(3);
        result.HeadshotR2Url.Should().Be($"https://assets.real-estate-star.com/agents/{AccountId}/{AgentId}/headshot.png");
        result.LogoR2Url.Should().Be($"https://assets.real-estate-star.com/agents/{AccountId}/{AgentId}/logo.png");
        result.IconR2Url.Should().Be($"https://assets.real-estate-star.com/agents/{AccountId}/{AgentId}/icon.png");
        callCount.Should().Be(3);
    }

    // ── RunAsync: partial failure ───────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_HeadshotDownloadFails_OtherAssetsStillProcessed()
    {
        var r2Mock = new Mock<ICloudflareR2Client>(MockBehavior.Loose);
        r2Mock.Setup(r => r.PutObjectAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Headshot returns 404; logo and icon return 200
        var callIndex = 0;
        var handler = new CallbackHttpMessageHandler(req =>
        {
            callIndex++;
            var url = req.RequestUri?.ToString() ?? "";
            if (url.Contains("headshot"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            return OkImageResponse("image/jpeg");
        });

        var fn = BuildFunction(handler, r2Mock);

        var result = await fn.RunAsync(new RehostAssetsInput
        {
            AccountId = AccountId,
            AgentId = AgentId,
            HeadshotUrl = "https://example.com/headshot.jpg",
            LogoUrl = "https://example.com/logo.jpg",
            IconUrl = "https://example.com/icon.jpg",
        }, Ct);

        result.AssetsRehosted.Should().Be(2);
        result.HeadshotR2Url.Should().BeNull();
        result.LogoR2Url.Should().NotBeNullOrEmpty();
        result.IconR2Url.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunAsync_HttpClientThrows_AssetSkipped_OthersProcessed()
    {
        var r2Mock = new Mock<ICloudflareR2Client>(MockBehavior.Loose);
        r2Mock.Setup(r => r.PutObjectAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Headshot throws; logo succeeds
        var handler = new CallbackHttpMessageHandler(req =>
        {
            var url = req.RequestUri?.ToString() ?? "";
            if (url.Contains("headshot"))
                throw new HttpRequestException("connection refused");
            return OkImageResponse("image/jpeg");
        });

        var fn = BuildFunction(handler, r2Mock);

        var result = await fn.RunAsync(new RehostAssetsInput
        {
            AccountId = AccountId,
            AgentId = AgentId,
            HeadshotUrl = "https://example.com/headshot.jpg",
            LogoUrl = "https://example.com/logo.jpg",
        }, Ct);

        result.AssetsRehosted.Should().Be(1);
        result.HeadshotR2Url.Should().BeNull();
        result.LogoR2Url.Should().NotBeNullOrEmpty();
    }

    // ── RunAsync: size cap ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ContentLengthExceedsCap_AssetSkipped()
    {
        var r2Mock = new Mock<ICloudflareR2Client>(MockBehavior.Strict);
        // R2 should NOT be called — the asset is rejected before upload
        var handler = new CallbackHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var bytes = new byte[1024];
            response.Content = new ByteArrayContent(bytes);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            response.Content.Headers.ContentLength = 6 * 1024 * 1024L; // 6 MB > 5 MB cap
            return response;
        });

        var fn = BuildFunction(handler, r2Mock);

        var result = await fn.RunAsync(new RehostAssetsInput
        {
            AccountId = AccountId,
            AgentId = AgentId,
            HeadshotUrl = "https://example.com/huge.jpg",
        }, Ct);

        result.AssetsRehosted.Should().Be(0);
        result.HeadshotR2Url.Should().BeNull();

        // R2 PutObjectAsync must NOT have been called
        r2Mock.Verify(r => r.PutObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_StreamBodyExceedsCap_AssetSkipped()
    {
        var r2Mock = new Mock<ICloudflareR2Client>(MockBehavior.Strict);

        // No Content-Length header, but body is > 5 MB
        var handler = new CallbackHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var oversizedBytes = new byte[6 * 1024 * 1024]; // 6 MB body, no Content-Length set
            response.Content = new ByteArrayContent(oversizedBytes);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return response;
        });

        var fn = BuildFunction(handler, r2Mock);

        var result = await fn.RunAsync(new RehostAssetsInput
        {
            AccountId = AccountId,
            AgentId = AgentId,
            HeadshotUrl = "https://example.com/huge.jpg",
        }, Ct);

        result.AssetsRehosted.Should().Be(0);
        result.HeadshotR2Url.Should().BeNull();

        r2Mock.Verify(r => r.PutObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── GetExtensionFromContentType ─────────────────────────────────────────────

    [Theory]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/jpg", ".jpg")]
    [InlineData("image/png", ".png")]
    [InlineData("image/gif", ".gif")]
    [InlineData("image/webp", ".webp")]
    [InlineData("image/svg+xml", ".svg")]
    [InlineData("image/x-icon", ".ico")]
    [InlineData("image/vnd.microsoft.icon", ".ico")]
    [InlineData("image/avif", ".avif")]
    [InlineData("application/octet-stream", ".bin")]
    [InlineData("text/html", ".bin")]
    [InlineData("IMAGE/JPEG", ".jpg")] // case-insensitive
    public void GetExtensionFromContentType_ReturnsExpectedExtension(string contentType, string expectedExt)
    {
        var ext = RehostAssetsToR2Function.GetExtensionFromContentType(contentType);
        ext.Should().Be(expectedExt);
    }

    // ── RehostSingleAssetAsync: success path ────────────────────────────────────

    [Fact]
    public async Task RehostSingleAssetAsync_Success_UploadsToCorrectR2Key()
    {
        string? capturedKey = null;
        string? capturedContentType = null;

        var r2Mock = new Mock<ICloudflareR2Client>(MockBehavior.Strict);
        r2Mock.Setup(r => r.PutObjectAsync(
                Bucket,
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                Ct))
            .Callback<string, string, Stream, string, CancellationToken>(
                (_, key, _, ct2, _) => { capturedKey = key; capturedContentType = ct2; })
            .Returns(Task.CompletedTask);

        var handler = new MockHttpMessageHandler { ResponseToReturn = OkImageResponse("image/webp") };
        var fn = BuildFunction(handler, r2Mock);

        var url = await fn.RehostSingleAssetAsync(
            Bucket, AccountId, AgentId, "headshot", "https://example.com/pic.webp", Ct);

        url.Should().Be($"https://assets.real-estate-star.com/agents/{AccountId}/{AgentId}/headshot.webp");
        capturedKey.Should().Be($"agents/{AccountId}/{AgentId}/headshot.webp");
        capturedContentType.Should().Be("image/webp");

        r2Mock.VerifyAll();
    }

    [Fact]
    public async Task RehostSingleAssetAsync_NonSuccessStatusCode_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.Forbidden),
        };

        var fn = BuildFunction(handler);

        var url = await fn.RehostSingleAssetAsync(
            Bucket, AccountId, AgentId, "logo", "https://example.com/logo.png", Ct);

        url.Should().BeNull();
    }

    [Fact]
    public async Task RehostSingleAssetAsync_NullContentType_FallsBackToOctetStream()
    {
        string? capturedContentType = null;

        var r2Mock = new Mock<ICloudflareR2Client>(MockBehavior.Loose);
        r2Mock.Setup(r => r.PutObjectAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Stream, string, CancellationToken>(
                (_, _, _, ct2, _) => capturedContentType = ct2)
            .Returns(Task.CompletedTask);

        // No Content-Type header
        var handler = new CallbackHttpMessageHandler(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new ByteArrayContent(new byte[100]);
            // Deliberately leave Content-Type null
            return resp;
        });

        var fn = BuildFunction(handler, r2Mock);

        var url = await fn.RehostSingleAssetAsync(
            Bucket, AccountId, AgentId, "icon", "https://example.com/icon", Ct);

        url.Should().NotBeNullOrEmpty();
        capturedContentType.Should().Be("application/octet-stream");
    }

    // ── Concurrent cap (SemaphoreSlim) ─────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ThreeAssets_MaxTwoInFlightAtATime()
    {
        // Track concurrent in-flight download count
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        var r2Mock = new Mock<ICloudflareR2Client>(MockBehavior.Loose);
        r2Mock.Setup(r => r.PutObjectAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CallbackHttpMessageHandler(async req =>
        {
            lock (lockObj)
            {
                currentConcurrent++;
                if (currentConcurrent > maxConcurrent)
                    maxConcurrent = currentConcurrent;
            }
            // Simulate async download delay
            await Task.Delay(10, CancellationToken.None).ConfigureAwait(false);
            lock (lockObj) { currentConcurrent--; }
            return OkImageResponse("image/jpeg");
        });

        var fn = BuildFunction(handler, r2Mock);

        await fn.RunAsync(new RehostAssetsInput
        {
            AccountId = AccountId,
            AgentId = AgentId,
            HeadshotUrl = "https://example.com/headshot.jpg",
            LogoUrl = "https://example.com/logo.jpg",
            IconUrl = "https://example.com/icon.jpg",
        }, Ct);

        maxConcurrent.Should().BeLessThanOrEqualTo(2,
            "the SemaphoreSlim(2,2) must prevent more than 2 concurrent downloads");
    }

    // ── DTOs ───────────────────────────────────────────────────────────────────

    [Fact]
    public void RehostAssetsInput_DefaultValues_AreEmpty()
    {
        var input = new RehostAssetsInput();
        input.AccountId.Should().BeEmpty();
        input.AgentId.Should().BeEmpty();
        input.CorrelationId.Should().BeEmpty();
        input.HeadshotUrl.Should().BeNull();
        input.LogoUrl.Should().BeNull();
        input.IconUrl.Should().BeNull();
    }

    [Fact]
    public void RehostAssetsResult_DefaultValues_AreZeroAndNull()
    {
        var result = new RehostAssetsResult();
        result.AssetsRehosted.Should().Be(0);
        result.HeadshotR2Url.Should().BeNull();
        result.LogoR2Url.Should().BeNull();
        result.IconR2Url.Should().BeNull();
    }

    // ── Private helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that invokes a delegate (synchronous or async)
    /// per request, used for per-URL response differentiation in tests.
    /// </summary>
    private sealed class CallbackHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> asyncCallback) : HttpMessageHandler
    {
        public CallbackHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> callback)
            : this(req => Task.FromResult(callback(req))) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => asyncCallback(request);
    }
}
