using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.WhatsApp.Webhook.ReceiveWebhook;

public class ReceiveWebhookEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/webhooks/whatsapp", async (
            HttpRequest request,
            [FromServices] IConfiguration config,
            [FromServices] WhatsAppIdempotencyStore idempotencyStore,
            [FromServices] IWebhookQueueService queue,
            [FromServices] IWhatsAppAuditService audit,
            [FromServices] ILogger<ReceiveWebhookEndpoint> logger,
            CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(ct);
            var signature = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
            var appSecret = config["WhatsApp:AppSecret"]!;

            return await Handle(rawBody, signature, appSecret, idempotencyStore, queue, audit, ct);
        }).DisableRateLimiting();

    internal static async Task<IResult> Handle(
        string rawBody,
        string? signature,
        string appSecret,
        WhatsAppIdempotencyStore idempotencyStore,
        IWebhookQueueService queue,
        IWhatsAppAuditService audit,
        CancellationToken ct)
    {
        // Step 1: Validate HMAC-SHA256 signature
        if (!IsSignatureValid(rawBody, signature, appSecret))
            return Results.Unauthorized();

        // Step 2: Deserialize payload
        WebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WebhookPayload>(rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            // Bad payload but we still return 200 per Meta requirements
            return Results.Ok();
        }

        if (payload is null)
            return Results.Ok();

        // Step 3: Status update — log [WA-012] and return 200 (no enqueue)
        var status = payload.GetFirstStatus();
        if (status is not null)
        {
            // [WA-012] delivery receipt — acknowledged, not processed
            return Results.Ok();
        }

        // Step 4: Extract message
        var message = payload.GetFirstMessage();
        if (message is null)
            return Results.Ok();

        // Step 5: Idempotency check
        if (idempotencyStore.IsProcessed(message.Id))
        {
            // [WA-002] duplicate message id — skipping
            return Results.Ok();
        }

        var phoneNumberId = payload.GetPhoneNumberId() ?? "";
        var body = message.Text?.Body ?? "";

        // Step 6: Audit before enqueue
        await audit.RecordReceivedAsync(
            message.Id,
            message.From,
            phoneNumberId,
            body,
            message.Type,
            ct);

        // Step 7: Mark processed then enqueue
        idempotencyStore.MarkProcessed(message.Id);

        var envelope = new WebhookEnvelope(
            MessageId: message.Id,
            FromPhone: message.From,
            Body: body,
            ReceivedAt: DateTime.UtcNow,
            PhoneNumberId: phoneNumberId,
            TraceId: Activity.Current?.TraceId.ToString());

        await queue.EnqueueAsync(envelope, ct);

        // Step 8: Always return 200 (Meta requirement)
        return Results.Ok();
    }

    private static bool IsSignatureValid(string rawBody, string? signature, string appSecret)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        // Expected format: "sha256=<hex>"
        const string prefix = "sha256=";
        if (!signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var hexPart = signature[prefix.Length..];

        byte[] receivedHash;
        try
        {
            receivedHash = Convert.FromHexString(hexPart);
        }
        catch (FormatException)
        {
            return false;
        }

        var key = Encoding.UTF8.GetBytes(appSecret);
        var data = Encoding.UTF8.GetBytes(rawBody);
        var expectedHash = HMACSHA256.HashData(key, data);

        return CryptographicOperations.FixedTimeEquals(receivedHash, expectedHash);
    }
}
