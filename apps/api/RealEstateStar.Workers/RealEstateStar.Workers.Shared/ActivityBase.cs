using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Workers.Shared;

/// <summary>
/// Lightweight base for Activities that provides consistent OTel span creation,
/// timing, and structured start/end logging. Used by new Activities only —
/// existing Activities are not retrofitted.
/// </summary>
public abstract class ActivityBase(
    ActivitySource activitySource,
    ILogger logger,
    string activityName)
{
    protected async Task ExecuteWithSpanAsync(
        string operationName, Func<Task> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var span = activitySource.StartActivity($"{activityName}.{operationName}");
        var sw = Stopwatch.GetTimestamp();

        logger.LogInformation("[{ActivityName}] Starting {Operation}",
            activityName, operationName);

        try
        {
            await action();
            var elapsed = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            span?.SetTag("outcome", "complete");
            logger.LogInformation("[{ActivityName}] {Operation} completed in {Duration}ms",
                activityName, operationName, elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var elapsed = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "[{ActivityName}] {Operation} failed after {Duration}ms",
                activityName, operationName, elapsed);
            throw;
        }
    }

    protected async Task<T> ExecuteWithSpanAsync<T>(
        string operationName, Func<Task<T>> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var span = activitySource.StartActivity($"{activityName}.{operationName}");
        var sw = Stopwatch.GetTimestamp();

        logger.LogInformation("[{ActivityName}] Starting {Operation}",
            activityName, operationName);

        try
        {
            var result = await action();
            var elapsed = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            span?.SetTag("outcome", "complete");
            logger.LogInformation("[{ActivityName}] {Operation} completed in {Duration}ms",
                activityName, operationName, elapsed);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var elapsed = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "[{ActivityName}] {Operation} failed after {Duration}ms",
                activityName, operationName, elapsed);
            throw;
        }
    }
}
