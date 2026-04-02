using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.Clients.Azure;

public sealed class AzureQueueLeadStore(
    QueueClient queueClient,
    ILogger<AzureQueueLeadStore> logger) : ILeadOrchestrationQueue
{
    internal const string QueueName = "lead-requests";

    // Azure Queue Storage approximate message count is eventually consistent and requires
    // an extra API call — return 0 here; health checks use this for staleness detection,
    // not for exact counts, so eventually-consistent data is acceptable.
    public int QueueDepth => 0;

    public async Task EnqueueAsync(LeadOrchestrationMessage message, CancellationToken ct)
    {
        using var activity = QueueDiagnostics.StartEnqueue(QueueName);
        try
        {
            var json = JsonSerializer.Serialize(message);
            var response = await queueClient.SendMessageAsync(json, ct);

            QueueDiagnostics.RecordEnqueue(QueueName, response.Value.MessageId, activity);

            logger.LogInformation(
                "[QUEUE-001] Lead orchestration request enqueued. AgentId={AgentId}, LeadId={LeadId}, CorrelationId={CorrelationId}",
                message.AgentId, message.LeadId, message.CorrelationId);
        }
        catch (Exception ex)
        {
            QueueDiagnostics.RecordFailure(QueueName, activity, ex);

            logger.LogError(ex,
                "[QUEUE-010] Failed to enqueue lead orchestration request. AgentId={AgentId}, LeadId={LeadId}",
                message.AgentId, message.LeadId);

            throw;
        }
    }

    public async Task<QueueMessage<LeadOrchestrationMessage>?> DequeueAsync(
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
                logger.LogDebug("[QUEUE-002] Lead queue empty — no messages to dequeue");
                return null;
            }

            try
            {
                var request = JsonSerializer.Deserialize<LeadOrchestrationMessage>(message.Body.ToString())
                    ?? throw new JsonException("Deserialized LeadOrchestrationMessage was null");

                activity?.SetTag("message.id", message.MessageId);

                logger.LogInformation(
                    "[QUEUE-002] Lead orchestration request dequeued. MessageId={MessageId}, AgentId={AgentId}, LeadId={LeadId}",
                    message.MessageId, request.AgentId, request.LeadId);

                return new QueueMessage<LeadOrchestrationMessage>(request, message.MessageId, message.PopReceipt);
            }
            catch (JsonException ex)
            {
                QueueDiagnostics.RecordFailure(QueueName, activity, ex);

                logger.LogError(ex,
                    "[QUEUE-011] Failed to deserialize lead queue message. MessageId={MessageId}",
                    message.MessageId);

                // Delete the poison message so it doesn't block the queue
                await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
                return null;
            }
        }
        catch (Exception ex) when (ex is not JsonException)
        {
            QueueDiagnostics.RecordFailure(QueueName, activity, ex);

            logger.LogError(ex, "[QUEUE-011] Failed to dequeue lead orchestration request");
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

            logger.LogInformation("[QUEUE-003] Lead message completed. MessageId={MessageId}", messageId);
        }
        catch (Exception ex)
        {
            QueueDiagnostics.RecordFailure(QueueName, activity, ex);

            logger.LogError(ex,
                "[QUEUE-012] Failed to complete lead message. MessageId={MessageId}", messageId);

            throw;
        }
    }
}
