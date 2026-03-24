using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.DataServices.Onboarding;

internal sealed class EncryptingSessionStoreDecorator(
    ISessionStore inner,
    IDataProtectionProvider provider,
    ILogger<EncryptingSessionStoreDecorator> logger) : ISessionStore
{
    private readonly IDataProtector _protector = provider.CreateProtector("OnboardingSession.GoogleTokens.v1");

    public async Task SaveAsync(OnboardingSession session, CancellationToken cancellationToken)
    {
        var original = session.GoogleTokens;
        if (original is not null)
        {
            session.GoogleTokens = original with
            {
                AccessToken = _protector.Protect(original.AccessToken),
                RefreshToken = _protector.Protect(original.RefreshToken),
            };
        }

        try
        {
            await inner.SaveAsync(session, cancellationToken);
        }
        finally
        {
            session.GoogleTokens = original;
        }
    }

    public async Task<OnboardingSession?> LoadAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = await inner.LoadAsync(sessionId, cancellationToken);
        if (session?.GoogleTokens is { } tokens)
        {
            try
            {
                session.GoogleTokens = tokens with
                {
                    AccessToken = _protector.Unprotect(tokens.AccessToken),
                    RefreshToken = _protector.Unprotect(tokens.RefreshToken),
                };
                logger.LogDebug("[SESSION-032] Decrypted tokens for session {SessionId}", sessionId);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                logger.LogWarning(ex, "[SESSION-030] Failed to decrypt tokens for {SessionId}, treating as plaintext (migration)", sessionId);
            }
        }

        return session;
    }
}
