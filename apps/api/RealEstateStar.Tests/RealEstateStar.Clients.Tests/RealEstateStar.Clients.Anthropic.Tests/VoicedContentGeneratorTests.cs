using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Anthropic.Tests;

public class VoicedContentGeneratorTests
{
    // ─── shared test fixtures ──────────────────────────────────────────────────

    private const string TestModel = "claude-haiku-4-5";
    private const string TestPipeline = "site-generation";

    private static SiteFacts BuildFacts(string factsHash = "hash-abc") =>
        new(
            Agent: new AgentIdentity("Jane Smith", "Jane A. Smith", "REALTOR®",
                "jane@example.com", "555-0100", "NJ12345", 10,
                ["en", "es"]),
            Brokerage: new BrokerageIdentity("SmithRealty", null, null, null, null, null),
            Location: new LocationFacts("NJ", ["Montclair", "Bloomfield"], new Dictionary<string, int>()),
            Specialties: new SpecialtiesFacts(["First-Time Buyers"], ["warm"], new Dictionary<string, int>()),
            Trust: new TrustSignals(42, 4.9m, 120, TimeSpan.FromHours(1), 450_000m),
            RecentSales: [],
            Testimonials: [],
            Credentials: [],
            Stages: new PipelineStages([], new Dictionary<string, int>()),
            VoicesByLocale: new Dictionary<string, LocaleVoice>())
        {
            FactsHash = factsHash
        };

    private static LocaleVoice BuildVoice(string voiceHash = "voice-xyz") =>
        new("en", "Be warm and direct.", "Friendly and professional.", voiceHash);

    private static FieldSpec<string> BuildFieldSpec(
        string name = "hero_headline",
        string model = TestModel,
        string fallback = "Your Trusted REALTOR®",
        Func<string, bool>? validator = null) =>
        new FieldSpec<string>(name, "Write a compelling headline.", 200, model, fallback)
        {
            Validator = validator
        };

    private static VoicedRequest<string> BuildRequest(
        SiteFacts? facts = null,
        LocaleVoice? voice = null,
        FieldSpec<string>? field = null) =>
        new(
            Facts: facts ?? BuildFacts(),
            Locale: "en",
            Voice: voice ?? BuildVoice(),
            Field: field ?? BuildFieldSpec(),
            PipelineStep: TestPipeline);

    private static AnthropicResponse BuildClaudeResponse(
        string content = "Welcome to SmithRealty",
        int inputTokens = 100,
        int outputTokens = 50,
        double durationMs = 123.0) =>
        new(content, inputTokens, outputTokens, durationMs);

    // ─── cache hit ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_CacheHit_ReturnsValueWithZeroMetrics()
    {
        var cache = new FakeDistributedContentCache();
        var request = BuildRequest();
        var expectedKey = VoicedContentGenerator.BuildCacheKey(request);

        // Pre-populate cache with a string value
        await cache.SetAsync(expectedKey, "Cached Headline!", TimeSpan.FromHours(1), CancellationToken.None);

        var claudeMock = new Mock<IAnthropicClient>();
        var sut = new VoicedContentGenerator(claudeMock.Object, cache,
            NullLogger<VoicedContentGenerator>.Instance);

        var result = await sut.GenerateAsync(request, CancellationToken.None);

        result.Value.Should().Be("Cached Headline!");
        result.IsFallback.Should().BeFalse();
        result.FailureReason.Should().BeNull();
        result.Metrics.InputTokens.Should().Be(0);
        result.Metrics.OutputTokens.Should().Be(0);
        result.Metrics.EstimatedCostUsd.Should().Be(0);
        result.Metrics.DurationMs.Should().Be(0);

        // Claude must NOT have been called
        claudeMock.VerifyNoOtherCalls();
    }

    // ─── cache miss → successful generation ───────────────────────────────────

