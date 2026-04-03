using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.WhatsApp.Interfaces;

namespace RealEstateStar.Workers.WhatsApp;

/// <summary>
/// BackgroundService that polls the Azure Storage Queue for inbound WhatsApp webhook messages.
///
/// TODO [Phase 4]: Remove this class entirely once ProcessWebhookFunction (Azure Functions
/// queue-triggered) is proven stable. The feature flag Features:WhatsApp:UseBackgroundService
/// controls whether this service is registered as a HostedService. Set it to false first,
/// then remove this file after a safe observation period.
/// </summary>
public class WebhookProcessorWorker(
    IWebhookQueueService queue,
    IConversationHandler handler,
    IWhatsAppSender whatsAppClient,
    IWhatsAppAuditService audit,
    ILogger<WebhookProcessorWorker> logger) : BackgroundService
{
    private const int MaxDequeueCount = 5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan EmptyQueueDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[WA-020] WebhookProcessorWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessOnceAsync(stoppingToken);
                // If queue was empty, wait longer before polling again
                await Task.Delay(processed ? PollInterval : EmptyQueueDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[WA-021] Worker loop error, retrying");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Processes a single message from the queue. Returns true if a message was found.
    /// Exposed as internal for testability.
    /// </summary>
    internal async Task<bool> ProcessOnceAsync(CancellationToken ct)
    {
        var queued = await queue.DequeueAsync(ct);
        if (queued is null) return false;

        var envelope = queued.Value;

        // Poison message detection — too many failures, give up
        if (queued.DequeueCount > MaxDequeueCount)
        {
            logger.LogWarning("[WA-017] Poison message detected: {MessageId} " +
                "dequeued {Count} times, removing. From: {Phone}, Body: {Body}",
                envelope.MessageId, queued.DequeueCount,
                envelope.FromPhone, envelope.Body[..Math.Min(100, envelope.Body.Length)]);
            await audit.UpdatePoisonAsync(envelope.MessageId,
                $"Exceeded max dequeue count ({queued.DequeueCount})", ct);
            await queue.CompleteAsync(queued.QueueMessageId, queued.PopReceipt, ct);
            return true;
        }

        try
        {
            // Audit: mark processing started
            // agentId resolution is deferred to Phase 3 (IConversationHandler, Task 11)
            await audit.UpdateProcessingAsync(envelope.MessageId, "", ct);

            // Route to conversation handler (agentId/firstName resolved in Task 11)
            var response = await handler.HandleMessageAsync(
                "",
                "",
                envelope.Body,
                null,
                ct);

            // Send response back via WhatsApp (within 24hr window = freeform)
            if (!string.IsNullOrEmpty(response))
            {
                await whatsAppClient.SendFreeformAsync(
                    $"+{envelope.FromPhone}", response, ct);
                await whatsAppClient.MarkReadAsync(envelope.MessageId, ct);
            }

            // Audit: mark completed
            await audit.UpdateCompletedAsync(envelope.MessageId, "", "", response ?? "", ct);

            // Success — remove from queue
            await queue.CompleteAsync(queued.QueueMessageId, queued.PopReceipt, ct);
            logger.LogInformation("[WA-022] Processed message {MessageId} successfully",
                envelope.MessageId);
        }
        catch (Exception ex)
        {
            // Audit: mark failed
            await audit.UpdateFailedAsync(envelope.MessageId, null, ex.Message, ct);

            // Do NOT delete from queue — message becomes visible again after
            // visibility timeout (30s) for automatic retry
            logger.LogError(ex, "[WA-006] Failed to process message {MessageId}, " +
                "attempt {Count}/{Max} — will retry after visibility timeout",
                envelope.MessageId, queued.DequeueCount, MaxDequeueCount);
        }

        return true;
    }
}
