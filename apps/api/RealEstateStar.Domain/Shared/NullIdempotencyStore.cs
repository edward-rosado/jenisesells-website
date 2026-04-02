using RealEstateStar.Domain.Shared.Interfaces;

namespace RealEstateStar.Domain.Shared;

/// <summary>
/// No-op idempotency store — never reports a step as completed.
/// Used in development and testing where replay protection is not needed.
/// </summary>
public sealed class NullIdempotencyStore : IIdempotencyStore
{
    public Task<bool> HasCompletedAsync(string key, CancellationToken ct) => Task.FromResult(false);
    public Task MarkCompletedAsync(string key, CancellationToken ct) => Task.CompletedTask;
}
