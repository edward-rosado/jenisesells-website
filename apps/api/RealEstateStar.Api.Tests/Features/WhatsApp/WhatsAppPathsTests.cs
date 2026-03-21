using FluentAssertions;
using RealEstateStar.Api.Features.WhatsApp;

namespace RealEstateStar.Api.Tests.Features.WhatsApp;

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
