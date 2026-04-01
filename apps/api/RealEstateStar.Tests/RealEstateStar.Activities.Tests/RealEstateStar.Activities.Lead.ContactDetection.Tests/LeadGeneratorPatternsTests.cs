using FluentAssertions;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Activities.Lead.ContactDetection.Tests;

public class LeadGeneratorPatternsTests
{
    // ── IsLeadGeneratorEmail ──────────────────────────────────────────────

    [Theory]
    [InlineData("leads@trulead.com")]
    [InlineData("notify@zillow.com")]
    [InlineData("noreply@realtor.com")]
    [InlineData("alerts@boldleads.com")]
    [InlineData("leads@cincpro.com")]
    [InlineData("noreply@kvcore.com")]
    [InlineData("leads@insiderealestate.com")]
    [InlineData("notify@ylopo.com")]
    [InlineData("leads@realgeeks.com")]
    [InlineData("notify@boomtownroi.com")]
    [InlineData("leads@followupboss.com")]
    [InlineData("noreply@sierraint.com")]
    public void IsLeadGeneratorEmail_returns_true_for_known_domains(string fromAddress)
    {
        LeadGeneratorPatterns.IsLeadGeneratorEmail(fromAddress).Should().BeTrue(
            because: $"{fromAddress} should be recognized as a lead generator");
    }

    [Theory]
    [InlineData("client@gmail.com")]
    [InlineData("john.doe@outlook.com")]
    [InlineData("agent@realestate.com")]
    [InlineData("info@somebroker.com")]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void IsLeadGeneratorEmail_returns_false_for_unknown_domains(string fromAddress)
    {
        LeadGeneratorPatterns.IsLeadGeneratorEmail(fromAddress).Should().BeFalse(
            because: $"{fromAddress} should not be recognized as a lead generator");
    }

    [Fact]
    public void IsLeadGeneratorEmail_is_case_insensitive()
    {
        LeadGeneratorPatterns.IsLeadGeneratorEmail("LEADS@ZILLOW.COM").Should().BeTrue();
        LeadGeneratorPatterns.IsLeadGeneratorEmail("Notify@Realtor.Com").Should().BeTrue();
    }

    // ── GetPlatformName ───────────────────────────────────────────────────

    [Theory]
    [InlineData("leads@trulead.com", "TruLead")]
    [InlineData("notify@zillow.com", "Zillow")]
    [InlineData("noreply@realtor.com", "Realtor.com")]
    [InlineData("alerts@boldleads.com", "BoldLeads")]
    [InlineData("leads@cincpro.com", "CincPro")]
    [InlineData("noreply@kvcore.com", "kvCORE")]
    [InlineData("leads@insiderealestate.com", "Inside Real Estate")]
    [InlineData("notify@ylopo.com", "Ylopo")]
    [InlineData("leads@realgeeks.com", "Real Geeks")]
    [InlineData("notify@boomtownroi.com", "BoomTown")]
    [InlineData("leads@followupboss.com", "Follow Up Boss")]
    [InlineData("noreply@sierraint.com", "Sierra")]
    public void GetPlatformName_returns_expected_name(string fromAddress, string expectedName)
    {
        LeadGeneratorPatterns.GetPlatformName(fromAddress).Should().Be(expectedName);
    }

    [Fact]
    public void GetPlatformName_returns_null_for_unknown_domain()
    {
        LeadGeneratorPatterns.GetPlatformName("user@gmail.com").Should().BeNull();
    }

    // ── ParseLeadFromEmail ────────────────────────────────────────────────

    [Fact]
    public void ParseLeadFromEmail_extracts_name_email_and_phone_from_body()
    {
        var subject = "New Lead Notification";
        var body = """
            You have a new lead!
            Name: John Smith
            Email: john.smith@example.com
            Phone: (555) 123-4567
            """;
        var from = "leads@zillow.com";

        var result = LeadGeneratorPatterns.ParseLeadFromEmail(subject, body, from);

        result.Should().NotBeNull();
        result!.Name.Should().Be("John Smith");
        result.Email.Should().Be("john.smith@example.com");
        result.Phone.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ParseLeadFromEmail_falls_back_to_subject_for_name_when_body_has_none()
    {
        var subject = "New Lead: Jane Doe";
        var body = "Email: jane.doe@example.com";
        var from = "notify@boldleads.com";

        var result = LeadGeneratorPatterns.ParseLeadFromEmail(subject, body, from);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Jane Doe");
    }

    [Fact]
    public void ParseLeadFromEmail_returns_null_when_no_name_found()
    {
        var subject = "New Lead Notification";
        var body = "You have a new inquiry.";
        var from = "leads@realgeeks.com";

        var result = LeadGeneratorPatterns.ParseLeadFromEmail(subject, body, from);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseLeadFromEmail_extracts_buyer_with_email_only()
    {
        var subject = "New Buyer: Robert Johnson";
        var body = "Buyer: Robert Johnson\nEmail: rjohnson@email.com";
        var from = "leads@cincpro.com";

        var result = LeadGeneratorPatterns.ParseLeadFromEmail(subject, body, from);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Robert Johnson");
        result.Email.Should().Be("rjohnson@email.com");
    }

    [Fact]
    public void ParseLeadFromEmail_returns_unknown_role()
    {
        var subject = "New Lead: Mary Williams";
        var body = "Name: Mary Williams\nEmail: mwilliams@test.com";
        var from = "notify@ylopo.com";

        var result = LeadGeneratorPatterns.ParseLeadFromEmail(subject, body, from);

        result.Should().NotBeNull();
        result!.Role.Should().Be(ContactRole.Unknown);
    }

    [Fact]
    public void ParseLeadFromEmail_handles_null_or_empty_inputs_gracefully()
    {
        // No name possible → null
        LeadGeneratorPatterns.ParseLeadFromEmail("", "", "leads@zillow.com").Should().BeNull();
    }
}
