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
    public async Task EnqueueAsync(ActivationRequest request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request);
        await queueClient.SendMessageAsync(json, ct);

        logger.LogInformation(
            "[QUEUE-001] Activation request enqueued for accountId={AccountId}, agentId={AgentId}",
            request.AccountId, request.AgentId);
    }

    public async Task<QueueMessage<ActivationRequest>?> DequeueAsync(
        TimeSpan visibilityTimeout,
        CancellationToken ct)
    {
        var response = await queueClient.ReceiveMessageAsync(visibilityTimeout, ct);
        var message = response?.Value;

        if (message is null)
            return null;

        try
        {
            var request = JsonSerializer.Deserialize<ActivationRequest>(message.Body.ToString())
                ?? throw new JsonException("Deserialized ActivationRequest was null");

            logger.LogInformation(
                "[QUEUE-002] Activation request dequeued. MessageId={MessageId}, AccountId={AccountId}, AgentId={AgentId}",
                message.MessageId, request.AccountId, request.AgentId);

            return new QueueMessage<ActivationRequest>(request, message.MessageId, message.PopReceipt);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "[QUEUE-003] Failed to deserialize activation queue message. MessageId={MessageId}",
                message.MessageId);

            // Delete the poison message so it doesn't block the queue
            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
            return null;
        }
    }

    public async Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct)
    {
        await queueClient.DeleteMessageAsync(messageId, popReceipt, ct);

        logger.LogInformation("[QUEUE-004] Activation message completed. MessageId={MessageId}", messageId);
    }
}
