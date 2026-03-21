namespace RealEstateStar.Domain.Notifications.Interfaces;

public interface IEmailTemplateRenderer
{
    string RenderPrivacyFooter(string agentHandle, string consentToken);
}
