using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.SiteFactExtractor.Tests;

public class SiteFactExtractorWorkerTests
{
    private readonly SiteFactExtractorWorker _sut =
        new(NullLogger<SiteFactExtractorWorker>.Instance);

    // ── Test data helpers ─────────────────────────────────────────────────────

    private static AccountConfig FullAccount() => new()
    {
        Handle = "jenise-buckalew",
        Agent = new AccountAgent
        {
            Name = "Jenise Buckalew",
            Title = "REALTOR®",
            Phone = "555-123-4567",
            Email = "jenise@example.com",
            LicenseNumber = "NJ-12345",
            Languages = ["en", "es"],
            Credentials = ["REALTOR®", "CRS", "ABR"]
        },
        Brokerage = new AccountBrokerage
        {
            Name = "Keller Williams",
            LicenseNumber = "KW-9999",
            OfficeAddress = "123 Main St, Montclair, NJ 07042",
            OfficePhone = "555-900-1234"
        },
        Location = new AccountLocation
        {
            State = "NJ",
            ServiceAreas = ["Montclair", "Bloomfield", "Glen Ridge"]
        }
    };

    private static AccountConfig MinimalAccount() => new()
    {
        Handle = "minimal-agent",
        Agent = new AccountAgent
        {
            Name = "Jane Doe",
            Phone = "555-000-0000",
            Email = "jane@example.com"
        }
    };

    private static Review MakeReview(string text = "Great agent!", int rating = 5,
        string reviewer = "John Smith", string source = "Zillow") =>
        new(text, rating, reviewer, source, new DateTime(2026, 1, 10));

    private static ThirdPartyProfile MakeProfile(
        int? salesCount = 25,
        int? yearsExperience = 8,
        IReadOnlyList<string>? specialties = null,
        IReadOnlyList<Review>? reviews = null,
        IReadOnlyList<ListingInfo>? recentSales = null) =>
        new(
            Platform: "Zillow",
            Bio: "Top agent in the area.",
            Reviews: reviews ?? [MakeReview()],
            SalesCount: salesCount,
            ActiveListingCount: 3,
            YearsExperience: yearsExperience,
            Specialties: specialties ?? ["First-Time Buyers", "Luxury"],
            ServiceAreas: ["Montclair"],
            RecentSales: recentSales ?? [MakeListing("100 Oak Ave", "Montclair", "NJ", "$450,000")],
            ActiveListings: []);

    private static ListingInfo MakeListing(string address, string city, string state, string price,
        DateTime? date = null) =>
        new(address, city, state, price,
            Status: "Sold",
            Beds: 3, Baths: 2, Sqft: 1500,
            ImageUrl: null,
            Date: date ?? new DateTime(2025, 12, 1));

    private static AgentDiscovery MakeDiscovery(IReadOnlyList<ThirdPartyProfile>? profiles = null,
        IReadOnlyList<Review>? reviews = null) =>
        new(
            HeadshotBytes: null,
            LogoBytes: null,
            Phone: "555-123-4567",
            Websites: [],
            Reviews: reviews ?? [MakeReview()],
            Profiles: profiles ?? [MakeProfile()],
            Ga4MeasurementId: null,
            WhatsAppEnabled: false);

    private static ActivationOutputs FullOutputs() => new()
    {
        VoiceSkill = "# Voice Profile: Jenise\n## Core Directive\nBe warm and helpful.",
        PersonalitySkill = "# Personality: Jenise\n## Core Traits\nEnthusiastic.",
        PipelineJson = """{"stages": ["Initial Contact", "Active Search", "Under Contract", "Closed"]}""",
        Discovery = MakeDiscovery(),
        LocalizedSkills = new Dictionary<string, string>
        {
            ["VoiceSkill.es"] = "# Perfil de Voz: Jenise\n## Directriz Principal\nSer cálida.",
            ["PersonalitySkill.es"] = "# Personalidad: Jenise\n## Rasgos\nEntusiasta."
        }
    };

    private static ActivationOutputs MinimalOutputs() => new();

    // ── Full extraction ───────────────────────────────────────────────────────

