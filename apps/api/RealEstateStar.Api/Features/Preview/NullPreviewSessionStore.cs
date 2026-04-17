using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Api.Features.Preview;

/// <summary>
/// No-op preview session store for local development when Azure Storage is not configured.
/// All reads return null; writes are silently discarded.
/// </summary>
internal sealed class NullPreviewSessionStore : IPreviewSessionStore
{
    public Task CreateAsync(PreviewSession session, CancellationToken ct) => Task.CompletedTask;

    public Task<PreviewSession?> GetAsync(string sessionId, CancellationToken ct) =>
        Task.FromResult<PreviewSession?>(null);

    public Task RevokeAsync(string sessionId, CancellationToken ct) =>
        throw new KeyNotFoundException($"Session '{sessionId}' not found (null store — Azure Storage not configured).");

    public Task<PreviewSession> RefreshExpiryAsync(string sessionId, DateTime createdAt, CancellationToken ct) =>
        throw new KeyNotFoundException($"Session '{sessionId}' not found (null store — Azure Storage not configured).");
}
