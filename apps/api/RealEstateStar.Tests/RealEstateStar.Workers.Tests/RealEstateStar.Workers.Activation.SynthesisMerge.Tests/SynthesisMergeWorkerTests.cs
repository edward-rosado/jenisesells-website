using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.SynthesisMerge.Tests;

public class SynthesisMergeWorkerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Review MakeReview(int rating = 5, string reviewer = "Alice", string text = "Great!") =>
        new(Text: text, Rating: rating, Reviewer: reviewer, Source: "Zillow", Date: null);

    private static SynthesisMergeWorker MakeWorker(
        IAnthropicClient? anthropic = null,
        IContentSanitizer? sanitizer = null,
        ILogger<SynthesisMergeWorker>? logger = null)
    {
        anthropic ??= new Mock<IAnthropicClient>().Object;
        sanitizer ??= new Mock<IContentSanitizer>().Object;
        logger ??= new Mock<ILogger<SynthesisMergeWorker>>().Object;
        return new SynthesisMergeWorker(anthropic, sanitizer, logger);
    }

    // ---------------------------------------------------------------------------
    // DetectContradictions — responsive personality + slow coaching response time
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectContradictions_ResponsivePersonalityAndSlowCoaching_DetectsResponseTimeContradiction()
    {
        var personality = "The agent is highly responsive and known for quick to respond to client inquiries.";
        var coaching = "Analysis shows >24h response gaps between agent and client emails.";

        var result = SynthesisMergeWorker.DetectContradictions(null, personality, coaching);

        result.Should().ContainSingle(c => c.Signal == "Response Time");
        result.Single(c => c.Signal == "Response Time").Source1.Should().Be("Personality");
        result.Single(c => c.Signal == "Response Time").Source2.Should().Be("Coaching");
    }

    // ---------------------------------------------------------------------------
    // DetectContradictions — no contradictions when data is clean
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectContradictions_CleanData_ReturnsEmptyList()
    {
        var personality = "The agent is methodical and detail-oriented.";
        var coaching = "Strong follow-up cadence, well-personalized emails, clear CTAs.";

        var result = SynthesisMergeWorker.DetectContradictions(null, personality, coaching);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // DetectContradictions — null inputs return empty list
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectContradictions_NullPersonality_ReturnsEmpty()
    {
        var result = SynthesisMergeWorker.DetectContradictions(
            voiceSkill: null,
            personalitySkill: null,
            coachingReport: "Some coaching content about slow response.");

        result.Should().BeEmpty();
    }

    [Fact]
    public void DetectContradictions_NullCoaching_ReturnsEmpty()
    {
        var result = SynthesisMergeWorker.DetectContradictions(
            voiceSkill: null,
            personalitySkill: "Agent is responsive and fast response in all communications.",
            coachingReport: null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void DetectContradictions_AllNull_ReturnsEmpty()
    {
        var result = SynthesisMergeWorker.DetectContradictions(null, null, null);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // DetectContradictions — relationship-first + low personalization detected
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectContradictions_RelationshipFirstPersonalityAndLowPersonalization_DetectsPersonalizationContradiction()
    {
        var personality = "This agent is a true relationship builder who values personal connection above all.";
        var coaching = "Emails tend to be generic with low personalization score: 2 out of 10.";

        var result = SynthesisMergeWorker.DetectContradictions(null, personality, coaching);

        result.Should().ContainSingle(c => c.Signal == "Personalization");
        result.Single(c => c.Signal == "Personalization").Source1.Should().Be("Personality");
    }

    // ---------------------------------------------------------------------------
    // DetectContradictions — warm voice + weak CTA detected
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectContradictions_WarmVoiceAndWeakCTA_DetectsCTAContradiction()
    {
        var voice = "The agent uses casual, warm, friendly language in all communications.";
        var personality = "Outgoing and personable.";
        var coaching = "Emails end with vague CTA like 'let me know' instead of specific next steps.";

        var result = SynthesisMergeWorker.DetectContradictions(voice, personality, coaching);

        result.Should().ContainSingle(c => c.Signal == "Call-to-Action Strength");
        result.Single(c => c.Signal == "Call-to-Action Strength").Source1.Should().Be("Voice");
        result.Single(c => c.Signal == "Call-to-Action Strength").Source2.Should().Be("Coaching");
    }

    [Fact]
    public void DetectContradictions_WeakCTAWithoutVoiceSkill_DoesNotDetectCTAContradiction()
    {
        // Without voice skill, the CTA check should not fire
        var personality = "Outgoing and personable.";
        var coaching = "Emails end with vague CTA like 'let me know'.";

        var result = SynthesisMergeWorker.DetectContradictions(
            voiceSkill: null,
            personalitySkill: personality,
            coachingReport: coaching);

        result.Should().NotContain(c => c.Signal == "Call-to-Action Strength");
    }

    // ---------------------------------------------------------------------------
    // DetectContradictions — multiple contradictions at once
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectContradictions_MultipleSignalsFired_ReturnsMultipleContradictions()
    {
        var voice = "The agent writes in a very casual and warm tone.";
        var personality = "responsive, quick to respond, and a relationship-first builder with personal connection.";
        var coaching = "Analysis: >24h response gap observed. Emails are generic with low personalization. CTAs are weak CTA.";

        var result = SynthesisMergeWorker.DetectContradictions(voice, personality, coaching);

        result.Should().HaveCount(3);
        result.Should().Contain(c => c.Signal == "Response Time");
        result.Should().Contain(c => c.Signal == "Personalization");
        result.Should().Contain(c => c.Signal == "Call-to-Action Strength");
    }

    // ---------------------------------------------------------------------------
    // BuildStrengthsSummary — with reviews produces average rating
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildStrengthsSummary_WithReviews_ProducesAverageRating()
    {
        var reviews = new List<Review>
        {
            MakeReview(rating: 5),
            MakeReview(rating: 4),
            MakeReview(rating: 3)
        };

        var result = SynthesisMergeWorker.BuildStrengthsSummary(
            personalitySkill: null,
            voiceSkill: null,
            pipelineMarkdown: null,
            reviews: reviews);

        result.Should().NotBeNull();
        result.Should().Contain("4.0/5");
        result.Should().Contain("3 reviews");
    }

    [Fact]
    public void BuildStrengthsSummary_SingleReview_ShowsExactRating()
    {
        var reviews = new List<Review> { MakeReview(rating: 5) };

        var result = SynthesisMergeWorker.BuildStrengthsSummary(
            personalitySkill: null,
            voiceSkill: null,
            pipelineMarkdown: null,
            reviews: reviews);

        result.Should().NotBeNull();
        result.Should().Contain("5.0/5");
        result.Should().Contain("1 reviews");
    }

    // ---------------------------------------------------------------------------
    // BuildStrengthsSummary — with pipeline markdown extracts total leads
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildStrengthsSummary_WithPipelineMarkdown_ExtractsTotalLeads()
    {
        var pipelineMarkdown = """
            ## Pipeline Summary

            **Total Leads:** 47
            **Active Clients:** 12
            **Under Contract:** 5
            """;

        var result = SynthesisMergeWorker.BuildStrengthsSummary(
            personalitySkill: null,
            voiceSkill: null,
            pipelineMarkdown: pipelineMarkdown,
            reviews: new List<Review>());

        result.Should().NotBeNull();
        result.Should().Contain("47 leads tracked");
    }

    [Fact]
    public void BuildStrengthsSummary_PipelineMarkdownWithoutTotalLeads_OmitsPipelineSection()
    {
        var pipelineMarkdown = "Some pipeline content without the expected format.";

        var result = SynthesisMergeWorker.BuildStrengthsSummary(
            personalitySkill: null,
            voiceSkill: null,
            pipelineMarkdown: pipelineMarkdown,
            reviews: new List<Review>());

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // BuildStrengthsSummary — all null inputs returns null
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildStrengthsSummary_AllNullInputsAndEmptyReviews_ReturnsNull()
    {
        var result = SynthesisMergeWorker.BuildStrengthsSummary(
            personalitySkill: null,
            voiceSkill: null,
            pipelineMarkdown: null,
            reviews: new List<Review>());

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // BuildStrengthsSummary — personality temperament section extracted
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildStrengthsSummary_WithPersonalitySkillTemperament_ExtractsTemperament()
    {
        var personalitySkill = """
            ## Temperament
            Warm, empathetic, and patient with first-time buyers.

            ## Communication Style
            Prefers concise emails with clear next steps.
            """;

        var result = SynthesisMergeWorker.BuildStrengthsSummary(
            personalitySkill: personalitySkill,
            voiceSkill: null,
            pipelineMarkdown: null,
            reviews: new List<Review>());

        result.Should().NotBeNull();
        result.Should().Contain("Temperament");
        result.Should().Contain("Warm, empathetic");
    }

    // ---------------------------------------------------------------------------
    // MergeAsync — skips coaching enrichment when personality is null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_NullPersonalitySkill_SkipsCoachingEnrichmentAndNeverCallsClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<SynthesisMergeWorker>>();

        var worker = new SynthesisMergeWorker(anthropic.Object, sanitizer.Object, logger.Object);

        var result = await worker.MergeAsync(
            voiceSkill: "Voice skill content",
            personalitySkill: null,
            coachingReport: "Coaching report content",
            pipelineJson: null,
            pipelineMarkdown: null,
            reviews: new List<Review>(),
            ct: CancellationToken.None);

        result.EnrichedCoachingReport.Should().BeNull();

        anthropic.Verify(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MergeAsync_NullCoachingReport_SkipsCoachingEnrichmentAndNeverCallsClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<SynthesisMergeWorker>>();

        var worker = new SynthesisMergeWorker(anthropic.Object, sanitizer.Object, logger.Object);

        var result = await worker.MergeAsync(
            voiceSkill: null,
            personalitySkill: "Personality skill content",
            coachingReport: null,
            pipelineJson: null,
            pipelineMarkdown: null,
            reviews: new List<Review>(),
            ct: CancellationToken.None);

        result.EnrichedCoachingReport.Should().BeNull();

        anthropic.Verify(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------------------
    // MergeAsync — calls Claude when both coaching and personality are present
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_BothCoachingAndPersonalityPresent_CallsClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<SynthesisMergeWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse(
                Content: "## Cross-Analysis Insights\nStrong alignment.",
                InputTokens: 200, OutputTokens: 50, DurationMs: 800));

        var worker = new SynthesisMergeWorker(anthropic.Object, sanitizer.Object, logger.Object);

        var result = await worker.MergeAsync(
            voiceSkill: null,
            personalitySkill: "Personality skill content",
            coachingReport: "Original coaching report",
            pipelineJson: null,
            pipelineMarkdown: null,
            reviews: new List<Review>(),
            ct: CancellationToken.None);

        result.EnrichedCoachingReport.Should().NotBeNull();
        result.EnrichedCoachingReport.Should().Contain("Original coaching report");
        result.EnrichedCoachingReport.Should().Contain("## Cross-Analysis Insights");

        anthropic.Verify(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------------
    // MergeAsync — always returns contradictions and strengths regardless of coaching
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_NullPersonality_StillReturnsContradictionsAndStrengths()
    {
        var worker = MakeWorker();

        var reviews = new List<Review> { MakeReview(rating: 5) };

        var result = await worker.MergeAsync(
            voiceSkill: null,
            personalitySkill: null,
            coachingReport: null,
            pipelineJson: null,
            pipelineMarkdown: null,
            reviews: reviews,
            ct: CancellationToken.None);

        // Contradictions: empty (null inputs)
        result.Contradictions.Should().BeEmpty();

        // Strengths: non-null because reviews were provided
        result.StrengthsSummary.Should().NotBeNull();
        result.StrengthsSummary.Should().Contain("5.0/5");
    }
}
