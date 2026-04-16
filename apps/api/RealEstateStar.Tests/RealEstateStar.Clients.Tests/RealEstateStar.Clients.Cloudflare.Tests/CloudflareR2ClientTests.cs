using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Cloudflare.Tests;

public class CloudflareR2ClientTests
{
    private const string Bucket = "test-bucket";
    private const string Key = "test/object.pdf";

    private static (CloudflareR2Client client, MockHttpMessageHandler handler) BuildClient()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.cloudflare.com/client/v4/accounts/test-account/r2/")
        };
        var client = new CloudflareR2Client(httpClient, NullLogger<CloudflareR2Client>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task PutObjectAsync_Success_DoesNotThrow()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("pdf-bytes"));
        var act = () => client.PutObjectAsync(Bucket, Key, content, "application/pdf", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PutObjectAsync_SetsContentType()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        await client.PutObjectAsync(Bucket, Key, content, "image/png", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Content!.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task PutObjectAsync_ApiError_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("Forbidden")
        };

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var act = () => client.PutObjectAsync(Bucket, Key, content, "application/pdf", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetObjectAsync_ObjectExists_ReturnsStream()
    {
        var (client, handler) = BuildClient();
        var bytes = Encoding.UTF8.GetBytes("object-content");
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        };

        var result = await client.GetObjectAsync(Bucket, Key, CancellationToken.None);

        result.Should().NotBeNull();
        using var reader = new StreamReader(result!);
        var text = await reader.ReadToEndAsync();
        text.Should().Be("object-content");
    }

    [Fact]
    public async Task GetObjectAsync_ObjectNotFound_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NotFound);

        var result = await client.GetObjectAsync(Bucket, Key, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetObjectAsync_ApiError_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("server error")
        };

        var act = () => client.GetObjectAsync(Bucket, Key, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetObjectAsync_BuildsCorrectUrl()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NotFound);

        await client.GetObjectAsync(Bucket, "folder/lead-report.pdf", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        var uri = handler.LastRequest!.RequestUri!.ToString();
        uri.Should().Contain("folder%2Flead-report.pdf");
    }

    [Fact]
    public async Task DeleteObjectAsync_Success_DoesNotThrow()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        var act = () => client.DeleteObjectAsync(Bucket, Key, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteObjectAsync_ApiError_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("Forbidden")
        };

        var act = () => client.DeleteObjectAsync(Bucket, Key, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
