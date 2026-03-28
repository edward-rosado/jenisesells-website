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
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using RealEstateStar.Api.Middleware;

namespace RealEstateStar.Api.Tests.Middleware;

public class CorrelationIdMiddlewareTests
{
    private const string HeaderName = "X-Correlation-ID";

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_WhenNoneProvided()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers[HeaderName].ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_PassesThroughExistingCorrelationId()
    {
        var context = new DefaultHttpContext();
        var existingId = "abc123";
        context.Request.Headers[HeaderName] = existingId;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers[HeaderName].ToString().Should().Be(existingId);
    }

    [Fact]
    public async Task InvokeAsync_AddsCorrelationIdToResponseHeaders()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers.Should().ContainKey(HeaderName);
        var id = context.Response.Headers[HeaderName].ToString();
        id.Should().HaveLength(32, "generated GUID with 'N' format is 32 hex characters");
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("valid-id_123", true)]
    [InlineData("abc", true)]
    [InlineData("a", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("has spaces", false)]
    [InlineData("has<html>", false)]
    [InlineData("has;semicolon", false)]
    public void IsValidCorrelationId_ValidatesCorrectly(string? id, bool expected)
    {
        CorrelationIdMiddleware.IsValidCorrelationId(id).Should().Be(expected);
    }

    [Fact]
    public void IsValidCorrelationId_RejectsTooLong()
    {
        var longId = new string('a', 65);
        CorrelationIdMiddleware.IsValidCorrelationId(longId).Should().BeFalse();
    }

    [Fact]
    public void IsValidCorrelationId_AcceptsMaxLength()
    {
        var maxId = new string('a', 64);
        CorrelationIdMiddleware.IsValidCorrelationId(maxId).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_RejectsInvalidCorrelationId_GeneratesNew()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderName] = "invalid<script>alert(1)</script>";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var id = context.Response.Headers[HeaderName].ToString();
        id.Should().HaveLength(32, "should generate a new GUID when input is invalid");
    }
}