    [Fact]
    public void Extract_FullData_AllFieldsPopulated()
    {
        var result = _sut.Extract(FullOutputs(), FullAccount());

        result.Agent.Name.Should().Be("Jenise Buckalew");
        result.Agent.Title.Should().Be("REALTOR®");
        result.Agent.LicenseNumber.Should().Be("NJ-12345");
        result.Agent.Languages.Should().BeEquivalentTo(["en", "es"]);

        result.Brokerage.Name.Should().Be("Keller Williams");
        result.Brokerage.LicenseNumber.Should().Be("KW-9999");
        result.Brokerage.OfficeAddress.Should().Contain("Montclair");

        result.Location.State.Should().Be("NJ");
        result.Location.ServiceAreas.Should().BeEquivalentTo(["Montclair", "Bloomfield", "Glen Ridge"]);

        result.Specialties.Specialties.Should().Contain("Luxury");

        result.Trust.ReviewCount.Should().BeGreaterThan(0);
        result.Trust.AverageRating.Should().Be(5.0m);

        result.RecentSales.Should().NotBeEmpty();
        result.Testimonials.Should().NotBeEmpty();

        result.Credentials.Should().HaveCount(3);
        result.Credentials.Select(c => c.Name).Should().Contain("CRS");

        result.Stages.StageNames.Should().Contain("Active Search");

        result.VoicesByLocale.Should().ContainKey("en");
        result.VoicesByLocale.Should().ContainKey("es");

        result.FactsHash.Should().NotBeNullOrEmpty();
        result.FactsHash.Should().HaveLength(64); // SHA-256 hex
    }

    // ── Minimal extraction ────────────────────────────────────────────────────

    [Fact]
    public void Extract_MinimalData_DoesNotThrow()
    {
        var act = () => _sut.Extract(MinimalOutputs(), MinimalAccount());
        act.Should().NotThrow();
    }

    [Fact]
    public void Extract_MinimalData_HasDefaults()
    {
        var result = _sut.Extract(MinimalOutputs(), MinimalAccount());

        result.Agent.Name.Should().Be("Jane Doe");
        result.Brokerage.Name.Should().BeEmpty();
        result.Location.State.Should().BeEmpty();
        result.Location.ServiceAreas.Should().BeEmpty();
        result.Specialties.Specialties.Should().BeEmpty();
        result.Trust.ReviewCount.Should().Be(0);
        result.Trust.AverageRating.Should().Be(0m);
        result.Trust.TransactionCount.Should().Be(0);
        result.RecentSales.Should().BeEmpty();
        result.Testimonials.Should().BeEmpty();
        result.Credentials.Should().BeEmpty();
        result.Stages.StageNames.Should().BeEquivalentTo(
            ["Initial Contact", "Qualification", "Active Search", "Under Contract", "Closed"]);
        result.VoicesByLocale.Should().BeEmpty();
        result.FactsHash.Should().HaveLength(64);
    }

    // ── RecentSales capped at 6 ───────────────────────────────────────────────

    [Fact]
    public void Extract_RecentSalesCappedAt6()
    {
        var manySales = Enumerable.Range(1, 20)
            .Select(i => MakeListing($"{i} Elm St", "Montclair", "NJ", "$500,000",
                new DateTime(2025, 1, i)))
            .ToList();

        var outputs = new ActivationOutputs
        {
            Discovery = new AgentDiscovery(null, null, null, [],
                [],
                [new ThirdPartyProfile("Zillow", null, [], null, null, null, [],
                    [], manySales, [])],
                null, false)
        };

        var result = _sut.Extract(outputs, MinimalAccount());

        result.RecentSales.Should().HaveCount(6);
    }

    // ── Testimonials capped at 8 ──────────────────────────────────────────────

    [Fact]
    public void Extract_TestimonialsCappedAt8()
    {
        var manyReviews = Enumerable.Range(1, 25)
            .Select(i => MakeReview($"Review {i}", rating: 5, reviewer: $"Reviewer {i}"))
            .ToList();

        var outputs = new ActivationOutputs
        {
            Discovery = MakeDiscovery(reviews: manyReviews)
        };

        var result = _sut.Extract(outputs, MinimalAccount());

        result.Testimonials.Should().HaveCount(8);
    }

