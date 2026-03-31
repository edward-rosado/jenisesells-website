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
    public async Task EnqueueAsync(LeadOrchestrationMessage message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message);
        await queueClient.SendMessageAsync(json, ct);

        logger.LogInformation(
            "[QUEUE-010] Lead orchestration request enqueued. AgentId={AgentId}, LeadId={LeadId}, CorrelationId={CorrelationId}",
            message.AgentId, message.LeadId, message.CorrelationId);
    }

    public async Task<QueueMessage<LeadOrchestrationMessage>?> DequeueAsync(
        TimeSpan visibilityTimeout,
        CancellationToken ct)
    {
        var response = await queueClient.ReceiveMessageAsync(visibilityTimeout, ct);
        var message = response?.Value;

        if (message is null)
            return null;

        try
        {
            var request = JsonSerializer.Deserialize<LeadOrchestrationMessage>(message.Body.ToString())
                ?? throw new JsonException("Deserialized LeadOrchestrationMessage was null");

            logger.LogInformation(
                "[QUEUE-011] Lead orchestration request dequeued. MessageId={MessageId}, AgentId={AgentId}, LeadId={LeadId}",
                message.MessageId, request.AgentId, request.LeadId);

            return new QueueMessage<LeadOrchestrationMessage>(request, message.MessageId, message.PopReceipt);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "[QUEUE-012] Failed to deserialize lead queue message. MessageId={MessageId}",
                message.MessageId);

            // Delete the poison message so it doesn't block the queue
            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
            return null;
        }
    }

    public async Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct)
    {
        await queueClient.DeleteMessageAsync(messageId, popReceipt, ct);

        logger.LogInformation("[QUEUE-013] Lead message completed. MessageId={MessageId}", messageId);
    }
}
