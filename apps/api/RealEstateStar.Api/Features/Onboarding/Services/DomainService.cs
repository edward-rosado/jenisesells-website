using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

// TODO: LOW-5 — Consider returning a result object instead of mutating session directly
public class DomainService(ILogger<DomainService> logger)
{
    public Task<bool> ValidateDnsAsync(string domain, CancellationToken ct)
    {
        // TODO: Check DNS CNAME/A records for the custom domain.
        // For now, stub returns true for any domain.
        logger.LogInformation("Validating DNS for custom domain {Domain}", domain);
        return Task.FromResult(true);
    }

    public Task<string> ConfigureCustomDomainAsync(OnboardingSession session, string domain, CancellationToken ct)
    {
        // TODO: Wire to Cloudflare API to add custom domain.
        session.CustomDomain = domain;
        logger.LogInformation("Custom domain {Domain} configured for session {SessionId}", domain, session.Id);
        return Task.FromResult($"Custom domain {domain} configured. DNS propagation may take up to 24 hours.");
    }
}
