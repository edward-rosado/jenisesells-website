using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Tests.Infrastructure;

public class ApiKeyHmacMiddlewareTests
{
    private const string TestSecret = "test-hmac-secret";
    private const string TestApiKey = "key-abc123";
    private const string TestAgentId = "agent-1";

    // Builds a minimal ASP.NET test host wired with the middleware and a simple /agents/{agentId}/leads endpoint.
    private static HttpClient BuildClient(ApiKeyHmacOptions? options = null)
    {
        var effectiveOptions = options ?? new ApiKeyHmacOptions
        {
            HmacSecret = TestSecret,
            ApiKeys = new Dictionary<string, string> { { TestApiKey, TestAgentId } },
            MaxTimestampDriftSeconds = 300
        };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddSingleton<IOptions<ApiKeyHmacOptions>>(new OptionsWrapper<ApiKeyHmacOptions>(effectiveOptions));
        builder.Logging.ClearProviders(); // keep test output clean unless overridden

        var app = builder.Build();

        app.UseRouting();
        app.UseMiddleware<ApiKeyHmacMiddleware>();

        app.MapPost("/agents/{agentId}/leads", async (HttpContext ctx) =>
        {
            // Read body to verify buffering works
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            await ctx.Response.WriteAsync(body);
        });

        app.MapGet("/health", () => Results.Ok());

        app.StartAsync().GetAwaiter().GetResult();

        return app.GetTestClient();
    }

    private static string NowTimestamp() =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    private static string GenerateHmac(string secret, string timestamp, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var message = $"{timestamp}.{body}";
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }

    private static HttpRequestMessage BuildLeadRequest(
        string agentId,
        string body,
        string? apiKey = TestApiKey,
        string? timestamp = null,
        string? signature = null)
    {
        var ts = timestamp ?? NowTimestamp();
        var sig = signature ?? GenerateHmac(TestSecret, ts, body);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/agents/{agentId}/leads")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        if (apiKey is not null)
            request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);

        request.Headers.TryAddWithoutValidation("X-Timestamp", ts);
        request.Headers.TryAddWithoutValidation("X-Signature", sig);

