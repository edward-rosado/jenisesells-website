using RealEstateStar.Domain.Shared.Interfaces;

namespace RealEstateStar.Services.LeadCommunicator.Templates;

public class PrivacyFooterRenderer : IEmailTemplateRenderer
{
    private const string BaseUrl = "real-estate-star.com";

    public string RenderPrivacyFooter(string agentHandle, string consentToken)
    {
        var baseUri = $"https://{agentHandle}.{BaseUrl}";
        var encodedToken = Uri.EscapeDataString(consentToken);

        return $"""
            <div style="margin-top: 32px; padding-top: 16px; border-top: 1px solid #e0e0e0; font-size: 12px; color: #666; text-align: center;">
                <p>
                    <a href="{baseUri}/privacy/opt-out?token={encodedToken}" style="color: #666; text-decoration: underline;">Unsubscribe</a>
                    &nbsp;|&nbsp;
                    <a href="{baseUri}/privacy/my-data?token={encodedToken}" style="color: #666; text-decoration: underline;">View My Data</a>
                    &nbsp;|&nbsp;
                    <a href="{baseUri}/privacy" style="color: #666; text-decoration: underline;">Privacy Policy</a>
                </p>
            </div>
            """;
    }
}
