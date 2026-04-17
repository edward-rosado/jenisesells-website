using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Functions.Activation;
using RealEstateStar.Functions.Activation.Activities;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Tests.Activation;

/// <summary>
/// Tests for <see cref="PersistSiteContentFunction"/>.
/// ICloudflareKvClient is mocked — no real network calls.
/// </summary>
public sealed class PersistSiteContentFunctionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string TestNamespaceId = "ns-test-abc123";

    private static (PersistSiteContentFunction Fn, Mock<ICloudflareKvClient> KvMock, Mock<IAccountConfigService> ConfigMock)
        BuildFn(string namespaceId = TestNamespaceId)
    {
        var kvMock = new Mock<ICloudflareKvClient>(MockBehavior.Strict);
        var configMock = new Mock<IAccountConfigService>(MockBehavior.Strict);
        var options = Options.Create(new SiteContentOptions { KvNamespaceId = namespaceId });
        var fn = new PersistSiteContentFunction(
            kvMock.Object,
            configMock.Object,
            options,
            NullLogger<PersistSiteContentFunction>.Instance);
        return (fn, kvMock, configMock);
    }

    private static PersistSiteContentInput BuildInput(
        string accountId = "acc1",
        string agentId = "agent1",
        IReadOnlyDictionary<string, object>? contentByLocale = null,
        string accountConfigJson = "{}")
    {
        contentByLocale ??= new Dictionary<string, object>
        {
            ["en"] = new Dictionary<string, string> { ["hero.headline"] = "Buy Your Dream Home" }
        };

        return new PersistSiteContentInput
        {
            AccountId = accountId,
            AgentId = agentId,
            CorrelationId = "corr-001",
            BuildResult = new BuildResult(BuildResultType.Full, contentByLocale, null),
            AccountConfigJson = accountConfigJson
        };
    }

    // ── Happy path ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SuccessfulPersist_WritesAllLocalesAsDraft()
    {
        // Arrange
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput(contentByLocale: new Dictionary<string, object>
        {
            ["en"] = new Dictionary<string, string> { ["hero.headline"] = "Buy Your Dream Home" },
            ["es"] = new Dictionary<string, string> { ["hero.headline"] = "Compra Tu Casa" }
        });

        var capturedKeys = new List<string>();
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.IsAny<string>(), It.IsAny<string>(), Ct))
              .Callback<string, string, string, CancellationToken>((_, key, _, _) => capturedKeys.Add(key))
              .Returns(Task.CompletedTask);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert — both locale draft keys were written
        capturedKeys.Should().Contain("content:v1:acc1:en:draft");
        capturedKeys.Should().Contain("content:v1:acc1:es:draft");
    }

    [Fact]
    public async Task RunAsync_SuccessfulPersist_KvKeyFormatIsVersioned()
    {
        // Arrange — verify key format uses v1 prefix
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput();

        string? capturedKey = null;
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.IsAny<string>(), It.IsAny<string>(), Ct))
              .Callback<string, string, string, CancellationToken>((_, key, _, _) => capturedKey ??= key)
              .Returns(Task.CompletedTask);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert — key includes version prefix
        capturedKey.Should().StartWith("content:v1:");
        capturedKey.Should().Be("content:v1:acc1:en:draft");
    }

    [Fact]
    public async Task RunAsync_SuccessfulPersist_WritesSiteStateAsPendingApproval()
    {
        // Arrange
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput();

        string? capturedSiteState = null;
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.IsAny<string>(), It.IsAny<string>(), Ct))
              .Callback<string, string, string, CancellationToken>((_, key, value, _) =>
              {
                  if (key.StartsWith("site-state:"))
                      capturedSiteState = value;
              })
              .Returns(Task.CompletedTask);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert
        capturedSiteState.Should().Be("\"pending_approval\"");
    }

    [Fact]
    public async Task RunAsync_SuccessfulPersist_WritesAccountConfigToKv()
    {
        // Arrange
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var accountJson = @"{""name"":""Jenise"",""handle"":""jenise""}";
        var input = BuildInput(accountConfigJson: accountJson);

        string? capturedAccountValue = null;
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.IsAny<string>(), It.IsAny<string>(), Ct))
              .Callback<string, string, string, CancellationToken>((_, key, value, _) =>
              {
                  if (key.StartsWith("account:v1:"))
                      capturedAccountValue = value;
              })
              .Returns(Task.CompletedTask);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert — account.json is written verbatim
        capturedAccountValue.Should().Be(accountJson);
    }

    [Fact]
    public async Task RunAsync_SuccessfulPersist_AccountKeyUsesVersionedFormat()
    {
        // Arrange
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput(accountId: "acc-xyz");

        string? capturedAccountKey = null;
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.IsAny<string>(), It.IsAny<string>(), Ct))
              .Callback<string, string, string, CancellationToken>((_, key, _, _) =>
              {
                  if (key.StartsWith("account:"))
                      capturedAccountKey = key;
              })
              .Returns(Task.CompletedTask);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert
        capturedAccountKey.Should().Be("account:v1:acc-xyz");
    }

    [Fact]
    public async Task RunAsync_SuccessfulPersist_SiteStateKeyUsesVersionedFormat()
    {
        // Arrange
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput(accountId: "acc-xyz");

        string? capturedStateKey = null;
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.IsAny<string>(), It.IsAny<string>(), Ct))
              .Callback<string, string, string, CancellationToken>((_, key, _, _) =>
              {
                  if (key.StartsWith("site-state:"))
                      capturedStateKey = key;
              })
              .Returns(Task.CompletedTask);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert
        capturedStateKey.Should().Be("site-state:v1:acc-xyz");
    }

    // ── Failure propagation ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_KvWriteFails_PropagatesException()
    {
        // Arrange — KV client throws on locale write
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput();

        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.IsAny<string>(), It.IsAny<string>(), Ct))
              .ThrowsAsync(new HttpRequestException("KV service unavailable"));

        // Act & Assert — exception must propagate so DF retries
        await Assert.ThrowsAsync<HttpRequestException>(() => fn.RunAsync(input, Ct));
    }

    [Fact]
    public async Task RunAsync_KvWriteFailsOnAccountKey_PropagatesException()
    {
        // Arrange — locale write succeeds, account write throws
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput();

        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.Is<string>(k => k.StartsWith("content:")), It.IsAny<string>(), Ct))
              .Returns(Task.CompletedTask);
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.Is<string>(k => k.StartsWith("account:")), It.IsAny<string>(), Ct))
              .ThrowsAsync(new HttpRequestException("KV service unavailable"));

        await Assert.ThrowsAsync<HttpRequestException>(() => fn.RunAsync(input, Ct));
    }

    [Fact]
    public async Task RunAsync_KvWriteFailsOnSiteState_PropagatesException()
    {
        // Arrange — locale + account writes succeed, site-state write throws
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput();

        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.Is<string>(k => k.StartsWith("content:")), It.IsAny<string>(), Ct))
              .Returns(Task.CompletedTask);
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.Is<string>(k => k.StartsWith("account:")), It.IsAny<string>(), Ct))
              .Returns(Task.CompletedTask);
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.Is<string>(k => k.StartsWith("site-state:")), It.IsAny<string>(), Ct))
              .ThrowsAsync(new HttpRequestException("KV service unavailable"));

        await Assert.ThrowsAsync<HttpRequestException>(() => fn.RunAsync(input, Ct));
    }

    // ── Edge cases ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_EmptyContentByLocale_StillWritesSiteStateAndAccount()
    {
        // Arrange — no locale content (e.g. Fallback result with empty dict)
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput(contentByLocale: new Dictionary<string, object>());

        var capturedKeys = new List<string>();
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.IsAny<string>(), It.IsAny<string>(), Ct))
              .Callback<string, string, string, CancellationToken>((_, key, _, _) => capturedKeys.Add(key))
              .Returns(Task.CompletedTask);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert — no locale draft writes, but account + site-state must still be written
        capturedKeys.Should().NotContain(k => k.StartsWith("content:"));
        capturedKeys.Should().Contain("account:v1:acc1");
        capturedKeys.Should().Contain("site-state:v1:acc1");
    }

    [Fact]
    public async Task RunAsync_MultipleLocales_WritesCorrectKeyPerLocale()
    {
        // Arrange
        var (fn, kvMock, configMock) = BuildFn();
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput(
            accountId: "acct-99",
            contentByLocale: new Dictionary<string, object>
            {
                ["en"] = new Dictionary<string, string> { ["title"] = "English" },
                ["es"] = new Dictionary<string, string> { ["title"] = "Español" },
                ["pt"] = new Dictionary<string, string> { ["title"] = "Português" }
            });

        var capturedKeys = new List<string>();
        kvMock.Setup(k => k.PutAsync(TestNamespaceId, It.IsAny<string>(), It.IsAny<string>(), Ct))
              .Callback<string, string, string, CancellationToken>((_, key, _, _) => capturedKeys.Add(key))
              .Returns(Task.CompletedTask);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert — one draft key per locale
        capturedKeys.Should().Contain("content:v1:acct-99:en:draft");
        capturedKeys.Should().Contain("content:v1:acct-99:es:draft");
        capturedKeys.Should().Contain("content:v1:acct-99:pt:draft");
    }

    [Fact]
    public async Task RunAsync_UsesNamespaceIdFromOptions()
    {
        // Arrange — custom namespace
        var (fn, kvMock, configMock) = BuildFn(namespaceId: "custom-ns-456");
        configMock.Setup(c => c.GetAccountAsync(It.IsAny<string>(), Ct)).ReturnsAsync((AccountConfig?)null);
        var input = BuildInput();

        string? capturedNamespace = null;
        kvMock.Setup(k => k.PutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), Ct))
              .Callback<string, string, string, CancellationToken>((ns, _, _, _) => capturedNamespace ??= ns)
              .Returns(Task.CompletedTask);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert — all writes use the configured namespace ID, never hardcoded
        capturedNamespace.Should().Be("custom-ns-456");
    }

    // ── ActivityNames constant ──────────────────────────────────────────────────

    [Fact]
    public void ActivityNames_PersistSiteContent_IsDefinedAndFollowsConvention()
    {
        ActivityNames.PersistSiteContent.Should().Be("ActivationPersistSiteContent");
        ActivityNames.PersistSiteContent.Should().StartWith("Activation");
    }
}
