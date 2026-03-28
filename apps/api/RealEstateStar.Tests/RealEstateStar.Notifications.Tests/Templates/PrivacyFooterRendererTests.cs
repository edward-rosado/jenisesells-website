using FluentAssertions;
using RealEstateStar.Notifications.Templates;

namespace RealEstateStar.Notifications.Tests.Templates;

public class PrivacyFooterRendererTests
{
    private readonly PrivacyFooterRenderer _renderer = new();

    [Fact]
    public void RenderPrivacyFooter_ContainsUnsubscribeLink()
    {
        var result = _renderer.RenderPrivacyFooter("jenise", "abc123");

        result.Should().Contain("https://jenise.real-estate-star.com/privacy/opt-out?token=abc123");
        result.Should().Contain(">Unsubscribe<");
    }

    [Fact]
    public void RenderPrivacyFooter_ContainsViewMyDataLink()
    {
        var result = _renderer.RenderPrivacyFooter("jenise", "abc123");

        result.Should().Contain("https://jenise.real-estate-star.com/privacy/my-data?token=abc123");
        result.Should().Contain(">View My Data<");
    }

    [Fact]
    public void RenderPrivacyFooter_ContainsPrivacyPolicyLink()
    {
        var result = _renderer.RenderPrivacyFooter("jenise", "abc123");

        result.Should().Contain("https://jenise.real-estate-star.com/privacy\"");
        result.Should().Contain(">Privacy Policy<");
    }

    [Fact]
    public void RenderPrivacyFooter_UrlEncodesConsentToken()
    {
        var result = _renderer.RenderPrivacyFooter("jenise", "tok en+special&chars=1");

        result.Should().Contain("token=tok%20en%2Bspecial%26chars%3D1");
        result.Should().NotContain("tok en+special&chars=1");
    }

    [Fact]
    public void RenderPrivacyFooter_InterpolatesAgentHandleIntoBaseUrl()
    {
        var result = _renderer.RenderPrivacyFooter("myagent", "token123");

        result.Should().Contain("https://myagent.real-estate-star.com");
        result.Should().NotContain("https://jenise.real-estate-star.com");
    }
}
