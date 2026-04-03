namespace RealEstateStar.Domain.Shared.Interfaces;

/// <summary>
/// Guards non-idempotent side effects (email sends, WhatsApp messages) against
/// duplicate execution during Durable Functions replay or Container App restarts.
/// Keys follow the pattern: {pipeline}:{instanceId}:{step}
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Returns true if the step has already completed successfully.</summary>
    Task<bool> HasCompletedAsync(string key, CancellationToken ct);

    /// <summary>Marks the step as completed. Idempotent — safe to call multiple times.</summary>
    Task MarkCompletedAsync(string key, CancellationToken ct);
}
