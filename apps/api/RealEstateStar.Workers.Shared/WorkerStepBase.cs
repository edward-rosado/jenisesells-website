using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Workers.Shared;

/// <summary>
/// Base class for worker pipeline steps. Provides OTel span creation and structured logging
/// around each step execution.
/// </summary>
public abstract class WorkerStepBase<TRequest, TResponse>(
    ActivitySource activitySource,
    ILogger logger) : IWorkerStep<TRequest, TResponse>
{
    public abstract string StepName { get; }

    public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct)
    {
        using var activity = activitySource.StartActivity(StepName);
        var sw = Stopwatch.GetTimestamp();

        try
        {
            logger.LogInformation("[{StepName}] Starting step", StepName);
            var result = await ExecuteCoreAsync(request, ct);
            var elapsed = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            logger.LogInformation("[{StepName}] Completed in {DurationMs}ms", StepName, elapsed);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var elapsed = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "[{StepName}] Failed after {DurationMs}ms", StepName, elapsed);
            throw;
        }
    }

    protected abstract Task<TResponse> ExecuteCoreAsync(TRequest request, CancellationToken ct);
}
