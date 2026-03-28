using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.DataServices.Config;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.DataServices.WhatsApp;
namespace RealEstateStar.DataServices.Tests.WhatsApp;

public class WhatsAppPathsTests
{
    [Fact]
    public void LeadConversation_ReturnsCorrectPath()
    {
        var path = WhatsAppPaths.LeadConversation("Jane Doe");
        path.Should().Be("Real Estate Star/1 - Leads/Jane Doe/WhatsApp Conversation.md");
    }

    [Fact]
    public void GeneralConversation_IsCorrectConstant()
    {
        WhatsAppPaths.GeneralConversation.Should().Be("Real Estate Star/WhatsApp/General.md");
    }
}
