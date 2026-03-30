using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Features.OAuth.GenerateLink;
using RealEstateStar.Api.Features.OAuth.Services;

namespace RealEstateStar.Api.Tests.Features.OAuth.GenerateLink;

public class GenerateAuthLinkEndpointTests
{
    private static AuthorizationLinkService CreateService() =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OAuthLink:Secret"] = "test-secret-32-bytes-long-enough!",
                    ["OAuthLink:ExpirationHours"] = "24",
                    ["Api:BaseUrl"] = "https://api.real-estate-star.com",
                })
                .Build(),
            NullLogger<AuthorizationLinkService>.Instance);

    [Fact]
    public async Task Handle_ValidRequest_Returns200WithAuthUrl()
    {
        var svc = CreateService();
        var request = new GenerateAuthLinkRequest("acct-1", "agent-1", "agent@example.com");
        var logger = NullLogger<GenerateAuthLinkEndpoint>.Instance;

        var result = await GenerateAuthLinkEndpoint.Handle(request, svc, logger, CancellationToken.None);

        var response = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<GenerateAuthLinkResponse>>(result);
        Assert.NotNull(response.Value);
        Assert.Contains("accountId=acct-1", response.Value.AuthorizationUrl);
        Assert.Contains("agentId=agent-1", response.Value.AuthorizationUrl);
        Assert.True(response.Value.ExpiresIn > 0);
        Assert.True(response.Value.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_MissingAccountId_Returns400()
    {
        var svc = CreateService();
        var request = new GenerateAuthLinkRequest("", "agent-1", "agent@example.com");
        var logger = NullLogger<GenerateAuthLinkEndpoint>.Instance;

        var result = await GenerateAuthLinkEndpoint.Handle(request, svc, logger, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    [Fact]
    public async Task Handle_MissingAgentId_Returns400()
    {
        var svc = CreateService();
        var request = new GenerateAuthLinkRequest("acct-1", "", "agent@example.com");
        var logger = NullLogger<GenerateAuthLinkEndpoint>.Instance;

        var result = await GenerateAuthLinkEndpoint.Handle(request, svc, logger, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    [Fact]
    public async Task Handle_MissingEmail_Returns400()
    {
        var svc = CreateService();
        var request = new GenerateAuthLinkRequest("acct-1", "agent-1", "");
        var logger = NullLogger<GenerateAuthLinkEndpoint>.Instance;

        var result = await GenerateAuthLinkEndpoint.Handle(request, svc, logger, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    [Fact]
    public async Task Handle_InvalidEmailFormat_Returns400()
    {
        var svc = CreateService();
        var request = new GenerateAuthLinkRequest("acct-1", "agent-1", "not-an-email");
        var logger = NullLogger<GenerateAuthLinkEndpoint>.Instance;

        var result = await GenerateAuthLinkEndpoint.Handle(request, svc, logger, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    [Fact]
    public async Task Handle_ValidRequest_ExpiresInMatchesExpiresAt()
    {
        var svc = CreateService();
        var request = new GenerateAuthLinkRequest("acct-1", "agent-1", "agent@example.com");
        var logger = NullLogger<GenerateAuthLinkEndpoint>.Instance;

        var before = DateTimeOffset.UtcNow;
        var result = await GenerateAuthLinkEndpoint.Handle(request, svc, logger, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        var response = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<GenerateAuthLinkResponse>>(result);
        var value = response.Value!;

        // ExpiresIn should be approximately the seconds between now and ExpiresAt
        var expectedExpiresIn = (long)(value.ExpiresAt - before).TotalSeconds;
        Assert.True(value.ExpiresIn > 0);
        Assert.True(value.ExpiresIn <= expectedExpiresIn + 5); // small tolerance for test timing
        Assert.True(value.ExpiresAt > after);
    }
}
