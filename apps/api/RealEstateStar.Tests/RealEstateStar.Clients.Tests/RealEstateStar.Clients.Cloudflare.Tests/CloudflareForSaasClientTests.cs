using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Cloudflare.Tests;

public class CloudflareForSaasClientTests
{
    private const string ZoneId = "test-zone-id";
    private const string HostnameId = "test-hostname-id";

    private static (CloudflareForSaasClient client, MockHttpMessageHandler handler) BuildClient()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.cloudflare.com/client/v4/")
        };
        var client = new CloudflareForSaasClient(
            httpClient,
            ZoneId,
            NullLogger<CloudflareForSaasClient>.Instance);
        return (client, handler);
    }

    private static string SuccessEnvelope(string id, string hostname, string status, string? sslStatus = "active") =>
        $$"""
        {
          "success": true,
          "result": {
            "id": "{{id}}",
            "hostname": "{{hostname}}",
            "status": "{{status}}",
            "ssl": {
              "status": "{{sslStatus}}"
            }
          }
        }
        """;

    [Fact]
    public async Task CreateCustomHostnameAsync_Success_ReturnsResult()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent(
                SuccessEnvelope("new-id-123", "agent.example.com", "pending"))
        };

        var result = await client.CreateCustomHostnameAsync("agent.example.com", CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be("new-id-123");
        result.Hostname.Should().Be("agent.example.com");
        result.Status.Should().Be("pending");
        result.SslStatus.Should().Be("active");
    }

    [Fact]
    public async Task CreateCustomHostnameAsync_ApiError_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new System.Net.Http.StringContent("""{"success":false,"errors":[{"code":1414,"message":"Invalid hostname"}]}""")
        };

        var act = () => client.CreateCustomHostnameAsync("invalid!", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateCustomHostnameAsync_NullResult_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("""{"success":true,"result":null}""")
        };

        var act = () => client.CreateCustomHostnameAsync("agent.example.com", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateCustomHostnameAsync_InvalidJson_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("not json {{{")
        };

        var act = () => client.CreateCustomHostnameAsync("agent.example.com", CancellationToken.None);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task CreateCustomHostnameAsync_PostsToCorrectUrl()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent(
                SuccessEnvelope("id", "h.example.com", "pending"))
        };

        await client.CreateCustomHostnameAsync("h.example.com", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Contain($"zones/{ZoneId}/custom_hostnames");
    }

    [Fact]
    public async Task DeleteCustomHostnameAsync_Success_DoesNotThrow()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("""{"success":true,"result":{"id":"test-hostname-id"}}""")
        };

        var act = () => client.DeleteCustomHostnameAsync(HostnameId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteCustomHostnameAsync_ApiError_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new System.Net.Http.StringContent("not found")
        };

        var act = () => client.DeleteCustomHostnameAsync(HostnameId, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DeleteCustomHostnameAsync_SendsDeleteToCorrectUrl()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("""{"success":true}""")
        };

        await client.DeleteCustomHostnameAsync(HostnameId, CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.ToString().Should().Contain($"zones/{ZoneId}/custom_hostnames/{HostnameId}");
    }

    [Fact]
    public async Task GetCustomHostnameAsync_HostnameExists_ReturnsResult()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent(
                SuccessEnvelope(HostnameId, "agent.example.com", "active", "active"))
        };

        var result = await client.GetCustomHostnameAsync(HostnameId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(HostnameId);
        result.Hostname.Should().Be("agent.example.com");
        result.Status.Should().Be("active");
        result.SslStatus.Should().Be("active");
    }

    [Fact]
    public async Task GetCustomHostnameAsync_NotFound_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NotFound);

        var result = await client.GetCustomHostnameAsync(HostnameId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCustomHostnameAsync_NullResult_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("""{"success":true,"result":null}""")
        };

        var result = await client.GetCustomHostnameAsync(HostnameId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCustomHostnameAsync_ApiError_Throws()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new System.Net.Http.StringContent("server error")
        };

        var act = () => client.GetCustomHostnameAsync(HostnameId, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetCustomHostnameAsync_NoSslField_ReturnsSslStatusNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("""
                {
                  "success": true,
                  "result": {
                    "id": "test-hostname-id",
                    "hostname": "agent.example.com",
                    "status": "pending"
                  }
                }
                """)
        };

        var result = await client.GetCustomHostnameAsync(HostnameId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.SslStatus.Should().BeNull();
    }
}
