namespace RealEstateStar.Workers.Shared;

public class PipelineRetryOptions
{
    public int MaxRetries { get; init; } = 3;
    public int BaseDelaySeconds { get; init; } = 30;
    public int MaxDelaySeconds { get; init; } = 600;
    public double BackoffMultiplier { get; init; } = 2.0;

    public TimeSpan GetDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(
            BaseDelaySeconds * Math.Pow(BackoffMultiplier, attempt - 1),
            MaxDelaySeconds));
}
