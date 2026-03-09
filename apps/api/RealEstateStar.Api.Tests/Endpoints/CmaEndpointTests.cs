using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Tests.Endpoints;

public class CmaEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CmaEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCma_Returns202_WithJobId()
    {
        var lead = new
        {
            firstName = "John",
            lastName = "Doe",
            email = "john@example.com",
            phone = "555-1234",
            address = "123 Main St",
            city = "Springfield",
            state = "NJ",
            zip = "07081",
            timeline = "3-6 months"
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/cma", lead);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("jobId", out var jobId));
        Assert.False(string.IsNullOrEmpty(jobId.GetString()));
        Assert.Equal("processing", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostCma_Returns400_WhenFirstNameMissing()
    {
        var lead = new
        {
            firstName = "",
            lastName = "Doe",
            email = "john@example.com",
            phone = "555-1234",
            address = "123 Main St",
            city = "Springfield",
            state = "NJ",
            zip = "07081",
            timeline = "3-6 months"
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/cma", lead);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task PostCma_Returns400_WhenEmailInvalid()
    {
        var lead = new
        {
            firstName = "John",
            lastName = "Doe",
            email = "not-an-email",
            phone = "555-1234",
            address = "123 Main St",
            city = "Springfield",
            state = "NJ",
            zip = "07081",
            timeline = "3-6 months"
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/cma", lead);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task PostCma_Returns400_WhenAddressMissing()
    {
        var lead = new
        {
            firstName = "John",
            lastName = "Doe",
            email = "john@example.com",
            phone = "555-1234",
            address = "",
            city = "",
            state = "NJ",
            zip = "07081",
            timeline = "3-6 months"
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/cma", lead);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task PostCma_Returns400_WhenZipInvalid()
    {
        var lead = new
        {
            firstName = "John",
            lastName = "Doe",
            email = "john@example.com",
            phone = "555-1234",
            address = "123 Main St",
            city = "Springfield",
            state = "NJ",
            zip = "ABCDE",
            timeline = "3-6 months"
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/cma", lead);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Zip", out _));
    }

    [Fact]
    public async Task GetCmaStatus_Returns404_ForUnknownJob()
    {
        var response = await _client.GetAsync("/agents/jenise-buckalew/cma/nonexistent-job/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
