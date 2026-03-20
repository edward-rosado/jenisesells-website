using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using RealEstateStar.Api.Features.WhatsApp.Services;
using RealEstateStar.Api.Features.WhatsApp.Webhook.ReceiveWebhook;
using RealEstateStar.Api.Tests.TestHelpers;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Webhook.ReceiveWebhook;

public class ReceiveWebhookEndpointTests
{
    private const string AppSecret = "test-app-secret-12345";

    private static WhatsAppIdempotencyStore FreshStore() =>
        new(new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }));

    private static string ComputeSignature(string body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(body);
        var hash = HMACSHA256.HashData(key, data);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildMessagePayload(string messageId = "wamid.abc123",
        string from = "15551234567",
        string body = "Hello agent!")
    {
        var payload = new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    id = "ENTRY_ID",
                    changes = new[]
                    {
                        new
                        {
                            value = new
                            {
                                messaging_product = "whatsapp",
                                metadata = new { phone_number_id = "PN_ID_1", display_phone_number = "15559876543" },
                                messages = new[]
                                {
                                    new
                                    {
                                        id = messageId,
                                        from,
                                        timestamp = "1710000000",
                                        type = "text",
                                        text = new { body }
                                    }
                                }
                            },
                            field = "messages"
                        }
                    }
                }
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string BuildStatusPayload(string statusId = "wamid.status123")
    {
        var payload = new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    id = "ENTRY_ID",
                    changes = new[]
                    {
                        new
                        {
                            value = new
                            {
                                messaging_product = "whatsapp",
                                metadata = new { phone_number_id = "PN_ID_1", display_phone_number = "15559876543" },
                                statuses = new[]
                                {
                                    new
                                    {
                                        id = statusId,
                                        status = "delivered",
                                        timestamp = "1710000001",
                                        recipient_id = "15551234567"
                                    }
                                }
                            },
                            field = "messages"
                        }
                    }
                }
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    [Fact]
    public async Task Handle_ValidSignature_Returns200_AndEnqueuesMessage()
    {
        var rawBody = BuildMessagePayload();
        var signature = ComputeSignature(rawBody, AppSecret);
        var queue = new InMemoryWebhookQueueService();
        var audit = new Mock<IWhatsAppAuditService>();
        var store = FreshStore();
        var ct = CancellationToken.None;

        var result = await ReceiveWebhookEndpoint.Handle(
            rawBody, signature, AppSecret, store, queue, audit.Object, ct);

        result.Should().BeOfType<Ok>();
        queue.Enqueued.Should().HaveCount(1);
        queue.Enqueued[0].MessageId.Should().Be("wamid.abc123");
        queue.Enqueued[0].FromPhone.Should().Be("15551234567");
        queue.Enqueued[0].Body.Should().Be("Hello agent!");
    }

    [Fact]
    public async Task Handle_InvalidSignature_Returns401()
    {
        var rawBody = BuildMessagePayload();
        var queue = new InMemoryWebhookQueueService();
        var audit = new Mock<IWhatsAppAuditService>();
        var store = FreshStore();
        var ct = CancellationToken.None;

        var result = await ReceiveWebhookEndpoint.Handle(
            rawBody, "sha256=deadbeefdeadbeef", AppSecret, store, queue, audit.Object, ct);

        result.Should().BeOfType<UnauthorizedHttpResult>();
        queue.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MissingSignature_Returns401()
    {
        var rawBody = BuildMessagePayload();
        var queue = new InMemoryWebhookQueueService();
        var audit = new Mock<IWhatsAppAuditService>();
        var store = FreshStore();
        var ct = CancellationToken.None;

        var result = await ReceiveWebhookEndpoint.Handle(
            rawBody, null, AppSecret, store, queue, audit.Object, ct);

        result.Should().BeOfType<UnauthorizedHttpResult>();
        queue.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DuplicateMessageId_Returns200_NoReprocessing()
    {
        var rawBody = BuildMessagePayload(messageId: "wamid.dup999");
        var signature = ComputeSignature(rawBody, AppSecret);
        var queue = new InMemoryWebhookQueueService();
        var audit = new Mock<IWhatsAppAuditService>();
        var store = FreshStore();
        var ct = CancellationToken.None;

        // First call
        await ReceiveWebhookEndpoint.Handle(
            rawBody, signature, AppSecret, store, queue, audit.Object, ct);

        // Second call — same message id
        var result = await ReceiveWebhookEndpoint.Handle(
            rawBody, signature, AppSecret, store, queue, audit.Object, ct);

        result.Should().BeOfType<Ok>();
        queue.Enqueued.Should().HaveCount(1, "duplicate should not be enqueued again");
    }

    [Fact]
    public async Task Handle_StatusUpdate_Returns200_NoProcessing()
    {
        var rawBody = BuildStatusPayload();
        var signature = ComputeSignature(rawBody, AppSecret);
        var queue = new InMemoryWebhookQueueService();
        var audit = new Mock<IWhatsAppAuditService>();
        var store = FreshStore();
        var ct = CancellationToken.None;

        var result = await ReceiveWebhookEndpoint.Handle(
            rawBody, signature, AppSecret, store, queue, audit.Object, ct);

        result.Should().BeOfType<Ok>();
        queue.Enqueued.Should().BeEmpty("status updates are not enqueued for processing");
        audit.Verify(a => a.RecordReceivedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
