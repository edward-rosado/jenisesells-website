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
using System.Text.Json;
using FluentAssertions;

namespace RealEstateStar.Api.Tests.Common;

public class AccountWhatsAppTests
{
    [Fact]
    public void Deserializes_WhatsAppConfig_FromJson()
    {
        var json = """
        {
            "phone_number": "+12015551234",
            "opted_in": true,
            "notification_preferences": ["new_lead", "cma_ready", "data_deletion"],
            "status": "active",
            "welcome_sent": true,
            "retry_after": "2026-03-19T18:00:00Z"
        }
        """;
        var config = JsonSerializer.Deserialize<AccountWhatsApp>(json);
        config.Should().NotBeNull();
        config!.PhoneNumber.Should().Be("+12015551234");
        config.OptedIn.Should().BeTrue();
        config.NotificationPreferences.Should().Contain("new_lead");
        config.Status.Should().Be("active");
        config.WelcomeSent.Should().BeTrue();
        config.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public void Defaults_Status_ToNotRegistered()
    {
        var config = new AccountWhatsApp { PhoneNumber = "+12015551234", OptedIn = true };
        config.Status.Should().Be("not_registered");
        config.WelcomeSent.Should().BeFalse();
        config.RetryAfter.Should().BeNull();
    }

    [Fact]
    public void Defaults_NotificationPreferences_IncludesDataDeletion()
    {
        var config = new AccountWhatsApp { PhoneNumber = "+12015551234", OptedIn = true };
        config.NotificationPreferences.Should().Contain("data_deletion");
    }

    [Fact]
    public void AccountIntegrations_Deserializes_WithWhatsApp()
    {
        var json = """
        {
            "email_provider": "gmail",
            "whatsapp": {
                "phone_number": "+12015551234",
                "opted_in": true
            }
        }
        """;
        var integrations = JsonSerializer.Deserialize<AccountIntegrations>(json);
        integrations!.WhatsApp.Should().NotBeNull();
        integrations.WhatsApp!.PhoneNumber.Should().Be("+12015551234");
    }

    [Fact]
    public void AccountIntegrations_NullWhatsApp_WhenNotPresent()
    {
        var json = """{"email_provider": "gmail"}""";
        var integrations = JsonSerializer.Deserialize<AccountIntegrations>(json);
        integrations!.WhatsApp.Should().BeNull();
    }
}
