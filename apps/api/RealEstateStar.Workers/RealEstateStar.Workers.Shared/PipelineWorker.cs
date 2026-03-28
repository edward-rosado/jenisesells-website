using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.Shared;

/// <summary>
/// Base class for all background pipeline workers. Enforces a consistent pattern:
/// read from channel → build context → execute steps (checkpoint/resume) →
/// exponential backoff retry → dead letter → health tracking.
/// </summary>
public abstract class PipelineWorker<TRequest, TContext>(
    ProcessingChannelBase<TRequest> channel,
    BackgroundServiceHealthTracker healthTracker,
    ILogger logger,
    PipelineRetryOptions? retryOptions = null) : BackgroundService
    where TContext : PipelineContext
{
    private readonly PipelineRetryOptions _retryOptions = retryOptions ?? new();

    protected abstract string WorkerName { get; }
    protected abstract TContext CreateContext(TRequest request);
    protected abstract Task ProcessAsync(TContext context, CancellationToken ct);

    protected virtual Task OnPermanentFailureAsync(TContext context, Exception lastException, CancellationToken ct) =>
        Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[{Worker}-001] {Worker} started. MaxRetries: {MaxRetries}, BaseDelay: {BaseDelay}s",
            WorkerName, WorkerName, _retryOptions.MaxRetries, _retryOptions.BaseDelaySeconds);

        await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
        {
            var ctx = CreateContext(request);
            await ExecuteWithRetryAsync(ctx, stoppingToken);
        }

        logger.LogInformation("[{Worker}-003] {Worker} stopping.", WorkerName, WorkerName);
    }

    private async Task ExecuteWithRetryAsync(TContext ctx, CancellationToken ct)
    {
        ctx.PipelineStartedAt ??= DateTime.UtcNow;

        for (var attempt = ctx.AttemptNumber; attempt <= _retryOptions.MaxRetries + 1; attempt++)
        {
            ctx.AttemptNumber = attempt;

            if (attempt > 1)
            {
                var delay = _retryOptions.GetDelay(attempt - 1);
                logger.LogInformation(
                    "[{Worker}-004] Retry {Attempt}/{Max} after {Delay}s. Failures: {Failures}. Steps: {StepSummary}. CorrelationId: {CorrelationId}",
                    WorkerName, attempt, _retryOptions.MaxRetries + 1, delay.TotalSeconds,
                    ctx.TotalFailures, FormatStepSummary(ctx), ctx.CorrelationId);
                await Task.Delay(delay, ct);
            }

            try
            {
                await ProcessAsync(ctx, ct);
                ctx.PipelineCompletedAt = DateTime.UtcNow;
                healthTracker.RecordActivity(WorkerName);

                logger.LogInformation(
                    "[{Worker}-010] Pipeline complete. Attempt: {Attempt}. Duration: {DurationMs}ms. Steps: {StepSummary}. CorrelationId: {CorrelationId}",
                    WorkerName, attempt, ctx.PipelineDurationMs, FormatStepSummary(ctx), ctx.CorrelationId);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ctx.TotalFailures++;
                ctx.LastFailedAt = DateTime.UtcNow;

                logger.LogWarning(ex,
                    "[{Worker}-005] Attempt {Attempt} failed after {DurationMs}ms. Failures: {Failures}. Steps: {StepSummary}. CorrelationId: {CorrelationId}",
                    WorkerName, attempt, (DateTime.UtcNow - ctx.PipelineStartedAt!.Value).TotalMilliseconds,
                    ctx.TotalFailures, FormatStepSummary(ctx), ctx.CorrelationId);
            }
        }

        // All retries exhausted
        ctx.PipelineCompletedAt = DateTime.UtcNow;
        logger.LogError(
            "[{Worker}-006] Permanently failed after {Attempts} attempts, {Failures} failures, {DurationMs}ms. Steps: {StepSummary}. CorrelationId: {CorrelationId}",
            WorkerName, ctx.AttemptNumber, ctx.TotalFailures, ctx.PipelineDurationMs,
            FormatStepSummary(ctx), ctx.CorrelationId);

        try
        {
            await OnPermanentFailureAsync(ctx, new InvalidOperationException($"Pipeline permanently failed after {ctx.TotalFailures} failures"), ct);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "[{Worker}-007] Dead letter write ALSO failed. CorrelationId: {CorrelationId}",
                WorkerName, ctx.CorrelationId);
        }
    }

    /// <summary>
    /// Run a named step only if it hasn't already completed (checkpoint/resume).
    /// </summary>
    protected async Task RunStepAsync(TContext ctx, string stepName, Func<Task> action, CancellationToken ct)
    {
        var step = ctx.GetOrCreateStep(stepName);

        if (step.Status == PipelineStepStatus.Completed)
        {
            logger.LogInformation("[{Worker}] Skipping '{Step}' — already completed ({DurationMs}ms). CorrelationId: {CorrelationId}",
                WorkerName, stepName, step.DurationMs, ctx.CorrelationId);
            return;
        }

        if (step.Status == PipelineStepStatus.PartiallyCompleted)
        {
            logger.LogInformation("[{Worker}] Resuming '{Step}' — partially completed ({SubSteps} sub-steps done). CorrelationId: {CorrelationId}",
                WorkerName, stepName, step.CompletedSubSteps.Count, ctx.CorrelationId);
        }

        step.StartedAt ??= DateTime.UtcNow;
        step.Status = PipelineStepStatus.InProgress;

        try
        {
            await action();
            step.Status = PipelineStepStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;

            logger.LogInformation("[{Worker}] Step '{Step}' completed in {DurationMs}ms. CorrelationId: {CorrelationId}",
                WorkerName, stepName, step.DurationMs, ctx.CorrelationId);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            step.Status = step.CompletedSubSteps.Count > 0
                ? PipelineStepStatus.PartiallyCompleted
                : PipelineStepStatus.Failed;
            step.CompletedAt = DateTime.UtcNow;
            step.Error = ex.Message;
            step.ErrorHistory.Add(new ErrorEntry(ctx.AttemptNumber, DateTime.UtcNow, stepName, ex.Message, ex.StackTrace));

            logger.LogError(ex, "[{Worker}] Step '{Step}' {Status} after {DurationMs}ms. SubSteps done: {SubSteps}. CorrelationId: {CorrelationId}",
                WorkerName, stepName, step.Status, step.DurationMs, string.Join(",", step.CompletedSubSteps), ctx.CorrelationId);
            throw;
        }
    }

    private static string FormatStepSummary(TContext ctx) =>
        string.Join(", ", ctx.Steps.Values.Select(s =>
            $"{s.Name}:{s.Status}({s.DurationMs?.ToString("F0") ?? "?"}ms)"));
}
