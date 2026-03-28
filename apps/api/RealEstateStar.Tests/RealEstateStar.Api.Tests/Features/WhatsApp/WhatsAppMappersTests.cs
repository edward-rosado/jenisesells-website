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
using RealEstateStar.Services.AgentNotifier;
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
using FluentAssertions;

namespace RealEstateStar.Api.Tests.Features.WhatsApp;

public class WhatsAppMappersTests
{
    [Fact]
    public void ToNewLeadParams_MapsAllFields()
    {
        var result = WhatsAppMappers.ToNewLeadParams(
            leadName: "Jane Smith",
            phone: "+12015559876",
            email: "jane@example.com",
            interest: "Buying",
            area: "Montclair, NJ");

        result.Should().HaveCount(5);
        result[0].Should().Be(("text", "Jane Smith"));
        result[1].Should().Be(("text", "+12015559876"));
        result[2].Should().Be(("text", "jane@example.com"));
        result[3].Should().Be(("text", "Buying"));
        result[4].Should().Be(("text", "Montclair, NJ"));
    }

    [Fact]
    public void ToCmaReadyParams_MapsAllFields()
    {
        var result = WhatsAppMappers.ToCmaReadyParams(
            leadName: "John Doe",
            address: "123 Main St, Montclair NJ",
            estimatedValue: "$650,000");

        result.Should().HaveCount(3);
        result[0].Should().Be(("text", "John Doe"));
        result[1].Should().Be(("text", "123 Main St, Montclair NJ"));
        result[2].Should().Be(("text", "$650,000"));
    }

    [Fact]
    public void ToFollowUpParams_MapsLeadAndDays()
    {
        var result = WhatsAppMappers.ToFollowUpParams(
            leadName: "Alice Brown",
            daysSinceSubmission: 7);

        result.Should().HaveCount(2);
        result[0].Should().Be(("text", "Alice Brown"));
        result[1].Should().Be(("text", "7"));
    }

    [Fact]
    public void ToDataDeletionParams_MapsLeadAndDeadline()
    {
        var deadline = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var result = WhatsAppMappers.ToDataDeletionParams(
            leadName: "Bob Wilson",
            deletionDeadline: deadline);

        result.Should().HaveCount(2);
        result[0].Should().Be(("text", "Bob Wilson"));
        result[1].Should().Be(("text", "2026-06-15"));
    }

    [Fact]
    public void ToWelcomeParams_MapsAgentName()
    {
        var result = WhatsAppMappers.ToWelcomeParams(agentFirstName: "Jenise");

        result.Should().HaveCount(1);
        result[0].Should().Be(("text", "Jenise"));
    }

    [Fact]
    public void Sanitize_StripsTemplateBraces()
    {
        // The sanitizer is exercised through the public mappers;
        // we verify it strips {{ and }} from input values.
        var result = WhatsAppMappers.ToNewLeadParams(
            leadName: "Jane {{Smith}}",
            phone: "+1",
            email: "x@x.com",
            interest: "Buy",
            area: "NJ");

        result[0].Should().Be(("text", "Jane Smith"));
    }

    [Fact]
    public void AllMappers_StripTemplateBraces()
    {
        const string injected = "{{evil}}";
        const string clean = "evil";

        // ToNewLeadParams — all string params
        var newLead = WhatsAppMappers.ToNewLeadParams(injected, injected, injected, injected, injected);
        newLead.Should().AllSatisfy(p => p.value.Should().Be(clean));

        // ToCmaReadyParams — all string params
        var cma = WhatsAppMappers.ToCmaReadyParams(injected, injected, injected);
        cma.Should().AllSatisfy(p => p.value.Should().Be(clean));

        // ToFollowUpParams — only leadName is a string (daysSinceSubmission is int, not sanitizable)
        var followUp = WhatsAppMappers.ToFollowUpParams(injected, 3);
        followUp[0].value.Should().Be(clean);

        // ToDataDeletionParams — only leadName is a string (deletionDeadline is DateTime)
        var deletion = WhatsAppMappers.ToDataDeletionParams(injected, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        deletion[0].value.Should().Be(clean);

        // ToWelcomeParams — single string param
        var welcome = WhatsAppMappers.ToWelcomeParams(injected);
        welcome[0].value.Should().Be(clean);
    }
}