    // ── FactsHash determinism ─────────────────────────────────────────────────

    [Fact]
    public void Extract_FactsHash_IsDeterministic()
    {
        var outputs = FullOutputs();
        var account = FullAccount();

        var hash1 = _sut.Extract(outputs, account).FactsHash;
        var hash2 = _sut.Extract(outputs, account).FactsHash;

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Extract_FactsHash_ChangesWhenAgentNameChanges()
    {
        var outputs = FullOutputs();

        var accountA = FullAccount();
        var accountB = new AccountConfig
        {
            Handle = accountA.Handle,
            Agent = new AccountAgent
            {
                Name = "Different Agent",
                Phone = accountA.Agent!.Phone,
                Email = accountA.Agent.Email,
                Languages = accountA.Agent.Languages
            },
            Brokerage = accountA.Brokerage,
            Location = accountA.Location
        };

        var hashA = _sut.Extract(outputs, accountA).FactsHash;
        var hashB = _sut.Extract(outputs, accountB).FactsHash;

        hashA.Should().NotBe(hashB);
    }

    [Fact]
    public void Extract_FactsHash_ChangesWhenVoiceSkillChanges()
    {
        var outputsA = FullOutputs();
        var outputsB = outputsA with { VoiceSkill = "Different voice skill content" };
        var account = FullAccount();

        var hashA = _sut.Extract(outputsA, account).FactsHash;
        var hashB = _sut.Extract(outputsB, account).FactsHash;

        hashA.Should().NotBe(hashB);
    }

    // ── Missing brokerage ─────────────────────────────────────────────────────

    [Fact]
    public void Extract_NoBrokerage_BrokerageIdentityHasEmptyName()
    {
        var account = new AccountConfig
        {
            Handle = "no-brokerage",
            Agent = new AccountAgent { Name = "Solo Agent", Phone = "555-000", Email = "a@b.com" },
            Brokerage = null,
            Location = new AccountLocation { State = "NJ", ServiceAreas = [] }
        };

        var act = () => _sut.Extract(MinimalOutputs(), account);
        act.Should().NotThrow();

        var result = _sut.Extract(MinimalOutputs(), account);
        result.Brokerage.Name.Should().BeEmpty();
        result.Brokerage.LicenseNumber.Should().BeNull();
        result.Brokerage.OfficeAddress.Should().BeNull();
        result.Brokerage.OfficePhone.Should().BeNull();
    }

    // ── VoicesByLocale from LocalizedSkills ───────────────────────────────────

    [Fact]
    public void Extract_LocalizedSkills_PopulatesVoicesByLocale()
    {
        var outputs = new ActivationOutputs
        {
            VoiceSkill = "# English Voice",
            PersonalitySkill = "# English Personality",
            LocalizedSkills = new Dictionary<string, string>
            {
                ["VoiceSkill.es"] = "# Spanish Voice",
                ["PersonalitySkill.es"] = "# Spanish Personality",
                ["VoiceSkill.pt"] = "# Portuguese Voice"
            }
        };

        var result = _sut.Extract(outputs, MinimalAccount());

        result.VoicesByLocale.Should().ContainKey("en");
        result.VoicesByLocale["en"].VoiceSkillMarkdown.Should().Be("# English Voice");
        result.VoicesByLocale["en"].PersonalitySkillMarkdown.Should().Be("# English Personality");

        result.VoicesByLocale.Should().ContainKey("es");
        result.VoicesByLocale["es"].VoiceSkillMarkdown.Should().Be("# Spanish Voice");
        result.VoicesByLocale["es"].PersonalitySkillMarkdown.Should().Be("# Spanish Personality");

        result.VoicesByLocale.Should().ContainKey("pt");
        result.VoicesByLocale["pt"].VoiceSkillMarkdown.Should().Be("# Portuguese Voice");
    }

    [Fact]
    public void Extract_NoVoiceSkills_VoicesByLocaleIsEmpty()
    {
        var result = _sut.Extract(MinimalOutputs(), MinimalAccount());
        result.VoicesByLocale.Should().BeEmpty();
    }

    [Fact]
    public void Extract_LocaleVoice_VoiceHashIsPopulated()
    {
        var outputs = new ActivationOutputs
        {
            VoiceSkill = "# English Voice",
            PersonalitySkill = "# English Personality"
        };

        var result = _sut.Extract(outputs, MinimalAccount());

        result.VoicesByLocale["en"].VoiceHash.Should().NotBeNullOrEmpty();
        result.VoicesByLocale["en"].VoiceHash.Should().HaveLength(16);
    }

    // ── Pipeline stage parsing ────────────────────────────────────────────────

    [Fact]
    public void Extract_ValidPipelineJson_UsesJsonStages()
    {
        var outputs = new ActivationOutputs
        {
            PipelineJson = """{"stages": ["Lead In", "Touring", "Offer", "Closed"]}"""
        };

        var result = _sut.Extract(outputs, MinimalAccount());

        result.Stages.StageNames.Should().BeEquivalentTo(
            ["Lead In", "Touring", "Offer", "Closed"],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Extract_MalformedPipelineJson_FallsBackToDefaults()
    {
        var outputs = new ActivationOutputs
        {
            PipelineJson = "not valid json {"
        };

        var act = () => _sut.Extract(outputs, MinimalAccount());
        act.Should().NotThrow();

        var result = _sut.Extract(outputs, MinimalAccount());
        result.Stages.StageNames.Should().HaveCount(5);
        result.Stages.StageNames.Should().Contain("Qualification");
    }

    [Fact]
    public void Extract_PipelineJsonWithObjectStages_ParsesNameProperty()
    {
        var outputs = new ActivationOutputs
        {
            PipelineJson = """{"stages": [{"name": "First Contact"}, {"name": "Closed"}]}"""
        };

        var result = _sut.Extract(outputs, MinimalAccount());

        result.Stages.StageNames.Should().BeEquivalentTo(
            ["First Contact", "Closed"],
            o => o.WithStrictOrdering());
    }

    // ── Trust signals ─────────────────────────────────────────────────────────

    [Fact]
    public void Extract_MultipleProfileReviews_AggregatesAllReviews()
    {
        var profile1 = MakeProfile(reviews: [MakeReview("Excellent!", 5)]);
        var profile2 = MakeProfile(reviews: [MakeReview("Very good.", 4), MakeReview("Good.", 4)]);
        var directReviews = new List<Review> { MakeReview("Outstanding!", 5) };

        var outputs = new ActivationOutputs
        {
            Discovery = new AgentDiscovery(null, null, null, [],
                directReviews, [profile1, profile2], null, false)
        };

        var result = _sut.Extract(outputs, MinimalAccount());

        // 1 direct + 1 + 2 from profiles = 4 total
        result.Trust.ReviewCount.Should().Be(4);
        result.Trust.AverageRating.Should().Be(4.5m);
    }

    [Fact]
    public void Extract_TransactionCount_SumsProfileSalesCounts()
    {
        var profile1 = MakeProfile(salesCount: 15);
        var profile2 = MakeProfile(salesCount: 30);

        var outputs = new ActivationOutputs
        {
            Discovery = MakeDiscovery(profiles: [profile1, profile2])
        };

        var result = _sut.Extract(outputs, MinimalAccount());

        result.Trust.TransactionCount.Should().Be(45);
    }

    // ── Credentials ───────────────────────────────────────────────────────────

    [Fact]
    public void Extract_CredentialsMappedFromAccount()
    {
        var account = FullAccount();
        var result = _sut.Extract(MinimalOutputs(), account);

        result.Credentials.Should().HaveCount(3);
        result.Credentials.Select(c => c.Name).Should().Contain("REALTOR®");
        result.Credentials.Select(c => c.Name).Should().Contain("CRS");
        result.Credentials.Select(c => c.Name).Should().Contain("ABR");
        result.Credentials.All(c => c.Issuer == null).Should().BeTrue();
    }
}
