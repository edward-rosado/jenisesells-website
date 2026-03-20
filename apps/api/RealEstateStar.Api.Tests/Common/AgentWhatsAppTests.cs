using System.Text.Json;
using FluentAssertions;
using RealEstateStar.Api.Common;

namespace RealEstateStar.Api.Tests.Common;

public class AgentWhatsAppTests
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
        var config = JsonSerializer.Deserialize<AgentWhatsApp>(json);
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
        var config = new AgentWhatsApp { PhoneNumber = "+12015551234", OptedIn = true };
        config.Status.Should().Be("not_registered");
        config.WelcomeSent.Should().BeFalse();
        config.RetryAfter.Should().BeNull();
    }

    [Fact]
    public void Defaults_NotificationPreferences_IncludesDataDeletion()
    {
        var config = new AgentWhatsApp { PhoneNumber = "+12015551234", OptedIn = true };
        config.NotificationPreferences.Should().Contain("data_deletion");
    }

    [Fact]
    public void AgentIntegrations_Deserializes_WithWhatsApp()
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
        var integrations = JsonSerializer.Deserialize<AgentIntegrations>(json);
        integrations!.WhatsApp.Should().NotBeNull();
        integrations.WhatsApp!.PhoneNumber.Should().Be("+12015551234");
    }

    [Fact]
    public void AgentIntegrations_NullWhatsApp_WhenNotPresent()
    {
        var json = """{"email_provider": "gmail"}""";
        var integrations = JsonSerializer.Deserialize<AgentIntegrations>(json);
        integrations!.WhatsApp.Should().BeNull();
    }
}
