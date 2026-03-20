using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Common;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.PollDriveChanges;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Gws;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Leads.PollDriveChanges;

public class PollDriveChangesEndpointTests
{
    private readonly Mock<DriveChangeMonitor> _monitor;
    private readonly Mock<IAgentConfigService> _agentConfigService;

    public PollDriveChangesEndpointTests()
    {
        // DriveChangeMonitor has a primary constructor with deps — pass them to Moq
        var gwsMock = new Mock<IGwsService>();
        var leadStoreMock = new Mock<ILeadStore>();
        _monitor = new Mock<DriveChangeMonitor>(
            gwsMock.Object,
            leadStoreMock.Object,
            NullLogger<DriveChangeMonitor>.Instance);
        _agentConfigService = new Mock<IAgentConfigService>(MockBehavior.Strict);
    }

    private static IConfiguration BuildConfig(string? token = "secret-token") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalApiToken"] = token,
            })
            .Build();

    private static DefaultHttpContext BuildHttpContext(string? bearerToken = "secret-token")
    {
        var ctx = new DefaultHttpContext();
        if (bearerToken is not null)
            ctx.Request.Headers.Authorization = $"Bearer {bearerToken}";
        return ctx;
    }

    private static AgentConfig MakeAgent(string id = "agent-a", string email = "agent@example.com") =>
        new()
        {
            Id = id,
            Identity = new AgentIdentity { Name = "Agent Name", Phone = "5551234567", Email = email },
        };

    // Test 1: Iterates all agents and polls each

    [Fact]
    public async Task Handle_IteratesAllAgentsAndPollsEach()
    {
        var agentA = MakeAgent("agent-a", "a@example.com");
        var agentB = MakeAgent("agent-b", "b@example.com");

        _agentConfigService
            .Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([agentA, agentB]);

        _monitor
            .Setup(m => m.PollAsync("agent-a", "a@example.com", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveChangeResult(3, 1, 0, []));
        _monitor
            .Setup(m => m.PollAsync("agent-b", "b@example.com", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveChangeResult(2, 0, 0, []));

        var result = await PollDriveChangesEndpoint.Handle(
            BuildHttpContext(),
            _monitor.Object,
            _agentConfigService.Object,
            BuildConfig(),
            NullLogger<PollDriveChangesEndpoint>.Instance,
            CancellationToken.None);

        _monitor.Verify(m => m.PollAsync("agent-a", "a@example.com", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _monitor.Verify(m => m.PollAsync("agent-b", "b@example.com", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        result.Should().NotBeNull();
    }

    // Test 2: Individual agent failure logged, continues others

    [Fact]
    public async Task Handle_AgentFailure_LogsAndContinuesToNextAgent()
    {
        var agentA = MakeAgent("agent-a", "a@example.com");
        var agentB = MakeAgent("agent-b", "b@example.com");

        _agentConfigService
            .Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([agentA, agentB]);

        _monitor
            .Setup(m => m.PollAsync("agent-a", "a@example.com", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Drive API unavailable"));
        _monitor
            .Setup(m => m.PollAsync("agent-b", "b@example.com", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveChangeResult(5, 2, 0, []));

        var result = await PollDriveChangesEndpoint.Handle(
            BuildHttpContext(),
            _monitor.Object,
            _agentConfigService.Object,
            BuildConfig(),
            NullLogger<PollDriveChangesEndpoint>.Instance,
            CancellationToken.None);

        // agent-b should still be polled despite agent-a failing
        _monitor.Verify(m => m.PollAsync("agent-b", "b@example.com", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);

        var okResult = result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<DriveChangeResult>>().Subject;
        okResult.Value.Should().NotBeNull();
        // 1 error counted for agent-a failure + 0 from agent-b = 1
        okResult.Value!.Errors.Should().Be(1);
        // 5 processed from agent-b
        okResult.Value.Processed.Should().Be(5);
    }

    // Test 3: Returns aggregate DriveChangeResult

    [Fact]
    public async Task Handle_ReturnsAggregateResult()
    {
        var agentA = MakeAgent("agent-a", "a@example.com");
        var agentB = MakeAgent("agent-b", "b@example.com");

        _agentConfigService
            .Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([agentA, agentB]);

        _monitor
            .Setup(m => m.PollAsync("agent-a", "a@example.com", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveChangeResult(4, 2, 0, []));
        _monitor
            .Setup(m => m.PollAsync("agent-b", "b@example.com", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveChangeResult(6, 3, 1, ["error detail"]));

        var result = await PollDriveChangesEndpoint.Handle(
            BuildHttpContext(),
            _monitor.Object,
            _agentConfigService.Object,
            BuildConfig(),
            NullLogger<PollDriveChangesEndpoint>.Instance,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<DriveChangeResult>>().Subject;
        okResult.Value.Should().NotBeNull();
        okResult.Value!.Processed.Should().Be(10);
        okResult.Value.StatusUpdated.Should().Be(5);
        okResult.Value.Errors.Should().Be(1);
        okResult.Value.ErrorDetails.Should().ContainSingle("error detail");
    }

    // Test 4: Missing or invalid auth token returns 401

    [Fact]
    public async Task Handle_MissingAuthToken_Returns401()
    {
        var ctx = new DefaultHttpContext();

        var result = await PollDriveChangesEndpoint.Handle(
            ctx,
            _monitor.Object,
            _agentConfigService.Object,
            BuildConfig("secret-token"),
            NullLogger<PollDriveChangesEndpoint>.Instance,
            CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult>();
        _agentConfigService.Verify(s => s.ListAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidAuthToken_Returns401()
    {
        var result = await PollDriveChangesEndpoint.Handle(
            BuildHttpContext("wrong-token"),
            _monitor.Object,
            _agentConfigService.Object,
            BuildConfig("secret-token"),
            NullLogger<PollDriveChangesEndpoint>.Instance,
            CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult>();
        _agentConfigService.Verify(s => s.ListAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NonBearerAuthScheme_Returns401()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";

        var result = await PollDriveChangesEndpoint.Handle(
            ctx,
            _monitor.Object,
            _agentConfigService.Object,
            BuildConfig("secret-token"),
            NullLogger<PollDriveChangesEndpoint>.Instance,
            CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult>();
    }
}
