using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Cloudflare.Tests;

public class CloudflareKvClientTests
{
    private const string NamespaceId = "test-namespace-id";
    private const string Key = "test-key";

    private static (CloudflareKvClient client, MockHttpMessageHandler handler) BuildClient()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.cloudflare.com/client/v4/accounts/test-account/storage/kv/")
        };
        var client = new CloudflareKvClient(httpClient, NullLogger<CloudflareKvClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task GetAsync_KeyExists_ReturnsValue()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("hello-world", Encoding.UTF8, "text/plain")
        };

        var result = await client.GetAsync(NamespaceId, Key, CancellationToken.None);

        result.Should().Be("hello-world");
    }

    [Fact]
    public async Task GetAsync_KeyNotFound_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NotFound);

        var result = await client.GetAsync(NamespaceId, Key, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ApiError_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal error")
        };

        var act = () => client.GetAsync(NamespaceId, Key, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetAsync_BuildsCorrectUrl()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("value")
        };

        await client.GetAsync(NamespaceId, "my/special-key", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        var uri = handler.LastRequest!.RequestUri!.ToString();
        uri.Should().Contain(NamespaceId);
        uri.Should().Contain("my%2Fspecial-key");
    }

    [Fact]
    public async Task PutAsync_Success_DoesNotThrow()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        var act = () => client.PutAsync(NamespaceId, Key, "new-value", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PutAsync_SendsPutRequestWithCorrectKey()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await client.PutAsync(NamespaceId, Key, "stored-value", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.ToString().Should().Contain($"namespaces/{NamespaceId}/values/{Key}");
    }

    [Fact]
    public async Task PutAsync_ApiError_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("Forbidden")
        };

        var act = () => client.PutAsync(NamespaceId, Key, "value", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DeleteAsync_Success_DoesNotThrow()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        var act = () => client.DeleteAsync(NamespaceId, Key, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_ApiError_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("error")
        };

        var act = () => client.DeleteAsync(NamespaceId, Key, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ListKeysAsync_Success_ReturnsKeys()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "result": [
                    { "name": "key-alpha" },
                    { "name": "key-beta" }
                  ]
                }
                """)
        };

        var result = await client.ListKeysAsync(NamespaceId, null, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain("key-alpha");
        result.Should().Contain("key-beta");
    }

    [Fact]
    public async Task ListKeysAsync_WithPrefix_AppendsQueryParam()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "result": [] }""")
        };

        await client.ListKeysAsync(NamespaceId, "agent-", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.Query.Should().Contain("prefix=agent-");
    }

    [Fact]
    public async Task ListKeysAsync_NullResult_ReturnsEmptyList()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "result": null }""")
        };

        var result = await client.ListKeysAsync(NamespaceId, null, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListKeysAsync_ApiError_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("error")
        };

        var act = () => client.ListKeysAsync(NamespaceId, null, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ListKeysAsync_InvalidJson_ThrowsJsonException()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not valid json {{{")
        };

        var act = () => client.ListKeysAsync(NamespaceId, null, CancellationToken.None);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }
}