        return request;
    }

    // Test 1: Valid API key + valid HMAC → request passes through (200)
    [Fact]
    public async Task ValidRequest_PassesThrough_Returns200()
    {
        var client = BuildClient();
        const string body = """{"name":"Jane Doe"}""";

        var response = await client.SendAsync(BuildLeadRequest(TestAgentId, body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Be(body);
    }

    // Test 2: Missing API key → 401
    [Fact]
    public async Task MissingApiKey_Returns401()
    {
        var client = BuildClient();
        const string body = """{"name":"Jane Doe"}""";
        var ts = NowTimestamp();
        var sig = GenerateHmac(TestSecret, ts, body);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/agents/{TestAgentId}/leads")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        // Deliberately omit X-API-Key
        request.Headers.TryAddWithoutValidation("X-Timestamp", ts);
        request.Headers.TryAddWithoutValidation("X-Signature", sig);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Test 3: Invalid API key → 401 + log [LEAD-019]
    [Fact]
    public async Task InvalidApiKey_Returns401_LogsLead019()
    {
        var logMessages = new List<string>();
        var mockLogger = new Mock<ILogger<ApiKeyHmacMiddleware>>();
        mockLogger
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[LEAD-019]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => logMessages.Add("[LEAD-019]"));

        var options = new ApiKeyHmacOptions
        {
            HmacSecret = TestSecret,
            ApiKeys = new Dictionary<string, string> { { TestApiKey, TestAgentId } },
            MaxTimestampDriftSeconds = 300
        };

        var body = """{"name":"Jane"}""";
        var ts = NowTimestamp();
        var sig = GenerateHmac(TestSecret, ts, body);

        // Exercise the middleware directly using DefaultHttpContext
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = $"/agents/{TestAgentId}/leads";
        httpContext.Request.Headers["X-API-Key"] = "INVALID-KEY";
        httpContext.Request.Headers["X-Timestamp"] = ts;
        httpContext.Request.Headers["X-Signature"] = sig;
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.RouteValues["agentId"] = TestAgentId;
        httpContext.Response.Body = new MemoryStream();

        var nextCalled = false;
        var middleware = new ApiKeyHmacMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new OptionsWrapper<ApiKeyHmacOptions>(options),
            mockLogger.Object);

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled.Should().BeFalse();
        logMessages.Should().ContainSingle(m => m.Contains("[LEAD-019]"));
    }

    // Test 4: API key maps to agentId that doesn't match route → 401 + log [LEAD-020]
    [Fact]
    public async Task ApiKeyAgentIdMismatch_Returns401_LogsLead020()
    {
        var logMessages = new List<string>();
        var mockLogger = new Mock<ILogger<ApiKeyHmacMiddleware>>();
        mockLogger
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[LEAD-020]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => logMessages.Add("[LEAD-020]"));

        var options = new ApiKeyHmacOptions
        {
            HmacSecret = TestSecret,
            ApiKeys = new Dictionary<string, string> { { TestApiKey, TestAgentId } },
            MaxTimestampDriftSeconds = 300
        };

        var body = """{"name":"Jane"}""";
        var ts = NowTimestamp();
        var sig = GenerateHmac(TestSecret, ts, body);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/agents/different-agent/leads";
        httpContext.Request.Headers["X-API-Key"] = TestApiKey; // maps to TestAgentId, not "different-agent"
        httpContext.Request.Headers["X-Timestamp"] = ts;
        httpContext.Request.Headers["X-Signature"] = sig;
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.RouteValues["agentId"] = "different-agent";
        httpContext.Response.Body = new MemoryStream();

        var nextCalled = false;
        var middleware = new ApiKeyHmacMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new OptionsWrapper<ApiKeyHmacOptions>(options),
            mockLogger.Object);

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled.Should().BeFalse();
        logMessages.Should().ContainSingle(m => m.Contains("[LEAD-020]"));
    }

    // Test 5: Missing HMAC signature → 401
    [Fact]
    public async Task MissingSignature_Returns401()
    {
        var client = BuildClient();
        const string body = """{"name":"Jane Doe"}""";
        var ts = NowTimestamp();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/agents/{TestAgentId}/leads")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-API-Key", TestApiKey);
        request.Headers.TryAddWithoutValidation("X-Timestamp", ts);
        // Deliberately omit X-Signature

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Test 6: Invalid HMAC signature → 401 + log [LEAD-021]
    [Fact]
    public async Task InvalidHmacSignature_Returns401_LogsLead021()
    {
        var logMessages = new List<string>();
        var mockLogger = new Mock<ILogger<ApiKeyHmacMiddleware>>();
        mockLogger
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[LEAD-021]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => logMessages.Add("[LEAD-021]"));

        var options = new ApiKeyHmacOptions
        {
            HmacSecret = TestSecret,
            ApiKeys = new Dictionary<string, string> { { TestApiKey, TestAgentId } },
            MaxTimestampDriftSeconds = 300
        };

        var body = """{"name":"Jane"}""";
        var ts = NowTimestamp();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = $"/agents/{TestAgentId}/leads";
        httpContext.Request.Headers["X-API-Key"] = TestApiKey;
        httpContext.Request.Headers["X-Timestamp"] = ts;
        httpContext.Request.Headers["X-Signature"] = "sha256=deadbeefdeadbeefdeadbeefdeadbeef"; // wrong
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.RouteValues["agentId"] = TestAgentId;
        httpContext.Response.Body = new MemoryStream();

        var nextCalled = false;
        var middleware = new ApiKeyHmacMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new OptionsWrapper<ApiKeyHmacOptions>(options),
            mockLogger.Object);

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled.Should().BeFalse();
        logMessages.Should().ContainSingle(m => m.Contains("[LEAD-021]"));
    }

    // Test 7: Timestamp > 5 min old → 401 + log [LEAD-022]
    [Fact]
    public async Task StaleTimestamp_Returns401_LogsLead022()
    {
        var logMessages = new List<string>();
        var mockLogger = new Mock<ILogger<ApiKeyHmacMiddleware>>();
        mockLogger
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[LEAD-022]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => logMessages.Add("[LEAD-022]"));

        var options = new ApiKeyHmacOptions
        {
            HmacSecret = TestSecret,
            ApiKeys = new Dictionary<string, string> { { TestApiKey, TestAgentId } },
            MaxTimestampDriftSeconds = 300
        };

        var body = """{"name":"Jane"}""";
        var staleTs = (DateTimeOffset.UtcNow - TimeSpan.FromMinutes(6)).ToUnixTimeSeconds().ToString();
        var sig = GenerateHmac(TestSecret, staleTs, body);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = $"/agents/{TestAgentId}/leads";
        httpContext.Request.Headers["X-API-Key"] = TestApiKey;
        httpContext.Request.Headers["X-Timestamp"] = staleTs;
        httpContext.Request.Headers["X-Signature"] = sig;
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.RouteValues["agentId"] = TestAgentId;
        httpContext.Response.Body = new MemoryStream();

        var nextCalled = false;
        var middleware = new ApiKeyHmacMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new OptionsWrapper<ApiKeyHmacOptions>(options),
            mockLogger.Object);

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled.Should().BeFalse();
        logMessages.Should().ContainSingle(m => m.Contains("[LEAD-022]"));
    }

    // Test 8: Timestamp in the future by > 5 min → 401
    [Fact]
    public async Task FutureTimestamp_Returns401()
    {
        var options = new ApiKeyHmacOptions
        {
            HmacSecret = TestSecret,
            ApiKeys = new Dictionary<string, string> { { TestApiKey, TestAgentId } },
            MaxTimestampDriftSeconds = 300
        };

        var body = """{"name":"Jane"}""";
        var futureTs = (DateTimeOffset.UtcNow + TimeSpan.FromMinutes(6)).ToUnixTimeSeconds().ToString();
        var sig = GenerateHmac(TestSecret, futureTs, body);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = $"/agents/{TestAgentId}/leads";
        httpContext.Request.Headers["X-API-Key"] = TestApiKey;
        httpContext.Request.Headers["X-Timestamp"] = futureTs;
        httpContext.Request.Headers["X-Signature"] = sig;
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.RouteValues["agentId"] = TestAgentId;
        httpContext.Response.Body = new MemoryStream();

        var mockLogger = new Mock<ILogger<ApiKeyHmacMiddleware>>();
        var middleware = new ApiKeyHmacMiddleware(
            _ => Task.CompletedTask,
            new OptionsWrapper<ApiKeyHmacOptions>(options),
            mockLogger.Object);

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    // Test 9: Body can still be read by endpoint after middleware (buffering works)
    [Fact]
    public async Task ValidRequest_EndpointCanReadBody()
    {
        var client = BuildClient();
        const string body = """{"name":"Buffering Test","email":"test@example.com"}""";

        var response = await client.SendAsync(BuildLeadRequest(TestAgentId, body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Be(body, "the endpoint should receive the full body after middleware reads it");
    }

    // Bonus: Non-lead paths are not intercepted
    [Fact]
    public async Task NonLeadPath_SkipsMiddleware_Returns200()
    {
        var client = BuildClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Bonus: ComputeHmac helper produces deterministic output
    [Fact]
    public void ComputeHmac_ProducesDeterministicSignature()
    {
        var sig1 = ApiKeyHmacMiddleware.ComputeHmac("secret", "1234567890", """{"x":1}""");
        var sig2 = ApiKeyHmacMiddleware.ComputeHmac("secret", "1234567890", """{"x":1}""");

        sig1.Should().Be(sig2);
        sig1.Should().StartWith("sha256=");
        sig1["sha256=".Length..].Should().HaveLength(64, "SHA-256 hex is 64 characters");
    }
}
