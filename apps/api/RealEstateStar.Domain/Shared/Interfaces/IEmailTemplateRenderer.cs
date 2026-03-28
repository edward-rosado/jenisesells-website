namespace RealEstateStar.Domain.Shared.Interfaces;

public interface IEmailTemplateRenderer
{
    string RenderPrivacyFooter(string agentHandle, string consentToken);
}
