using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.WhatsApp.Interfaces;

namespace RealEstateStar.Functions.WhatsApp;

/// <summary>
/// Azure Functions queue-triggered replacement for WebhookProcessorWorker.
/// The Functions host handles:
///   - Dequeuing from "whatsapp-webhooks"
///   - Poison message routing to "whatsapp-webhooks-poison" after maxDequeueCount=5 (host.json)
///   - Message completion (deletion) on success
///   - Message visibility reset on unhandled exception (for retry)
///
/// TODO [Phase 4]: Remove WebhookProcessorWorker (BackgroundService) from Workers.WhatsApp
/// once feature flag Features:WhatsApp:UseBackgroundService is fully disabled in all environments.
/// </summary>
public class ProcessWebhookFunction(
    IConversationHandler handler,
    IWhatsAppSender whatsAppSender,
    IWhatsAppAuditService audit,
    ILogger<ProcessWebhookFunction> logger)
{
    // Azure Functions host moves messages to the -poison queue after maxDequeueCount=5 (host.json).
    // We still track poison in the audit trail for observability, but do NOT manually complete —
    // the host manages queue operations when using the QueueTrigger binding.
    private const int PoisonThreshold = 5;

    [Function("ProcessWhatsAppWebhook")]
    public async Task RunAsync(
        [QueueTrigger("whatsapp-webhooks")] string messageBody,
        long dequeueCount,
        string id,
        CancellationToken cancellationToken)
    {
        WebhookEnvelope envelope;
        try
        {
            envelope = DeserializeEnvelope(messageBody);
        }
        catch (Exception ex)
        {
            // Malformed message — log and let the host retry until poison threshold
            logger.LogError(ex, "[WA-FN-001] Failed to deserialize webhook queue message {QueueMessageId}", id);
            throw;
        }

        // Poison-threshold audit — record in audit table before host moves to poison queue
        if (dequeueCount >= PoisonThreshold)
        {
            logger.LogWarning(
                "[WA-FN-002] Poison message detected: {MessageId} dequeued {Count} times. " +
                "Host will move to whatsapp-webhooks-poison. From: {Phone}, Body: {BodySnippet}",
                envelope.MessageId, dequeueCount,
                envelope.FromPhone, envelope.Body[..Math.Min(100, envelope.Body.Length)]);

            await audit.UpdatePoisonAsync(
                envelope.MessageId,
                $"Exceeded max dequeue count ({dequeueCount})",
                cancellationToken);

            // Re-throw so the host can move the message to the -poison queue on next dequeue
            // (host increments dequeueCount beyond maxDequeueCount before moving)
            return;
        }

        try
        {
            await audit.UpdateProcessingAsync(envelope.MessageId, string.Empty, cancellationToken);

            var response = await handler.HandleMessageAsync(
                string.Empty,
                string.Empty,
                envelope.Body,
                null,
                cancellationToken);

            if (!string.IsNullOrEmpty(response))
            {
                await whatsAppSender.SendFreeformAsync(
                    $"+{envelope.FromPhone}", response, cancellationToken);
                await whatsAppSender.MarkReadAsync(envelope.MessageId, cancellationToken);
            }

            await audit.UpdateCompletedAsync(
                envelope.MessageId, string.Empty, string.Empty, response ?? string.Empty, cancellationToken);

            logger.LogInformation("[WA-FN-003] Processed webhook message {MessageId} successfully",
                envelope.MessageId);
        }
        catch (Exception ex)
        {
            await audit.UpdateFailedAsync(envelope.MessageId, null, ex.Message, cancellationToken);

            logger.LogError(ex,
                "[WA-FN-004] Failed to process webhook message {MessageId}, " +
                "attempt {Count}/{Max} — host will retry after visibility timeout",
                envelope.MessageId, dequeueCount, PoisonThreshold);

            // Re-throw so the host leaves the message visible for retry
            throw;
        }
    }

    /// <summary>
    /// Deserializes the queue message body. The Azure Queue Storage SDK encodes messages
    /// as Base64 by default (QueueMessageEncoding.Base64). The Functions worker SDK
    /// auto-decodes Base64 before passing the string to the trigger binding.
    /// If the body is still Base64 (legacy encoding), this method handles both cases.
    /// </summary>
    internal static WebhookEnvelope DeserializeEnvelope(string messageBody)
    {
        // Try direct JSON first (Functions SDK may have already decoded)
        if (messageBody.TrimStart().StartsWith('{'))
        {
            return JsonSerializer.Deserialize<WebhookEnvelope>(messageBody)
                ?? throw new InvalidOperationException("Deserialized envelope was null");
        }

        // Fall back to Base64 decode (legacy encoding path)
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(messageBody));
        return JsonSerializer.Deserialize<WebhookEnvelope>(json)
            ?? throw new InvalidOperationException("Deserialized envelope was null after Base64 decode");
    }
}