    [Fact]
    public async Task GenerateAsync_CacheMiss_CallsClaudeAndPopulatesCache()
    {
        var cache = new FakeDistributedContentCache();
        var request = BuildRequest();
        var claudeResponse = BuildClaudeResponse("Welcome, Jane's clients!");

        var claudeMock = new Mock<IAnthropicClient>();
        claudeMock
            .Setup(c => c.SendAsync(
                request.Field.Model,
                It.IsAny<string>(),   // system prompt
                request.Field.PromptTemplate,
                request.Field.MaxOutputTokens,
                request.PipelineStep,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(claudeResponse);

        var sut = new VoicedContentGenerator(claudeMock.Object, cache,
            NullLogger<VoicedContentGenerator>.Instance);

        var result = await sut.GenerateAsync(request, CancellationToken.None);

        result.Value.Should().Be("Welcome, Jane's clients!");
        result.IsFallback.Should().BeFalse();
        result.FailureReason.Should().BeNull();
        result.Metrics.InputTokens.Should().Be(100);
        result.Metrics.OutputTokens.Should().Be(50);
        result.Metrics.EstimatedCostUsd.Should().BeGreaterThan(0);
        result.Metrics.DurationMs.Should().BeGreaterThanOrEqualTo(0);

        // Cache should now contain the generated value
        var cacheKey = VoicedContentGenerator.BuildCacheKey(request);
        var cached = await cache.GetAsync<string>(cacheKey, CancellationToken.None);
        cached.Should().Be("Welcome, Jane's clients!");
    }

    // ─── validation failure → fallback ────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_ValidationFails_ReturnsFallback()
    {
        var cache = new FakeDistributedContentCache();
        var field = BuildFieldSpec(
            fallback: "Default Headline",
            validator: v => v.Length >= 100); // impossible constraint → always fails

        var request = BuildRequest(field: field);
        var claudeResponse = BuildClaudeResponse("Short");

        var claudeMock = new Mock<IAnthropicClient>();
        claudeMock
            .Setup(c => c.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(claudeResponse);

        var sut = new VoicedContentGenerator(claudeMock.Object, cache,
            NullLogger<VoicedContentGenerator>.Instance);

        var result = await sut.GenerateAsync(request, CancellationToken.None);

        result.Value.Should().Be("Default Headline");
        result.IsFallback.Should().BeTrue();
        result.FailureReason.Should().Be("Validation failed");

        // Cache should be empty — failed validation must not be cached
        cache.Count.Should().Be(0);
    }

    // ─── Claude error → fallback ──────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_ClaudeThrows_ReturnsFallbackWithZeroMetrics()
    {
        var cache = new FakeDistributedContentCache();
        var request = BuildRequest(field: BuildFieldSpec(fallback: "Fallback Headline"));

        var claudeMock = new Mock<IAnthropicClient>();
        claudeMock
            .Setup(c => c.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Anthropic API returned 500"));

        var sut = new VoicedContentGenerator(claudeMock.Object, cache,
            NullLogger<VoicedContentGenerator>.Instance);

        var result = await sut.GenerateAsync(request, CancellationToken.None);

        result.Value.Should().Be("Fallback Headline");
        result.IsFallback.Should().BeTrue();
        result.FailureReason.Should().Contain("500");
        result.Metrics.InputTokens.Should().Be(0);
        result.Metrics.OutputTokens.Should().Be(0);
        result.Metrics.EstimatedCostUsd.Should().Be(0);
        result.Metrics.DurationMs.Should().Be(0);

        // Cache must be empty
        cache.Count.Should().Be(0);
    }

    // ─── cache key determinism ────────────────────────────────────────────────

    [Fact]
    public void BuildCacheKey_SameInputs_ProducesSameKey()
    {
        var facts = BuildFacts("hash-111");
        var voice = BuildVoice("voice-222");
        var field = BuildFieldSpec("tagline");

        var r1 = new VoicedRequest<string>(facts, "en", voice, field, TestPipeline);
        var r2 = new VoicedRequest<string>(facts, "en", voice, field, TestPipeline);

        var key1 = VoicedContentGenerator.BuildCacheKey(r1);
        var key2 = VoicedContentGenerator.BuildCacheKey(r2);

        key1.Should().Be(key2);
    }

    [Fact]
    public void BuildCacheKey_DifferentFactsHash_ProducesDifferentKey()
    {
        var voice = BuildVoice();
        var field = BuildFieldSpec();

        var r1 = new VoicedRequest<string>(BuildFacts("hash-A"), "en", voice, field, TestPipeline);
        var r2 = new VoicedRequest<string>(BuildFacts("hash-B"), "en", voice, field, TestPipeline);

        VoicedContentGenerator.BuildCacheKey(r1)
            .Should().NotBe(VoicedContentGenerator.BuildCacheKey(r2));
    }

    [Fact]
    public void BuildCacheKey_DifferentLocale_ProducesDifferentKey()
    {
        var facts = BuildFacts();
        var voice = BuildVoice();
        var field = BuildFieldSpec();

        var r1 = new VoicedRequest<string>(facts, "en", voice, field, TestPipeline);
        var r2 = new VoicedRequest<string>(facts, "es", voice, field, TestPipeline);

        VoicedContentGenerator.BuildCacheKey(r1)
            .Should().NotBe(VoicedContentGenerator.BuildCacheKey(r2));
    }

    [Fact]
    public void BuildCacheKey_DifferentVoiceHash_ProducesDifferentKey()
    {
        var facts = BuildFacts();
        var field = BuildFieldSpec();

        var r1 = new VoicedRequest<string>(facts, "en", BuildVoice("voice-A"), field, TestPipeline);
        var r2 = new VoicedRequest<string>(facts, "en", BuildVoice("voice-B"), field, TestPipeline);

        VoicedContentGenerator.BuildCacheKey(r1)
            .Should().NotBe(VoicedContentGenerator.BuildCacheKey(r2));
    }

    [Fact]
    public void BuildCacheKey_DifferentFieldName_ProducesDifferentKey()
    {
        var facts = BuildFacts();
        var voice = BuildVoice();

        var r1 = new VoicedRequest<string>(facts, "en", voice, BuildFieldSpec("tagline"), TestPipeline);
        var r2 = new VoicedRequest<string>(facts, "en", voice, BuildFieldSpec("bio"), TestPipeline);

        VoicedContentGenerator.BuildCacheKey(r1)
            .Should().NotBe(VoicedContentGenerator.BuildCacheKey(r2));
    }

    // ─── system prompt correctness ─────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_SystemPromptContainsAgentNameAndVoice()
    {
        var cache = new FakeDistributedContentCache();
        var request = BuildRequest();

        string? capturedSystemPrompt = null;

        var claudeMock = new Mock<IAnthropicClient>();
        claudeMock
            .Setup(c => c.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, systemPrompt, _, _, _, _) => capturedSystemPrompt = systemPrompt)
            .ReturnsAsync(BuildClaudeResponse());

        var sut = new VoicedContentGenerator(claudeMock.Object, cache,
            NullLogger<VoicedContentGenerator>.Instance);

        await sut.GenerateAsync(request, CancellationToken.None);

        capturedSystemPrompt.Should().Contain("Jane Smith");
        capturedSystemPrompt.Should().Contain("Be warm and direct.");   // VoiceSkillMarkdown
        capturedSystemPrompt.Should().Contain("Friendly and professional."); // PersonalitySkillMarkdown
        capturedSystemPrompt.Should().Contain("en");
    }

    // ─── cost estimation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("claude-haiku-4-5", 1_000_000, 1_000_000, 0.80 + 4.0)]
    [InlineData("claude-sonnet-4-5", 1_000_000, 1_000_000, 3.0 + 15.0)]
    [InlineData("claude-opus-4-5", 1_000_000, 1_000_000, 15.0 + 75.0)]
    public async Task GenerateAsync_EstimatedCostMatchesModel(
        string model, int inputTokens, int outputTokens, double expectedCostUsd)
    {
        var cache = new FakeDistributedContentCache();
        var field = BuildFieldSpec(model: model);
        var request = BuildRequest(field: field);

        var claudeMock = new Mock<IAnthropicClient>();
        claudeMock
            .Setup(c => c.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("Generated", inputTokens, outputTokens, 50.0));

        var sut = new VoicedContentGenerator(claudeMock.Object, cache,
            NullLogger<VoicedContentGenerator>.Instance);

        var result = await sut.GenerateAsync(request, CancellationToken.None);

        result.Metrics.EstimatedCostUsd.Should().BeApproximately(expectedCostUsd, precision: 0.001);
    }

    // ─── second call hits cache ───────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_SecondCall_HitsCacheAndDoesNotCallClaude()
    {
        var cache = new FakeDistributedContentCache();
        var request = BuildRequest();

        var claudeMock = new Mock<IAnthropicClient>();
        claudeMock
            .Setup(c => c.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildClaudeResponse("First Generation"));

        var sut = new VoicedContentGenerator(claudeMock.Object, cache,
            NullLogger<VoicedContentGenerator>.Instance);

        var first = await sut.GenerateAsync(request, CancellationToken.None);
        var second = await sut.GenerateAsync(request, CancellationToken.None);

        first.IsFallback.Should().BeFalse();
        second.IsFallback.Should().BeFalse();
        second.Value.Should().Be(first.Value);
        second.Metrics.InputTokens.Should().Be(0, "second call should be a cache hit with zero tokens");

        // Claude should have been called only once
        claudeMock.Verify(c => c.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
