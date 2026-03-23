namespace RealEstateStar.Workers.Shared.Context;

public class StepRecord
{
    public required string Name { get; init; }
    public PipelineStepStatus Status { get; set; } = PipelineStepStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? DurationMs => StartedAt.HasValue && CompletedAt.HasValue
        ? (CompletedAt.Value - StartedAt.Value).TotalMilliseconds
        : null;
    public string? Error { get; set; }
    public HashSet<string> CompletedSubSteps { get; } = [];
    public List<ErrorEntry> ErrorHistory { get; } = [];
}
