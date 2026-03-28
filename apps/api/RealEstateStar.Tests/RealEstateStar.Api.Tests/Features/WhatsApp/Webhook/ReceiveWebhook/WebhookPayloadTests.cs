using Xunit;
using Moq;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using System.Text.Json;
using FluentAssertions;
using RealEstateStar.Api.Features.WhatsApp.Webhook.ReceiveWebhook;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Webhook.ReceiveWebhook;

public class WebhookPayloadTests
{
    private const string SamplePayload = """
    {
      "object": "whatsapp_business_account",
      "entry": [{
        "id": "WABA_ID",
        "changes": [{
          "value": {
            "messaging_product": "whatsapp",
            "metadata": {
              "phone_number_id": "PHONE_ID",
              "display_phone_number": "+15551234567"
            },
            "messages": [{
              "id": "wamid.abc123",
              "from": "12015551234",
              "timestamp": "1700000000",
              "type": "text",
              "text": { "body": "What's her budget?" }
            }]
          },
          "field": "messages"
        }]
      }]
    }
    """;

    [Fact]
    public void Deserializes_TextMessage_FromMetaPayload()
    {
        var payload = JsonSerializer.Deserialize<WebhookPayload>(SamplePayload);
        payload.Should().NotBeNull();
        payload!.Object.Should().Be("whatsapp_business_account");
        var message = payload.GetFirstMessage();
        message.Should().NotBeNull();
        message!.Id.Should().Be("wamid.abc123");
        message.From.Should().Be("12015551234");
        message.Type.Should().Be("text");
        message.Text!.Body.Should().Be("What's her budget?");
    }

    [Fact]
    public void GetFirstMessage_ReturnsNull_WhenNoMessages()
    {
        var json = """{"object":"whatsapp_business_account","entry":[{"id":"x","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"phone_number_id":"x","display_phone_number":"x"},"statuses":[{"id":"wamid.1","status":"delivered","timestamp":"1700000000","recipient_id":"12345"}]},"field":"messages"}]}]}""";
        var payload = JsonSerializer.Deserialize<WebhookPayload>(json);
        payload!.GetFirstMessage().Should().BeNull();
    }

    [Fact]
    public void GetPhoneNumberId_ReturnsMetadataValue()
    {
        var payload = JsonSerializer.Deserialize<WebhookPayload>(SamplePayload);
        payload!.GetPhoneNumberId().Should().Be("PHONE_ID");
    }

    [Fact]
    public void GetFirstStatus_ReturnsStatus_WhenPresent()
    {
        var json = """{"object":"whatsapp_business_account","entry":[{"id":"x","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"phone_number_id":"x","display_phone_number":"x"},"statuses":[{"id":"wamid.1","status":"delivered","timestamp":"1700000000","recipient_id":"12345"}]},"field":"messages"}]}]}""";
        var payload = JsonSerializer.Deserialize<WebhookPayload>(json);
        var status = payload!.GetFirstStatus();
        status.Should().NotBeNull();
        status!.Status.Should().Be("delivered");
        status.Id.Should().Be("wamid.1");
        status.RecipientId.Should().Be("12345");
    }

    [Fact]
    public void GetFirstStatus_ReturnsNull_WhenNoStatuses()
    {
        var payload = JsonSerializer.Deserialize<WebhookPayload>(SamplePayload);
        payload!.GetFirstStatus().Should().BeNull();
    }

    [Fact]
    public void GetFirstMessage_ReturnsNull_WhenEntryListIsEmpty()
    {
        var payload = new WebhookPayload(); // Entry defaults to []
        payload.GetFirstMessage().Should().BeNull();
    }

    [Fact]
    public void GetFirstStatus_ReturnsNull_WhenEntryListIsEmpty()
    {
        var payload = new WebhookPayload();
        payload.GetFirstStatus().Should().BeNull();
    }

    [Fact]
    public void GetPhoneNumberId_ReturnsNull_WhenEntryListIsEmpty()
    {
        var payload = new WebhookPayload();
        payload.GetPhoneNumberId().Should().BeNull();
    }

    [Fact]
    public void GetFirstMessage_ReturnsNull_WhenChangesListIsEmpty()
    {
        var payload = new WebhookPayload
        {
            Entry = [new WebhookEntry()] // Changes defaults to []
        };
        payload.GetFirstMessage().Should().BeNull();
    }

    [Fact]
    public void GetFirstStatus_ReturnsNull_WhenChangesListIsEmpty()
    {
        var payload = new WebhookPayload
        {
            Entry = [new WebhookEntry()]
        };
        payload.GetFirstStatus().Should().BeNull();
    }

    [Fact]
    public void GetPhoneNumberId_ReturnsNull_WhenChangesListIsEmpty()
    {
        var payload = new WebhookPayload
        {
            Entry = [new WebhookEntry()]
        };
        payload.GetPhoneNumberId().Should().BeNull();
    }

    [Fact]
    public void GetPhoneNumberId_ReturnsNull_WhenMetadataIsNull()
    {
        var payload = new WebhookPayload
        {
            Entry =
            [
                new WebhookEntry
                {
                    Changes = [new WebhookChange { Value = new WebhookValue { Metadata = null } }]
                }
            ]
        };
        payload.GetPhoneNumberId().Should().BeNull();
    }
}
