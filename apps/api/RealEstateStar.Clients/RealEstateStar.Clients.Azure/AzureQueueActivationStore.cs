using System.Diagnostics;
using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Clients.Azure;

public sealed class AzureQueueActivationStore(
    QueueClient queueClient,
    ILogger<AzureQueueActivationStore> logger) : IActivationQueue
{
    internal const string QueueName = "activation-requests";

    public async Task EnqueueAsync(ActivationRequest request, CancellationToken ct)
    {
        using var activity = QueueDiagnostics.StartEnqueue(QueueName);
        try
        {
            var json = JsonSerializer.Serialize(request);
            var response = await queueClient.SendMessageAsync(json, ct);

            QueueDiagnostics.RecordEnqueue(QueueName, response.Value.MessageId, activity);

            logger.LogInformation(
                "[QUEUE-001] Activation request enqueued for accountId={AccountId}, agentId={AgentId}",
                request.AccountId, request.AgentId);
        }
        catch (Exception ex)
        {
            QueueDiagnostics.RecordFailure(QueueName, activity, ex);

            logger.LogError(ex,
                "[QUEUE-010] Failed to enqueue activation request. AccountId={AccountId}, AgentId={AgentId}",
                request.AccountId, request.AgentId);

            throw;
        }
    }

    public async Task<QueueMessage<ActivationRequest>?> DequeueAsync(
        TimeSpan visibilityTimeout,
        CancellationToken ct)
    {
        using var activity = QueueDiagnostics.StartDequeue(QueueName);
        try
        {
            var response = await queueClient.ReceiveMessageAsync(visibilityTimeout, ct);
            var message = response?.Value;

            if (message is null)
            {
                logger.LogDebug("[QUEUE-002] Activation queue empty — no messages to dequeue");
                return null;
            }

            try
            {
                var request = JsonSerializer.Deserialize<ActivationRequest>(message.Body.ToString())
                    ?? throw new JsonException("Deserialized ActivationRequest was null");

                activity?.SetTag("message.id", message.MessageId);

                logger.LogInformation(
                    "[QUEUE-002] Activation request dequeued. MessageId={MessageId}, AccountId={AccountId}, AgentId={AgentId}",
                    message.MessageId, request.AccountId, request.AgentId);

                return new QueueMessage<ActivationRequest>(request, message.MessageId, message.PopReceipt);
            }
            catch (JsonException ex)
            {
                QueueDiagnostics.RecordFailure(QueueName, activity, ex);

                logger.LogError(ex,
                    "[QUEUE-003] Failed to deserialize activation queue message. MessageId={MessageId}",
                    message.MessageId);

                // Delete the poison message so it doesn't block the queue
                await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
                return null;
            }
        }
        catch (Exception ex) when (ex is not JsonException)
        {
            QueueDiagnostics.RecordFailure(QueueName, activity, ex);

            logger.LogError(ex, "[QUEUE-011] Failed to dequeue activation request");
            throw;
        }
    }

    public async Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct)
    {
        using var activity = QueueDiagnostics.StartComplete(QueueName);
        try
        {
            await queueClient.DeleteMessageAsync(messageId, popReceipt, ct);

            QueueDiagnostics.RecordComplete(QueueName, messageId, activity);

            logger.LogInformation("[QUEUE-003] Activation message completed. MessageId={MessageId}", messageId);
        }
        catch (Exception ex)
        {
            QueueDiagnostics.RecordFailure(QueueName, activity, ex);

            logger.LogError(ex,
                "[QUEUE-012] Failed to complete activation message. MessageId={MessageId}", messageId);

            throw;
        }
    }
}
