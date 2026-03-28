namespace RealEstateStar.Workers.Shared.Context;

/// <summary>
/// Non-generic base so PipelineWorker can reference without knowing TRequest.
/// Tracks pipeline-level timing, per-step timing, and retry history.
/// </summary>
public abstract class PipelineContext
{
    public required string AgentId { get; init; }
    public required string CorrelationId { get; init; }

    // Retry tracking
    public int AttemptNumber { get; set; } = 1;
    public int TotalFailures { get; set; }
    public DateTime? LastFailedAt { get; set; }

    // Pipeline-level timing
    public DateTime? PipelineStartedAt { get; set; }
    public DateTime? PipelineCompletedAt { get; set; }
    public double? PipelineDurationMs => PipelineStartedAt.HasValue && PipelineCompletedAt.HasValue
        ? (PipelineCompletedAt.Value - PipelineStartedAt.Value).TotalMilliseconds
        : null;

    // Per-step tracking
    public Dictionary<string, StepRecord> Steps { get; } = new();

    // Intermediate data
    public Dictionary<string, object> Data { get; } = new();

    // Data accessors
    public T? Get<T>(string key) where T : class =>
        Data.TryGetValue(key, out var value) ? value as T : null;

    public void Set<T>(string key, T value) where T : class =>
        Data[key] = value;

    // Step tracking
    public StepRecord GetOrCreateStep(string stepName)
    {
        if (!Steps.TryGetValue(stepName, out var record))
        {
            record = new StepRecord { Name = stepName };
            Steps[stepName] = record;
        }
        return record;
    }

    public bool HasCompleted(string stepName) =>
        Steps.TryGetValue(stepName, out var s) && s.Status == PipelineStepStatus.Completed;

    public bool HasPartiallyCompleted(string stepName) =>
        Steps.TryGetValue(stepName, out var s) && s.Status == PipelineStepStatus.PartiallyCompleted;

    public bool HasCompletedSubStep(string stepName, string subStepName) =>
        Steps.TryGetValue(stepName, out var s) && s.CompletedSubSteps.Contains(subStepName);

    public void MarkSubStepCompleted(string stepName, string subStepName)
    {
        var step = GetOrCreateStep(stepName);
        step.CompletedSubSteps.Add(subStepName);
        if (step.Status == PipelineStepStatus.Pending)
            step.Status = PipelineStepStatus.InProgress;
    }
}

/// <summary>
/// Generic context that carries the original request object.
/// </summary>
public abstract class PipelineContext<TRequest> : PipelineContext
{
    public required TRequest Request { get; init; }
}
