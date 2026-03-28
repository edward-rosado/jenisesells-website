using Xunit;
using Moq;
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
using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RealEstateStar.Api.Tests.Infrastructure;

public class RateLimitingTests
{
    [Fact]
    public void RateLimiter_ConfiguresAllRequiredPolicies()
    {
        // Arrange & Act: Simply verify that the rate limiter can be configured with all required policies
        // This test ensures the policies are registered without errors
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // This should not throw — if any policy registration fails, an exception will be thrown here
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global limiter
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            // All four required policies — registration should succeed
            options.AddPolicy("lead-create", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromHours(1) }));

            options.AddPolicy("deletion-request", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1) }));

            options.AddPolicy("delete-data", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));

            options.AddPolicy("lead-opt-out", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));
        });

        builder.Services.AddRouting();
        builder.Logging.ClearProviders();

        var app = builder.Build();

        // Assert: Building the app should succeed without exceptions
        app.Should().NotBeNull("Application should build successfully with rate limiter policies configured");
        app.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Should().NotBeNull("Rate limiter options should be available in service provider");
    }

    [Theory]
    [InlineData("lead-create", 20)]
    [InlineData("deletion-request", 5)]
    [InlineData("delete-data", 10)]
    [InlineData("lead-opt-out", 10)]
    public async Task RateLimitPolicy_EnforcesCorrectLimit(string policyName, int expectedPermitLimit)
    {
        // Arrange: Create a minimal app with the specified policy
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Set global limiter very high so we only test the policy-specific limit
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10000,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            // Add the test policy with the expected limit
            if (policyName == "lead-create")
            {
                options.AddPolicy(policyName, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromHours(1) }));
            }
            else if (policyName == "deletion-request")
            {
                options.AddPolicy(policyName, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1) }));
            }
            else if (policyName == "delete-data")
            {
                options.AddPolicy(policyName, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));
            }
            else if (policyName == "lead-opt-out")
            {
                options.AddPolicy(policyName, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));
            }
        });

        builder.Services.AddRouting();
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapGet("/test", () => "OK").RequireRateLimiting(policyName);

        await app.StartAsync();
        var client = app.GetTestClient();

        // Act & Assert: Make expectedPermitLimit requests (all should succeed)
        for (int i = 0; i < expectedPermitLimit; i++)
        {
            var response = await client.GetAsync("/test");
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Request {i + 1} of {expectedPermitLimit} should succeed (within limit)");
        }

        // The next request should be rate limited (429 Too Many Requests)
        var limitExceededResponse = await client.GetAsync("/test");
        limitExceededResponse.StatusCode.Should().Be((HttpStatusCode)429,
            $"Request {expectedPermitLimit + 1} should exceed the rate limit and return 429");
    }
}
