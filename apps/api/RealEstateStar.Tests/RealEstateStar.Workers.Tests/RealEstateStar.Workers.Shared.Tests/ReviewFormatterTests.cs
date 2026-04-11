using FluentAssertions;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Workers.Shared.Tests;

public class ReviewFormatterTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Review MakeReview(
        string text = "Great agent!",
        int rating = 5,
        string reviewer = "Alice",
        string source = "Zillow",
        DateTime? date = null)
        => new(Text: text, Rating: rating, Reviewer: reviewer, Source: source, Date: date);

    private static ThirdPartyProfile MakeProfileWithReviews(params Review[] reviews)
        => new(
            Platform: "Zillow",
            Bio: null,
            Reviews: reviews,
            SalesCount: null,
            ActiveListingCount: null,
            YearsExperience: null,
            Specialties: new List<string>(),
            ServiceAreas: new List<string>(),
            RecentSales: new List<ListingInfo>(),
            ActiveListings: new List<ListingInfo>());

    // ---------------------------------------------------------------------------
    // FormatReviews (with profiles) — empty reviews returns no-data message
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatReviews_WithProfiles_EmptyReviewsAndEmptyProfiles_ReturnsNoDataMessage()
    {
        var result = ReviewFormatter.FormatReviews(
            new List<Review>(),
            new List<ThirdPartyProfile>(),
            maxCount: 10,
            instruction: "Use these to extract tone");

        result.Should().Be("(No client reviews available)");
    }

    // ---------------------------------------------------------------------------
    // FormatReviews (with profiles) — single review formatted correctly
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatReviews_WithProfiles_SingleReview_FormatsCorrectly()
    {
        var review = MakeReview(
            text: "Sold our house in 2 weeks!",
            rating: 5,
            reviewer: "Bob Jones",
            source: "Google");

        var result = ReviewFormatter.FormatReviews(
            new List<Review> { review },
            new List<ThirdPartyProfile>(),
            maxCount: 10,
            instruction: "Identify voice patterns");

        result.Should().Contain("[5/5 — Google] Bob Jones: Sold our house in 2 weeks!");
        result.Should().Contain("Client Reviews (1 of 1)");
    }

    // ---------------------------------------------------------------------------
    // FormatReviews (with profiles) — maxCount limits output
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatReviews_WithProfiles_MaxCountLimitsOutput()
    {
        var reviews = new List<Review>
        {
            MakeReview(text: "First review", reviewer: "Reviewer1"),
            MakeReview(text: "Second review", reviewer: "Reviewer2"),
            MakeReview(text: "Third review", reviewer: "Reviewer3")
        };

        var result = ReviewFormatter.FormatReviews(
            reviews,
            new List<ThirdPartyProfile>(),
            maxCount: 2,
            instruction: "Analyze");

        result.Should().Contain("2 of 3");
        result.Should().Contain("First review");
        result.Should().Contain("Second review");
        result.Should().NotContain("Third review");
    }

    // ---------------------------------------------------------------------------
    // FormatReviews (with profiles) — instruction text appears in output
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatReviews_WithProfiles_InstructionAppearsInOutput()
    {
        var review = MakeReview();

        var result = ReviewFormatter.FormatReviews(
            new List<Review> { review },
            new List<ThirdPartyProfile>(),
            maxCount: 5,
            instruction: "Extract catchphrases and signature expressions");

        result.Should().Contain("INSTRUCTION: Extract catchphrases and signature expressions");
    }

    // ---------------------------------------------------------------------------
    // FormatReviews (with profiles) — profile fallback when top-level reviews empty
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatReviews_WithProfiles_EmptyTopLevelReviews_FallsBackToProfileReviews()
    {
        var profileReview = MakeReview(text: "Excellent service!", reviewer: "Carol", source: "Realtor.com");
        var profile = MakeProfileWithReviews(profileReview);

        var result = ReviewFormatter.FormatReviews(
            new List<Review>(),
            new List<ThirdPartyProfile> { profile },
            maxCount: 10,
            instruction: "Identify tone");

        result.Should().Contain("[5/5 — Realtor.com] Carol: Excellent service!");
        result.Should().NotBe("(No client reviews available)");
    }

    // ---------------------------------------------------------------------------
    // FormatReviews (with profiles) — both empty returns no-data message
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatReviews_WithProfiles_BothEmpty_ReturnsNoDataMessage()
    {
        var emptyProfile = MakeProfileWithReviews();

        var result = ReviewFormatter.FormatReviews(
            new List<Review>(),
            new List<ThirdPartyProfile> { emptyProfile },
            maxCount: 10,
            instruction: "Analyze");

        result.Should().Be("(No client reviews available)");
    }

    // ---------------------------------------------------------------------------
    // FormatReviews (without profiles) — empty reviews returns no-data message
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatReviews_WithoutProfiles_EmptyReviews_ReturnsNoDataMessage()
    {
        var result = ReviewFormatter.FormatReviews(
            new List<Review>(),
            maxCount: 10,
            instruction: "Analyze tone");

        result.Should().Be("(No client reviews available)");
    }

    // ---------------------------------------------------------------------------
    // FormatReviews (without profiles) — single review formatted correctly
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatReviews_WithoutProfiles_SingleReview_FormatsCorrectly()
    {
        var review = MakeReview(
            text: "She found us the perfect home.",
            rating: 4,
            reviewer: "David Lee",
            source: "Redfin");

        var result = ReviewFormatter.FormatReviews(
            new List<Review> { review },
            maxCount: 5,
            instruction: "Extract voice patterns");

        result.Should().Contain("[4/5 — Redfin] David Lee: She found us the perfect home.");
        result.Should().Contain("INSTRUCTION: Extract voice patterns");
        result.Should().Contain("Client Reviews (1 of 1)");
    }

    // ---------------------------------------------------------------------------
    // FormatReviews (without profiles) — maxCount limits output
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatReviews_WithoutProfiles_MaxCountLimitsOutput()
    {
        var reviews = new List<Review>
        {
            MakeReview(text: "Alpha", reviewer: "R1"),
            MakeReview(text: "Beta", reviewer: "R2"),
            MakeReview(text: "Gamma", reviewer: "R3")
        };

        var result = ReviewFormatter.FormatReviews(
            reviews,
            maxCount: 2,
            instruction: "Analyze");

        result.Should().Contain("2 of 3");
        result.Should().Contain("Alpha");
        result.Should().Contain("Beta");
        result.Should().NotContain("Gamma");
    }

    // ---------------------------------------------------------------------------
    // FormatReviews — top-level reviews take precedence over profiles
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatReviews_WithProfiles_TopLevelReviewsNotEmpty_IgnoresProfileReviews()
    {
        var topLevelReview = MakeReview(text: "Top-level review", reviewer: "TopReviewer");
        var profileReview = MakeReview(text: "Profile review", reviewer: "ProfileReviewer");
        var profile = MakeProfileWithReviews(profileReview);

        var result = ReviewFormatter.FormatReviews(
            new List<Review> { topLevelReview },
            new List<ThirdPartyProfile> { profile },
            maxCount: 10,
            instruction: "Analyze");

        result.Should().Contain("Top-level review");
        result.Should().NotContain("Profile review");
    }
}
