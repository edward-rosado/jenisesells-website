using FluentAssertions;
using RealEstateStar.DataServices.WhatsApp;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Services;

public class ConversationLogRendererTests
{
    [Fact]
    public void RenderHeader_IncludesLeadNameAndScore()
    {
        var header = ConversationLogRenderer.RenderHeader("Jane Doe",
            new DateTime(2026, 3, 19), 82);
        header.Should().Contain("# WhatsApp Conversation — Jane Doe");
        header.Should().Contain("**Score:** 82/100");
        header.Should().Contain("**Submitted:** 2026-03-19");
    }

    [Fact]
    public void RenderMessage_IncludesTimestampAndSender()
    {
        var msg = ConversationLogRenderer.RenderMessage(
            new DateTime(2026, 3, 19, 14, 15, 0),
            "Real Estate Star", "Hello there", "new_lead_notification");
        msg.Should().Contain("2:15 PM — Real Estate Star");
        msg.Should().Contain("(template: new_lead_notification)");
        msg.Should().Contain("> Hello there");
    }

    [Fact]
    public void RenderMessage_OmitsTemplateTag_WhenNull()
    {
        var msg = ConversationLogRenderer.RenderMessage(
            new DateTime(2026, 3, 19, 14, 32, 0),
            "Jenise", "What's her budget?", null);
        msg.Should().Contain("2:32 PM — Jenise");
        msg.Should().NotContain("template:");
    }

    [Fact]
    public void RenderMessage_QuotesMultilineBody()
    {
        var msg = ConversationLogRenderer.RenderMessage(
            new DateTime(2026, 3, 19, 14, 15, 0),
            "Real Estate Star", "Line 1\nLine 2\nLine 3", null);
        msg.Should().Contain("> Line 1");
        msg.Should().Contain("> Line 2");
        msg.Should().Contain("> Line 3");
    }

    [Fact]
    public void RenderDateHeader_FormatsCorrectly()
    {
        var header = ConversationLogRenderer.RenderDateHeader(new DateTime(2026, 3, 19));
        header.Should().Contain("### Mar 19, 2026");
    }
}
