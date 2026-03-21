# WhatsApp Agent Communication Channel — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add WhatsApp as a notification + conversational channel between Real Estate Star and its agent customers. Agents receive lead cards, CMA alerts, and follow-up reminders on WhatsApp and can reply to ask questions about leads.

**Architecture:** Outbound via Meta Graph API (template messages outside 24hr window, freeform within). Inbound via Meta webhook → intent classification (Claude Haiku) → scoped response generation → Drive conversation log. Plugs into existing `ILeadNotifier` multi-channel pattern from the lead submission API plan. All conversations logged to the lead's Google Drive folder as human-readable markdown.

**Tech Stack:** .NET 10, Meta WhatsApp Cloud API (Graph API v20.0), Claude API (Haiku for classification, Sonnet for response synthesis), Google Drive (gws CLI), Azure Queue Storage (durable webhook processing), IMemoryCache, BackgroundService, OpenTelemetry

**Spec:** `docs/superpowers/specs/2026-03-19-whatsapp-agent-channel-design.md`

**Design deviation:** The spec shows `Send/SendTemplateMessage/` and `Send/SendFreeformMessage/` as internal endpoints. This plan routes all sends through `IWhatsAppClient` directly from services — no internal HTTP endpoints. These are never called externally, and adding endpoints for internal service calls would be over-engineering. If external send capability is needed later, we can add endpoints then.

**Prerequisite:** The [Lead Submission API plan](2026-03-19-lead-submission-api.md) must be completed first. This plan depends on:
- `ILeadNotifier` + `MultiChannelLeadNotifier` (Task 16) — WhatsApp is added as a third channel
- `ILeadStore` + `LeadPaths` (Task 8) — conversation logs live in lead folders
- `IGwsService` promoted to `Services/Gws/` (Task 0) — Drive writes for conversation logs
- `IFileStorageProvider` (Task 1) — storage abstraction for appending conversation logs
- Agent config schema updated with `notifications` placeholder (Task 37)
- `AgentConfig` + `AgentIntegrations` models in `Common/` — extended with WhatsApp fields

---

## Parallel Execution Map

Tasks are grouped into **work streams** that can execute in parallel. All depend on the lead submission API plan being complete.

```
Stream A: Config + Types (no internal deps)    Stream B: HTTP Client + Webhook Payloads
  Task 1: AgentWhatsApp model + schema           Task 3: WebhookPayload deserialization types
  Task 2: WhatsAppTypes + WhatsAppPaths           Task 4: IWhatsAppClient + WhatsAppClient
                                                  Task 4b: IWebhookQueueService (Azure Queue Storage)

         ↓ depends on A+B
Stream C: Core Services                        Stream D: Webhook Endpoints
  Task 5: WhatsAppIdempotencyStore               Task 8: VerifyWebhookEndpoint
  Task 5b: IWhatsAppAuditService (Table Storage) Task 9: ReceiveWebhookEndpoint (audit + enqueue)
  Task 6: ConversationLogRenderer                Task 9b: WebhookProcessorWorker (dequeue + audit + process)
  Task 7: IConversationLogger

         ↓ depends on C+D
Stream E: Notifier + Conversation              Stream F: Background Jobs
  Task 10: IWhatsAppNotifier                     Task 13: WhatsAppRetryJob
  Task 11: IConversationHandler (guardrails)
  Task 12: Response generation (agent voice)

         ↓ depends on E
Stream G: Integration
  Task 14: Wire into MultiChannelLeadNotifier
  Task 15: Onboarding tool (SendWhatsAppWelcome)
  Task 16: DI registration in Program.cs

         ↓ depends on all
Stream H: Observability + Infrastructure
  Task 17: WhatsAppDiagnostics (ActivitySource + Meters)
  Task 18: CI/CD + config updates
  Task 19: Integration tests + 100% coverage
```

### Durable Queue Architecture

```
Meta Webhook POST → ReceiveWebhookEndpoint
  │ validate HMAC signature
  │ idempotency check
  │ enqueue to Azure Queue Storage → return 200 OK immediately
  │
  ↓ (decoupled, durable)
WebhookProcessorWorker (BackgroundService)
  │ dequeue message (30s visibility timeout)
  │ deserialize + look up agent
  │ route to IConversationHandler
  │ on success → delete message from queue
  │ on failure → message becomes visible again after timeout
  │ after 5 failed attempts → moves to poison queue (whatsapp-webhook-poison)
  │
  ↓ (separate concern)
PoisonQueueMonitor (BackgroundService, runs every 10 min)
  │ checks whatsapp-webhook-poison queue
  │ logs [WA-017] with message details
  │ increments whatsapp.queue.poison counter
```

**Why Azure Queue Storage (not Service Bus):**
- ~$0.01/month at current scale (vs $10+/month for Service Bus)
- Built-in visibility timeout = automatic retry on failure
- Built-in dequeue count = poison message detection (no custom tracking)
- 7-day message retention = survives extended outages
- Already on Azure (Container Apps) — no new vendor

---

## Stream A: Config + Types (no internal dependencies)

### Task 1: AgentWhatsApp model + agent config schema

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Common/AgentConfig.cs`
- Modify: `config/agent.schema.json`
- Test: `apps/api/RealEstateStar.Api.Tests/Common/AgentWhatsAppTests.cs`

- [ ] **Step 1: Write tests for AgentWhatsApp deserialization**

```csharp
// AgentWhatsAppTests.cs
public class AgentWhatsAppTests
{
    [Fact]
    public void Deserializes_WhatsAppConfig_FromJson()
    {
        var json = """
        {
            "phone_number": "+12015551234",
            "opted_in": true,
            "notification_preferences": ["new_lead", "cma_ready", "data_deletion"],
            "status": "active",
            "welcome_sent": true,
            "retry_after": "2026-03-19T18:00:00Z"
        }
        """;
        var config = JsonSerializer.Deserialize<AgentWhatsApp>(json);
        config.Should().NotBeNull();
        config!.PhoneNumber.Should().Be("+12015551234");
        config.OptedIn.Should().BeTrue();
        config.NotificationPreferences.Should().Contain("new_lead");
        config.Status.Should().Be("active");
        config.WelcomeSent.Should().BeTrue();
        config.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public void Defaults_Status_ToNotRegistered()
    {
        var config = new AgentWhatsApp { PhoneNumber = "+12015551234", OptedIn = true };
        config.Status.Should().Be("not_registered");
        config.WelcomeSent.Should().BeFalse();
        config.RetryAfter.Should().BeNull();
    }

    [Fact]
    public void Defaults_NotificationPreferences_IncludesDataDeletion()
    {
        var config = new AgentWhatsApp { PhoneNumber = "+12015551234", OptedIn = true };
        config.NotificationPreferences.Should().Contain("data_deletion");
    }

    [Fact]
    public void AgentIntegrations_Deserializes_WithWhatsApp()
    {
        var json = """
        {
            "email_provider": "gmail",
            "whatsapp": {
                "phone_number": "+12015551234",
                "opted_in": true
            }
        }
        """;
        var integrations = JsonSerializer.Deserialize<AgentIntegrations>(json);
        integrations!.WhatsApp.Should().NotBeNull();
        integrations.WhatsApp!.PhoneNumber.Should().Be("+12015551234");
    }

    [Fact]
    public void AgentIntegrations_NullWhatsApp_WhenNotPresent()
    {
        var json = """{"email_provider": "gmail"}""";
        var integrations = JsonSerializer.Deserialize<AgentIntegrations>(json);
        integrations!.WhatsApp.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "AgentWhatsAppTests" -v n`
Expected: FAIL — `AgentWhatsApp` class does not exist.

- [ ] **Step 3: Add AgentWhatsApp class to AgentConfig.cs**

```csharp
// Add to AgentIntegrations class:
[JsonPropertyName("whatsapp")]
public AgentWhatsApp? WhatsApp { get; init; }

// New class in same file:
public class AgentWhatsApp
{
    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; init; } = "";

    [JsonPropertyName("opted_in")]
    public bool OptedIn { get; init; }

    [JsonPropertyName("notification_preferences")]
    public List<string> NotificationPreferences { get; init; } = ["new_lead", "cma_ready", "data_deletion"];

    [JsonPropertyName("status")]
    public string Status { get; set; } = "not_registered";

    [JsonPropertyName("welcome_sent")]
    public bool WelcomeSent { get; set; }

    [JsonPropertyName("retry_after")]
    public DateTime? RetryAfter { get; set; }
}
```

- [ ] **Step 4: Update config/agent.schema.json**

Add `whatsapp` object inside the existing `integrations.properties`:

```json
"whatsapp": {
  "type": "object",
  "properties": {
    "phone_number": {
      "type": "string",
      "pattern": "^\\+[1-9]\\d{1,14}$",
      "description": "Agent's WhatsApp number in E.164 format"
    },
    "opted_in": {
      "type": "boolean",
      "default": false
    },
    "notification_preferences": {
      "type": "array",
      "items": {
        "type": "string",
        "enum": ["new_lead", "cma_ready", "follow_up_reminder", "listing_alert", "data_deletion"]
      },
      "default": ["new_lead", "cma_ready", "data_deletion"]
    },
    "status": {
      "type": "string",
      "enum": ["active", "not_registered", "error"],
      "default": "not_registered"
    },
    "welcome_sent": {
      "type": "boolean",
      "default": false
    },
    "retry_after": {
      "type": "string",
      "format": "date-time"
    }
  },
  "required": ["phone_number", "opted_in"]
}
```

- [ ] **Step 5: Run tests, verify pass**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "AgentWhatsAppTests" -v n`
Expected: All 5 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/RealEstateStar.Api/Common/AgentConfig.cs config/agent.schema.json apps/api/RealEstateStar.Api.Tests/Common/AgentWhatsAppTests.cs
git commit -m "feat: add AgentWhatsApp model and schema for WhatsApp integration

Extends AgentIntegrations with WhatsApp config: phone number (E.164),
opt-in flag, notification preferences, delivery status tracking,
and scheduled retry support for late WhatsApp adoption."
```

---

### Task 2: WhatsAppTypes + WhatsAppPaths

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/WhatsAppTypes.cs`
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/WhatsAppPaths.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/WhatsAppPathsTests.cs`

- [ ] **Step 1: Write tests for WhatsAppPaths**

```csharp
// WhatsAppPathsTests.cs
public class WhatsAppPathsTests
{
    [Fact]
    public void LeadConversation_ReturnsCorrectPath()
    {
        var path = WhatsAppPaths.LeadConversation("Jane Doe");
        path.Should().Be("Real Estate Star/1 - Leads/Jane Doe/WhatsApp Conversation.md");
    }

    [Fact]
    public void GeneralConversation_IsCorrectConstant()
    {
        WhatsAppPaths.GeneralConversation.Should().Be("Real Estate Star/WhatsApp/General.md");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "WhatsAppPathsTests" -v n`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Create WhatsAppTypes.cs**

```csharp
// Features/WhatsApp/WhatsAppTypes.cs
using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.WhatsApp;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    NewLead,
    CmaReady,
    FollowUpReminder,
    ListingAlert,
    DataDeletion,
    Welcome
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageDirection
{
    Outbound,
    Inbound
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntentType
{
    LeadQuestion,
    ActionRequest,
    Acknowledge,
    Help,
    OutOfScope,
    Unknown
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutOfScopeCategory
{
    GeneralReQuestion,
    LegalFinancial,
    NonReTopic,
    NoLeadData,
    PromptInjection
}
```

- [ ] **Step 4: Create WhatsAppPaths.cs**

```csharp
// Features/WhatsApp/WhatsAppPaths.cs
namespace RealEstateStar.Api.Features.WhatsApp;

public static class WhatsAppPaths
{
    public static string LeadConversation(string leadName) =>
        $"{LeadPaths.LeadFolder(leadName)}/WhatsApp Conversation.md";

    public const string GeneralConversation =
        "Real Estate Star/WhatsApp/General.md";
}
```

Note: `LeadPaths` comes from the lead submission API plan (Task 8). It must exist before this task runs.

- [ ] **Step 5: Run tests, verify pass, commit**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "WhatsAppPathsTests" -v n`
Expected: PASS.

```bash
git add apps/api/RealEstateStar.Api/Features/WhatsApp/WhatsAppTypes.cs apps/api/RealEstateStar.Api/Features/WhatsApp/WhatsAppPaths.cs apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/WhatsAppPathsTests.cs
git commit -m "feat: add WhatsApp enums and Drive path constants

NotificationType, IntentType, OutOfScopeCategory enums.
WhatsAppPaths for per-lead conversation logs in Drive folders."
```

---

## Stream B: HTTP Client + Webhook Payloads (parallel with A)

### Task 3: WebhookPayload deserialization types

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Webhook/ReceiveWebhook/WebhookPayload.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Webhook/ReceiveWebhook/WebhookPayloadTests.cs`

- [ ] **Step 1: Write tests for Meta webhook payload deserialization**

```csharp
// WebhookPayloadTests.cs
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
        var json = """{"object":"whatsapp_business_account","entry":[{"id":"x","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"phone_number_id":"x","display_phone_number":"x"},"statuses":[]},"field":"messages"}]}]}""";
        var payload = JsonSerializer.Deserialize<WebhookPayload>(json);
        payload!.GetFirstMessage().Should().BeNull();
    }

    [Fact]
    public void GetPhoneNumberId_ReturnsMetadataValue()
    {
        var payload = JsonSerializer.Deserialize<WebhookPayload>(SamplePayload);
        payload!.GetPhoneNumberId().Should().Be("PHONE_ID");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "WebhookPayloadTests" -v n`
Expected: FAIL — `WebhookPayload` does not exist.

- [ ] **Step 3: Implement WebhookPayload.cs**

```csharp
// Features/WhatsApp/Webhook/ReceiveWebhook/WebhookPayload.cs
using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.WhatsApp.Webhook.ReceiveWebhook;

public class WebhookPayload
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = "";

    [JsonPropertyName("entry")]
    public List<WebhookEntry> Entry { get; init; } = [];

    public WebhookMessage? GetFirstMessage() =>
        Entry.FirstOrDefault()?.Changes.FirstOrDefault()?.Value.Messages?.FirstOrDefault();

    public WebhookStatus? GetFirstStatus() =>
        Entry.FirstOrDefault()?.Changes.FirstOrDefault()?.Value.Statuses?.FirstOrDefault();

    public string? GetPhoneNumberId() =>
        Entry.FirstOrDefault()?.Changes.FirstOrDefault()?.Value.Metadata?.PhoneNumberId;
}

public class WebhookEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("changes")]
    public List<WebhookChange> Changes { get; init; } = [];
}

public class WebhookChange
{
    [JsonPropertyName("value")]
    public WebhookValue Value { get; init; } = new();

    [JsonPropertyName("field")]
    public string Field { get; init; } = "";
}

public class WebhookValue
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; init; } = "";

    [JsonPropertyName("metadata")]
    public WebhookMetadata? Metadata { get; init; }

    [JsonPropertyName("messages")]
    public List<WebhookMessage>? Messages { get; init; }

    [JsonPropertyName("statuses")]
    public List<WebhookStatus>? Statuses { get; init; }
}

public class WebhookStatus
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = "";

    [JsonPropertyName("recipient_id")]
    public string RecipientId { get; init; } = "";
}

public class WebhookMetadata
{
    [JsonPropertyName("phone_number_id")]
    public string PhoneNumberId { get; init; } = "";

    [JsonPropertyName("display_phone_number")]
    public string DisplayPhoneNumber { get; init; } = "";
}

public class WebhookMessage
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("from")]
    public string From { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("text")]
    public WebhookText? Text { get; init; }
}

public class WebhookText
{
    [JsonPropertyName("body")]
    public string Body { get; init; } = "";
}
```

- [ ] **Step 4: Run tests, verify pass, commit**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "WebhookPayloadTests" -v n`
Expected: All 3 PASS.

```bash
git add apps/api/RealEstateStar.Api/Features/WhatsApp/Webhook/ReceiveWebhook/WebhookPayload.cs apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Webhook/ReceiveWebhook/WebhookPayloadTests.cs
git commit -m "feat: add Meta WhatsApp webhook payload deserialization types

Maps Meta's webhook JSON structure for inbound text messages
and status updates. Helper methods extract first message and
phone number ID from nested payload."
```

---

### Task 4: IWhatsAppClient + WhatsAppClient

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/IWhatsAppClient.cs`
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/WhatsAppClient.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/WhatsAppClientTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// WhatsAppClientTests.cs
public class WhatsAppClientTests
{
    private readonly Mock<IHttpClientFactory> _httpFactory = new();
    private readonly MockHttpMessageHandler _handler = new();
    private readonly WhatsAppClient _sut;

    public WhatsAppClientTests()
    {
        var httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://graph.facebook.com/v20.0/")
        };
        _httpFactory.Setup(f => f.CreateClient("WhatsApp")).Returns(httpClient);
        _sut = new WhatsAppClient(_httpFactory.Object, "PHONE_ID", "ACCESS_TOKEN",
            Mock.Of<ILogger<WhatsAppClient>>());
    }

    [Fact]
    public async Task SendTemplateAsync_PostsCorrectPayload()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"messaging_product":"whatsapp","messages":[{"id":"wamid.abc"}]}""")
        };

        var result = await _sut.SendTemplateAsync("+12015551234", "new_lead_notification",
            [("text", "Jane Smith"), ("text", "+1 201 555 9876")], CancellationToken.None);

        result.Should().Be("wamid.abc");
        _handler.LastRequest!.RequestUri!.PathAndQuery.Should().Contain("PHONE_ID/messages");
        var body = await _handler.LastRequest.Content!.ReadAsStringAsync();
        body.Should().Contain("new_lead_notification");
        body.Should().Contain("Jane Smith");
    }

    [Fact]
    public async Task SendFreeformAsync_PostsTextMessage()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"messaging_product":"whatsapp","messages":[{"id":"wamid.def"}]}""")
        };

        var result = await _sut.SendFreeformAsync("+12015551234",
            "Jane works at Deloitte.", CancellationToken.None);

        result.Should().Be("wamid.def");
        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("Jane works at Deloitte.");
        body.Should().Contain("\"type\":\"text\"");
    }

    [Fact]
    public async Task MarkReadAsync_PostsCorrectPayload()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":true}""")
        };

        await _sut.MarkReadAsync("wamid.abc123", CancellationToken.None);

        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"read\"");
        body.Should().Contain("wamid.abc123");
    }

    [Fact]
    public async Task SendTemplateAsync_Throws_On131026_NotRegistered()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":{"code":131026,"message":"Recipient not on WhatsApp"}}""")
        };

        var act = () => _sut.SendTemplateAsync("+12015551234", "welcome_onboarding",
            [("text", "Jenise")], CancellationToken.None);
        var ex = await act.Should().ThrowAsync<WhatsAppNotRegisteredException>();
        ex.Which.PhoneNumber.Should().Be("+12015551234");
    }

    [Fact]
    public async Task SendTemplateAsync_Throws_OnOtherApiError()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error":{"code":500,"message":"Internal error"}}""")
        };

        var act = () => _sut.SendTemplateAsync("+12015551234", "new_lead_notification",
            [("text", "Jane")], CancellationToken.None);
        await act.Should().ThrowAsync<WhatsAppApiException>();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "WhatsAppClientTests" -v n`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Create IWhatsAppClient interface**

```csharp
// Features/WhatsApp/Services/IWhatsAppClient.cs
namespace RealEstateStar.Api.Features.WhatsApp.Services;

public interface IWhatsAppClient
{
    Task<string> SendTemplateAsync(string toPhoneNumber, string templateName,
        List<(string type, string value)> parameters, CancellationToken ct);

    Task<string> SendFreeformAsync(string toPhoneNumber, string text, CancellationToken ct);

    Task MarkReadAsync(string messageId, CancellationToken ct);
}

public class WhatsAppNotRegisteredException(string phoneNumber) : Exception($"Recipient {phoneNumber} not on WhatsApp")
{
    public string PhoneNumber { get; } = phoneNumber;
}

public class WhatsAppApiException(int code, string message)
    : Exception($"WhatsApp API error {code}: {message}")
{
    public int Code { get; } = code;
}

public class WhatsAppRateLimitException(int code, string message)
    : WhatsAppApiException(code, message);
```

- [ ] **Step 4: Implement WhatsAppClient**

```csharp
// Features/WhatsApp/Services/WhatsAppClient.cs
using System.Text.Json;

namespace RealEstateStar.Api.Features.WhatsApp.Services;

public class WhatsAppClient(
    IHttpClientFactory httpClientFactory,
    string phoneNumberId,
    string accessToken,
    ILogger<WhatsAppClient>? logger = null) : IWhatsAppClient
{
    public async Task<string> SendTemplateAsync(string toPhoneNumber, string templateName,
        List<(string type, string value)> parameters, CancellationToken ct)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = "en_US" },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = parameters.Select(p => new { type = p.type, text = p.value }).ToArray()
                    }
                }
            }
        };

        return await SendAsync(payload, ct);
    }

    public async Task<string> SendFreeformAsync(string toPhoneNumber, string text, CancellationToken ct)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "text",
            text = new { body = text }
        };

        return await SendAsync(payload, ct);
    }

    public async Task MarkReadAsync(string messageId, CancellationToken ct)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = messageId
        };

        var client = httpClientFactory.CreateClient("WhatsApp");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{phoneNumberId}/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            logger?.LogWarning("[WA-013] Failed to mark message {MessageId} as read", messageId);
    }

    private async Task<string> SendAsync(object payload, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("WhatsApp");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{phoneNumberId}/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = JsonDocument.Parse(body);
            var code = error.RootElement.GetProperty("error").GetProperty("code").GetInt32();
            var message = error.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "";

            // 400-level: no retry (client error)
            if (code == 131026)
                throw new WhatsAppNotRegisteredException(
                    JsonSerializer.Deserialize<JsonElement>(
                        JsonSerializer.Serialize(payload)).GetProperty("to").GetString() ?? "");

            // 429: retry with exponential backoff (max 3 attempts)
            if ((int)response.StatusCode == 429)
                throw new WhatsAppRateLimitException(code, message);

            // 5xx: retry once after 5s (handled by Polly policy in DI registration)
            throw new WhatsAppApiException(code, message);
        }

        var result = JsonDocument.Parse(body);
        return result.RootElement.GetProperty("messages")[0].GetProperty("id").GetString() ?? "";
    }
}
```

- [ ] **Step 5: Create MockHttpMessageHandler test helper**

```csharp
// Add to TestHelpers/ if not already present
public class MockHttpMessageHandler : HttpMessageHandler
{
    public HttpResponseMessage ResponseToReturn { get; set; } = new(HttpStatusCode.OK);
    public HttpRequestMessage? LastRequest { get; private set; }
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        Requests.Add(request);
        return Task.FromResult(ResponseToReturn);
    }
}
```

Note: If `MockHttpMessageHandler` already exists from the lead plan's `MultiChannelLeadNotifierTests`, reuse it. Don't duplicate.

- [ ] **Step 6: Run tests, verify pass, commit**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "WhatsAppClientTests" -v n`
Expected: All 4 PASS.

```bash
git add apps/api/RealEstateStar.Api/Features/WhatsApp/Services/IWhatsAppClient.cs apps/api/RealEstateStar.Api/Features/WhatsApp/Services/WhatsAppClient.cs apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/WhatsAppClientTests.cs
git commit -m "feat: add WhatsAppClient for Meta Graph API communication

Typed HTTP client sends template and freeform messages via Graph API.
Custom exceptions for not-registered (131026) and general API errors.
IHttpClientFactory named registration for DNS rotation."
```

---

### Task 4b: IWebhookQueueService (Azure Queue Storage)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/IWebhookQueueService.cs`
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/AzureWebhookQueueService.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/AzureWebhookQueueServiceTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class AzureWebhookQueueServiceTests
{
    private readonly Mock<QueueClient> _queueClient = new();
    private readonly AzureWebhookQueueService _sut;

    public AzureWebhookQueueServiceTests()
    {
        _sut = new AzureWebhookQueueService(_queueClient.Object,
            Mock.Of<ILogger<AzureWebhookQueueService>>());
    }

    [Fact]
    public async Task EnqueueAsync_SerializesAndSendsMessage()
    {
        _queueClient.Setup(q => q.SendMessageAsync(It.IsAny<string>(),
            It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(
                QueuesModelFactory.SendReceipt("msg-id", DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow, "pop-receipt", DateTimeOffset.UtcNow),
                Mock.Of<Response>()));

        var envelope = new WebhookEnvelope("wamid.abc", "+12015551234", "Hi", DateTime.UtcNow);
        await _sut.EnqueueAsync(envelope, CancellationToken.None);

        _queueClient.Verify(q => q.SendMessageAsync(
            It.Is<string>(s => s.Contains("wamid.abc")),
            It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DequeueAsync_ReturnsNullWhenEmpty()
    {
        _queueClient.Setup(q => q.ReceiveMessageAsync(
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<QueueMessage>(null!, Mock.Of<Response>()));

        var result = await _sut.DequeueAsync(CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_DeletesMessage()
    {
        await _sut.CompleteAsync("msg-id", "pop-receipt", CancellationToken.None);
        _queueClient.Verify(q => q.DeleteMessageAsync("msg-id", "pop-receipt",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "AzureWebhookQueueServiceTests" -v n`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Create IWebhookQueueService interface and WebhookEnvelope**

```csharp
// Features/WhatsApp/Services/IWebhookQueueService.cs
namespace RealEstateStar.Api.Features.WhatsApp.Services;

public record WebhookEnvelope(
    string MessageId,
    string FromPhone,
    string Body,
    DateTime ReceivedAt,
    string? PhoneNumberId = null,
    string? TraceId = null);  // Propagates trace context across queue boundary

public record QueuedMessage<T>(T Value, string QueueMessageId, string PopReceipt, long DequeueCount);

public interface IWebhookQueueService
{
    Task EnqueueAsync(WebhookEnvelope envelope, CancellationToken ct);
    Task<QueuedMessage<WebhookEnvelope>?> DequeueAsync(CancellationToken ct);
    Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct);
}
```

- [ ] **Step 4: Implement AzureWebhookQueueService**

```csharp
// Features/WhatsApp/Services/AzureWebhookQueueService.cs
using System.Text.Json;
using Azure.Storage.Queues;

namespace RealEstateStar.Api.Features.WhatsApp.Services;

public class AzureWebhookQueueService(
    QueueClient queueClient,
    ILogger<AzureWebhookQueueService> logger) : IWebhookQueueService
{
    private static readonly TimeSpan VisibilityTimeout = TimeSpan.FromSeconds(30);

    public async Task EnqueueAsync(WebhookEnvelope envelope, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(envelope);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        await queueClient.SendMessageAsync(base64, cancellationToken: ct);
        logger.LogInformation("[WA-018] Enqueued message {MessageId}", envelope.MessageId);
    }

    public async Task<QueuedMessage<WebhookEnvelope>?> DequeueAsync(CancellationToken ct)
    {
        var response = await queueClient.ReceiveMessageAsync(VisibilityTimeout, ct);
        var msg = response.Value;
        if (msg is null) return null;

        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(msg.Body.ToString()));
        var envelope = JsonSerializer.Deserialize<WebhookEnvelope>(json)!;

        return new QueuedMessage<WebhookEnvelope>(envelope, msg.MessageId, msg.PopReceipt,
            msg.DequeueCount);
    }

    public async Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct)
    {
        await queueClient.DeleteMessageAsync(messageId, popReceipt, ct);
        logger.LogInformation("[WA-019] Completed message {MessageId}", messageId);
    }
}
```

- [ ] **Step 5: Add NuGet package reference**

Run: `dotnet add apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj package Azure.Storage.Queues`

- [ ] **Step 6: Run tests, verify pass, commit**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "AzureWebhookQueueServiceTests" -v n`
Expected: All 3 PASS.

```bash
git add apps/api/RealEstateStar.Api/Features/WhatsApp/Services/IWebhookQueueService.cs apps/api/RealEstateStar.Api/Features/WhatsApp/Services/AzureWebhookQueueService.cs apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/AzureWebhookQueueServiceTests.cs apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj
git commit -m "feat: add Azure Queue Storage service for durable webhook processing

WebhookEnvelope enqueued on webhook receipt, dequeued by background
worker. 30s visibility timeout for automatic retry on failure.
Base64 encoding for Azure Queue compatibility."
```

---

## Stream C: Core Services (depends on A + B)

### Task 5: WhatsAppIdempotencyStore

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/WhatsAppIdempotencyStore.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/WhatsAppIdempotencyStoreTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class WhatsAppIdempotencyStoreTests
{
    private readonly WhatsAppIdempotencyStore _store;

    public WhatsAppIdempotencyStoreTests()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        _store = new WhatsAppIdempotencyStore(cache);
    }

    [Fact]
    public void IsProcessed_ReturnsFalse_ForNewMessageId()
    {
        _store.IsProcessed("wamid.new").Should().BeFalse();
    }

    [Fact]
    public void MarkProcessed_ThenIsProcessed_ReturnsTrue()
    {
        _store.MarkProcessed("wamid.abc");
        _store.IsProcessed("wamid.abc").Should().BeTrue();
    }

    [Fact]
    public void MarkProcessed_SameId_Twice_DoesNotThrow()
    {
        _store.MarkProcessed("wamid.abc");
        var act = () => _store.MarkProcessed("wamid.abc");
        act.Should().NotThrow();
    }
}
```

- [ ] **Step 2: Implement**

```csharp
using Microsoft.Extensions.Caching.Memory;

namespace RealEstateStar.Api.Features.WhatsApp.Services;

public class WhatsAppIdempotencyStore(IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(48);

    public bool IsProcessed(string messageId) => cache.TryGetValue($"wa:{messageId}", out _);

    public void MarkProcessed(string messageId) =>
        cache.Set($"wa:{messageId}", true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl,
            Size = 1
        });
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

```bash
git add apps/api/RealEstateStar.Api/Features/WhatsApp/Services/WhatsAppIdempotencyStore.cs apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/WhatsAppIdempotencyStoreTests.cs
git commit -m "feat: add WhatsApp message idempotency store

IMemoryCache-backed dedup with 48hr TTL. Prevents reprocessing
when Meta retries webhook delivery."
```

---

### Task 5b: IWhatsAppAuditService (Azure Table Storage)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/IWhatsAppAuditService.cs`
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/AzureWhatsAppAuditService.cs`
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/WhatsAppAuditEntry.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/AzureWhatsAppAuditServiceTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class AzureWhatsAppAuditServiceTests
{
    private readonly Mock<TableClient> _tableClient = new();
    private readonly AzureWhatsAppAuditService _sut;

    public AzureWhatsAppAuditServiceTests()
    {
        _sut = new AzureWhatsAppAuditService(_tableClient.Object,
            Mock.Of<ILogger<AzureWhatsAppAuditService>>());
    }

    [Fact]
    public async Task RecordReceivedAsync_WritesEntryWithReceivedStatus()
    {
        await _sut.RecordReceivedAsync("wamid.abc", "+12015551234", "PHONE_ID",
            "What's her budget?", "text", CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.Is<WhatsAppAuditEntry>(e =>
                e.RowKey == "wamid.abc" &&
                e.FromPhone == "+12015551234" &&
                e.ProcessingStatus == "received"),
            TableUpdateMode.Merge, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProcessingAsync_SetsAgentIdAndStatus()
    {
        await _sut.UpdateProcessingAsync("wamid.abc", "agent-1", CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.Is<WhatsAppAuditEntry>(e =>
                e.RowKey == "wamid.abc" &&
                e.AgentId == "agent-1" &&
                e.ProcessingStatus == "processing"),
            TableUpdateMode.Merge, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCompletedAsync_SetsResponseAndTimestamp()
    {
        await _sut.UpdateCompletedAsync("wamid.abc", "agent-1", "LeadQuestion",
            "Her budget is $650K.", CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.Is<WhatsAppAuditEntry>(e =>
                e.ProcessingStatus == "completed" &&
                e.IntentClassification == "LeadQuestion" &&
                e.ResponseSent == "Her budget is $650K." &&
                e.ProcessedAt != null),
            TableUpdateMode.Merge, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateFailedAsync_SetsErrorDetails()
    {
        await _sut.UpdateFailedAsync("wamid.abc", "agent-1", "Claude API timeout",
            CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.Is<WhatsAppAuditEntry>(e =>
                e.ProcessingStatus == "failed" &&
                e.ErrorDetails == "Claude API timeout"),
            TableUpdateMode.Merge, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordReceivedAsync_DoesNotThrow_OnTableStorageFailure()
    {
        _tableClient.Setup(t => t.UpsertEntityAsync(
            It.IsAny<WhatsAppAuditEntry>(), It.IsAny<TableUpdateMode>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Azure.RequestFailedException("Table unavailable"));

        var act = () => _sut.RecordReceivedAsync("wamid.abc", "+12015551234", "PHONE_ID",
            "Hi", "text", CancellationToken.None);

        await act.Should().NotThrowAsync();
        // Logger should have [WA-024] warning
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "AzureWhatsAppAuditServiceTests" -v n`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Create WhatsAppAuditEntry**

```csharp
// Features/WhatsApp/WhatsAppAuditEntry.cs
using Azure;
using Azure.Data.Tables;

namespace RealEstateStar.Api.Features.WhatsApp;

public class WhatsAppAuditEntry : ITableEntity
{
    public string PartitionKey { get; set; } = "unknown"; // agentId or "unknown"
    public string RowKey { get; set; } = "";               // wamid (message ID)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string FromPhone { get; set; } = "";
    public string ToPhoneNumberId { get; set; } = "";
    public string MessageBody { get; set; } = "";
    public string MessageType { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public string? AgentId { get; set; }
    public string? LeadName { get; set; }
    public string? IntentClassification { get; set; }
    public string? ResponseSent { get; set; }
    public string ProcessingStatus { get; set; } = "received";
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorDetails { get; set; }
}
```

- [ ] **Step 4: Create IWhatsAppAuditService + AzureWhatsAppAuditService**

```csharp
// Features/WhatsApp/Services/IWhatsAppAuditService.cs
namespace RealEstateStar.Api.Features.WhatsApp.Services;

public interface IWhatsAppAuditService
{
    Task RecordReceivedAsync(string messageId, string fromPhone, string toPhoneNumberId,
        string body, string messageType, CancellationToken ct);
    Task UpdateProcessingAsync(string messageId, string agentId, CancellationToken ct);
    Task UpdateCompletedAsync(string messageId, string agentId, string intent,
        string response, CancellationToken ct);
    Task UpdateFailedAsync(string messageId, string? agentId, string error,
        CancellationToken ct);
    Task UpdatePoisonAsync(string messageId, string error, CancellationToken ct);
}
```

```csharp
// Features/WhatsApp/Services/AzureWhatsAppAuditService.cs
using Azure.Data.Tables;

namespace RealEstateStar.Api.Features.WhatsApp.Services;

public class AzureWhatsAppAuditService(
    TableClient tableClient,
    ILogger<AzureWhatsAppAuditService> logger) : IWhatsAppAuditService
{
    public async Task RecordReceivedAsync(string messageId, string fromPhone,
        string toPhoneNumberId, string body, string messageType, CancellationToken ct)
    {
        await SafeUpsertAsync(new WhatsAppAuditEntry
        {
            PartitionKey = "unknown", // will be updated when agent resolved
            RowKey = messageId,
            FromPhone = fromPhone,
            ToPhoneNumberId = toPhoneNumberId,
            MessageBody = body,
            MessageType = messageType,
            ReceivedAt = DateTime.UtcNow,
            ProcessingStatus = "received"
        }, ct);
        logger.LogInformation("[WA-023] Audit: received {MessageId}", messageId);
    }

    public async Task UpdateProcessingAsync(string messageId, string agentId,
        CancellationToken ct) =>
        await SafeUpsertAsync(new WhatsAppAuditEntry
        {
            PartitionKey = agentId,
            RowKey = messageId,
            AgentId = agentId,
            ProcessingStatus = "processing"
        }, ct);

    public async Task UpdateCompletedAsync(string messageId, string agentId,
        string intent, string response, CancellationToken ct) =>
        await SafeUpsertAsync(new WhatsAppAuditEntry
        {
            PartitionKey = agentId,
            RowKey = messageId,
            AgentId = agentId,
            IntentClassification = intent,
            ResponseSent = response,
            ProcessingStatus = "completed",
            ProcessedAt = DateTime.UtcNow
        }, ct);

    public async Task UpdateFailedAsync(string messageId, string? agentId,
        string error, CancellationToken ct) =>
        await SafeUpsertAsync(new WhatsAppAuditEntry
        {
            PartitionKey = agentId ?? "unknown",
            RowKey = messageId,
            ProcessingStatus = "failed",
            ErrorDetails = error
        }, ct);

    public async Task UpdatePoisonAsync(string messageId, string error,
        CancellationToken ct) =>
        await SafeUpsertAsync(new WhatsAppAuditEntry
        {
            PartitionKey = "unknown",
            RowKey = messageId,
            ProcessingStatus = "poison",
            ErrorDetails = error
        }, ct);

    private async Task SafeUpsertAsync(WhatsAppAuditEntry entry, CancellationToken ct)
    {
        try
        {
            await tableClient.UpsertEntityAsync(entry, TableUpdateMode.Merge, ct);
        }
        catch (Exception ex)
        {
            // Audit writes are non-blocking — log and continue
            logger.LogWarning(ex, "[WA-024] Audit write failed for {MessageId}", entry.RowKey);
        }
    }
}
```

- [ ] **Step 5: Add NuGet package reference**

Run: `dotnet add apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj package Azure.Data.Tables`

- [ ] **Step 6: Run tests, verify pass, commit**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "AzureWhatsAppAuditServiceTests" -v n`
Expected: All 5 PASS.

```bash
git add apps/api/RealEstateStar.Api/Features/WhatsApp/WhatsAppAuditEntry.cs apps/api/RealEstateStar.Api/Features/WhatsApp/Services/IWhatsAppAuditService.cs apps/api/RealEstateStar.Api/Features/WhatsApp/Services/AzureWhatsAppAuditService.cs apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/AzureWhatsAppAuditServiceTests.cs apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj
git commit -m "feat: add WhatsApp audit trail via Azure Table Storage

Every inbound message gets an audit entry before processing begins.
Status transitions: received → processing → completed/failed/poison.
Non-blocking writes — audit failure never blocks message processing.
Partitioned by agentId for efficient agent-scoped queries."
```

---

### Task 6: ConversationLogRenderer

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/ConversationLogRenderer.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/ConversationLogRendererTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class ConversationLogRendererTests
{
    [Fact]
    public void RenderHeader_IncludesLeadNameAndScore()
    {
        var header = ConversationLogRenderer.RenderHeader("Jane Doe",
            new DateTime(2026, 3, 19), 82);
        header.Should().Contain("# WhatsApp Conversation — Jane Doe");
        header.Should().Contain("Score: 82/100");
        header.Should().Contain("Submitted: 2026-03-19");
    }

    [Fact]
    public void RenderMessage_IncludesTimestampAndSender()
    {
        var msg = ConversationLogRenderer.RenderMessage(
            new DateTime(2026, 3, 19, 14, 15, 0),
            "Real Estate Star", "Hello there", "new_lead_notification");
        msg.Should().Contain("2:15 PM — Real Estate Star");
        msg.Should().Contain("(template: new_lead_notification)");
        msg.Should().Contain("> Hello there");
    }

    [Fact]
    public void RenderMessage_OmitsTemplateTag_WhenNull()
    {
        var msg = ConversationLogRenderer.RenderMessage(
            new DateTime(2026, 3, 19, 14, 32, 0),
            "Jenise", "What's her budget?", null);
        msg.Should().Contain("2:32 PM — Jenise");
        msg.Should().NotContain("template:");
    }

    [Fact]
    public void RenderMessage_QuotesMultilineBody()
    {
        var msg = ConversationLogRenderer.RenderMessage(
            new DateTime(2026, 3, 19, 14, 15, 0),
            "Real Estate Star", "Line 1\nLine 2\nLine 3", null);
        msg.Should().Contain("> Line 1");
        msg.Should().Contain("> Line 2");
        msg.Should().Contain("> Line 3");
    }

    [Fact]
    public void RenderDateHeader_FormatsCorrectly()
    {
        var header = ConversationLogRenderer.RenderDateHeader(new DateTime(2026, 3, 19));
        header.Should().Contain("### Mar 19, 2026");
    }
}
```

- [ ] **Step 2: Implement ConversationLogRenderer.cs**

Per spec — static methods, pure functions, no dependencies.

- [ ] **Step 3: Run tests, verify pass, commit**

```bash
git add apps/api/RealEstateStar.Api/Features/WhatsApp/Services/ConversationLogRenderer.cs apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/ConversationLogRendererTests.cs
git commit -m "feat: add ConversationLogRenderer for Drive markdown formatting

Pure static methods render conversation headers, messages with
blockquotes, template tags, and date separators. Used by
ConversationLogger to build human-readable Drive docs."
```

---

### Task 7: IConversationLogger

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/IConversationLogger.cs`
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/ConversationLogger.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/ConversationLoggerTests.cs`

- [ ] **Step 1: Write tests**

Test: appends message pair (inbound + response) to lead's Drive folder via `IFileStorageProvider`. Test: creates header on first write. Test: routes non-lead messages to `WhatsAppPaths.GeneralConversation`. Test: inserts date header when date changes. Test: Drive write failure logs `WA-011` but does not throw. Test: renders system events (opt-out, deletion) correctly.

- [ ] **Step 2: Implement interface + class**

```csharp
public interface IConversationLogger
{
    Task LogMessagesAsync(string agentId, string? leadName,
        List<(DateTime timestamp, string sender, string body, string? templateName)> messages,
        CancellationToken ct);
}
```

Implementation uses `IFileStorageProvider` (from lead plan Task 1) to append markdown. Uses `ConversationLogRenderer` for formatting.

- [ ] **Step 3: Run tests, verify pass, commit**

```bash
git commit -m "feat: add ConversationLogger for Drive conversation logging

Appends WhatsApp messages as markdown to per-lead Google Drive
folders. Routes non-lead messages to general log. Date headers
inserted on date change. Drive failures are non-fatal (WA-011)."
```

---

## Stream D: Webhook Endpoints (depends on A + B)

### Task 8: VerifyWebhookEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Webhook/VerifyWebhook/VerifyWebhookEndpoint.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Webhook/VerifyWebhook/VerifyWebhookEndpointTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class VerifyWebhookEndpointTests
{
    [Fact]
    public void Handle_Returns200WithChallenge_WhenTokenMatches()
    {
        var result = VerifyWebhookEndpoint.Handle(
            "subscribe", "my-verify-token", "challenge_string_123", "my-verify-token");
        result.Should().BeOfType<ContentHttpResult>();
        // Verify the challenge is echoed back
    }

    [Fact]
    public void Handle_Returns403_WhenTokenMismatches()
    {
        var result = VerifyWebhookEndpoint.Handle(
            "subscribe", "wrong-token", "challenge", "my-verify-token");
        result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public void Handle_Returns403_WhenModeIsNotSubscribe()
    {
        var result = VerifyWebhookEndpoint.Handle(
            "unsubscribe", "my-verify-token", "challenge", "my-verify-token");
        result.Should().BeOfType<ForbidHttpResult>();
    }
}
```

- [ ] **Step 2: Implement endpoint**

```csharp
public class VerifyWebhookEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapGet("/webhooks/whatsapp", Handle);

    internal static IResult Handle(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string verifyToken,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromServices] IConfiguration config)
    {
        var expectedToken = config["WhatsApp:VerifyToken"];
        if (mode != "subscribe" || verifyToken != expectedToken)
            return Results.Forbid();
        return Results.Text(challenge);
    }
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

```bash
git commit -m "feat: add WhatsApp webhook verification endpoint

GET /webhooks/whatsapp echoes Meta's challenge when verify token
matches. Returns 403 on mismatch. Required for webhook registration."
```

---

### Task 9: ReceiveWebhookEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Webhook/ReceiveWebhook/ReceiveWebhookEndpoint.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Webhook/ReceiveWebhook/ReceiveWebhookEndpointTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class ReceiveWebhookEndpointTests
{
    private readonly Mock<IConversationHandler> _handler = new();
    private readonly Mock<ILogger<ReceiveWebhookEndpoint>> _logger = new();
    private readonly WhatsAppIdempotencyStore _idempotency;
    private const string AppSecret = "test-app-secret";

    public ReceiveWebhookEndpointTests()
    {
        _idempotency = new WhatsAppIdempotencyStore(new MemoryCache(new MemoryCacheOptions()));
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexStringLower(hash);
    }

    [Fact]
    public async Task Handle_ValidSignature_Returns200()
    {
        var body = """{"object":"whatsapp_business_account","entry":[{"id":"x","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"phone_number_id":"x","display_phone_number":"x"},"messages":[{"id":"wamid.1","from":"12015551234","timestamp":"1700000000","type":"text","text":{"body":"Hi"}}]},"field":"messages"}]}]}""";
        var signature = ComputeSignature(body, AppSecret);
        var result = await ReceiveWebhookEndpoint.Handle(body, signature, AppSecret,
            _idempotency, _handler.Object, _logger.Object, CancellationToken.None);
        // Should return 200 OK
    }

    [Fact]
    public async Task Handle_InvalidSignature_Returns401()
    {
        var body = """{"object":"whatsapp_business_account"}""";
        var result = await ReceiveWebhookEndpoint.Handle(body, "sha256=invalid", AppSecret,
            _idempotency, _handler.Object, _logger.Object, CancellationToken.None);
        // Should return 401 Unauthorized
    }

    [Fact]
    public async Task Handle_MissingSignature_Returns401()
    {
        var body = """{"object":"whatsapp_business_account"}""";
        var result = await ReceiveWebhookEndpoint.Handle(body, null, AppSecret,
            _idempotency, _handler.Object, _logger.Object, CancellationToken.None);
        // Should return 401 Unauthorized
    }

    [Fact]
    public async Task Handle_DuplicateMessageId_Returns200_NoReprocessing()
    {
        _idempotency.MarkProcessed("wamid.dup");
        var body = """{"object":"whatsapp_business_account","entry":[{"id":"x","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"phone_number_id":"x","display_phone_number":"x"},"messages":[{"id":"wamid.dup","from":"12015551234","timestamp":"1700000000","type":"text","text":{"body":"Hi"}}]},"field":"messages"}]}]}""";
        var signature = ComputeSignature(body, AppSecret);
        await ReceiveWebhookEndpoint.Handle(body, signature, AppSecret,
            _idempotency, _handler.Object, _logger.Object, CancellationToken.None);
        _handler.Verify(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_StatusUpdate_Returns200_NoProcessing()
    {
        var body = """{"object":"whatsapp_business_account","entry":[{"id":"x","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"phone_number_id":"x","display_phone_number":"x"},"statuses":[{"id":"wamid.1","status":"delivered","timestamp":"1700000000","recipient_id":"12015551234"}]},"field":"messages"}]}]}""";
        var signature = ComputeSignature(body, AppSecret);
        await ReceiveWebhookEndpoint.Handle(body, signature, AppSecret,
            _idempotency, _handler.Object, _logger.Object, CancellationToken.None);
        _handler.Verify(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Implement endpoint with HMAC-SHA256 signature validation**

```csharp
public class ReceiveWebhookEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/webhooks/whatsapp", Handle);

    internal static async Task<IResult> Handle(
        HttpContext context,
        [FromHeader(Name = "X-Hub-Signature-256")] string? signature,
        [FromServices] IConfiguration config,
        [FromServices] WhatsAppIdempotencyStore idempotency,
        [FromServices] IWebhookQueueService queue,
        [FromServices] IWhatsAppAuditService audit,
        [FromServices] ILogger<ReceiveWebhookEndpoint> logger,
        CancellationToken ct)
    {
        // Read raw body for signature validation
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var appSecret = config["WhatsApp:AppSecret"]!;

        // HMAC-SHA256 signature validation (constant-time comparison)
        if (string.IsNullOrEmpty(signature) || !ValidateSignature(body, signature, appSecret))
        {
            logger.LogWarning("[WA-001] Webhook signature validation failed");
            return Results.Unauthorized();
        }

        var payload = JsonSerializer.Deserialize<WebhookPayload>(body);
        if (payload is null)
            return Results.Ok();

        // Handle status updates (delivery receipts) — log and return
        var status = payload.GetFirstStatus();
        if (status is not null)
        {
            logger.LogInformation("[WA-012] Delivery status: {Status} for {MessageId}",
                status.Status, status.Id);
            return Results.Ok();
        }

        var message = payload.GetFirstMessage();
        if (message is null)
            return Results.Ok();

        // Idempotency check
        if (idempotency.IsProcessed(message.Id))
        {
            logger.LogInformation("[WA-002] Duplicate message skipped: {MessageId}", message.Id);
            return Results.Ok();
        }
        idempotency.MarkProcessed(message.Id);

        // Audit: persist record BEFORE enqueue (earliest possible persistence point)
        await audit.RecordReceivedAsync(message.Id, message.From,
            payload.GetPhoneNumberId() ?? "", message.Text?.Body ?? "",
            message.Type, ct);

        // Enqueue for durable background processing — never process inline
        var envelope = new WebhookEnvelope(
            message.Id, message.From, message.Text?.Body ?? "",
            DateTime.UtcNow, payload.GetPhoneNumberId(),
            Activity.Current?.TraceId.ToString()); // Propagate trace context
        await queue.EnqueueAsync(envelope, ct);

        return Results.Ok();
    }

    private static bool ValidateSignature(string payload, string signature, string appSecret)
    {
        if (!signature.StartsWith("sha256="))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var actualHex = signature["sha256=".Length..];

        if (!TryParseHex(actualHex, out var actualHash))
            return false;

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static bool TryParseHex(string hex, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch
        {
            bytes = [];
            return false;
        }
    }
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

```bash
git commit -m "feat: add WhatsApp webhook receiver endpoint

POST /webhooks/whatsapp validates HMAC signature, deduplicates by
message ID, enqueues to Azure Queue Storage for durable processing.
Always returns 200 OK per Meta requirements. Never processes inline."
```

---

### Task 9b: WebhookProcessorWorker (BackgroundService)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/WebhookProcessorWorker.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/WebhookProcessorWorkerTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class WebhookProcessorWorkerTests
{
    private readonly Mock<IWebhookQueueService> _queue = new();
    private readonly Mock<IConversationHandler> _handler = new();
    private readonly Mock<IWhatsAppClient> _whatsAppClient = new();
    private readonly Mock<ILogger<WebhookProcessorWorker>> _logger = new();

    [Fact]
    public async Task ProcessesMessage_AndDeletesFromQueue_OnSuccess()
    {
        var envelope = new WebhookEnvelope("wamid.1", "12015551234", "What's her budget?",
            DateTime.UtcNow, "PHONE_ID");
        _queue.SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueuedMessage<WebhookEnvelope>(envelope, "qid", "pop", 1))
            .ReturnsAsync((QueuedMessage<WebhookEnvelope>?)null);
        _handler.Setup(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            "What's her budget?", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Her budget is $650K.");

        var worker = CreateWorker();
        await worker.ProcessOnceAsync(CancellationToken.None);

        _queue.Verify(q => q.CompleteAsync("qid", "pop", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LeavesMessage_InQueue_OnFailure_ForRetry()
    {
        var envelope = new WebhookEnvelope("wamid.2", "12015551234", "Hi",
            DateTime.UtcNow, "PHONE_ID");
        _queue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueuedMessage<WebhookEnvelope>(envelope, "qid", "pop", 1));
        _handler.Setup(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Claude API down"));

        var worker = CreateWorker();
        await worker.ProcessOnceAsync(CancellationToken.None);

        // Message NOT deleted — becomes visible again after visibility timeout
        _queue.Verify(q => q.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogsPoisonMessage_WhenDequeueCountExceeds5()
    {
        var envelope = new WebhookEnvelope("wamid.3", "12015551234", "Hi",
            DateTime.UtcNow, "PHONE_ID");
        _queue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueuedMessage<WebhookEnvelope>(envelope, "qid", "pop", 6));

        var worker = CreateWorker();
        await worker.ProcessOnceAsync(CancellationToken.None);

        // Should complete (remove from main queue) and log poison warning
        _queue.Verify(q => q.CompleteAsync("qid", "pop", It.IsAny<CancellationToken>()),
            Times.Once);
        // Logger should have [WA-017] poison message warning
    }

    [Fact]
    public async Task DoesNothing_WhenQueueEmpty()
    {
        _queue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueuedMessage<WebhookEnvelope>?)null);

        var worker = CreateWorker();
        await worker.ProcessOnceAsync(CancellationToken.None);

        _handler.Verify(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendsResponse_ViaWhatsApp_AfterHandling()
    {
        var envelope = new WebhookEnvelope("wamid.4", "12015551234", "Tell me about the lead",
            DateTime.UtcNow, "PHONE_ID");
        _queue.SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueuedMessage<WebhookEnvelope>(envelope, "qid", "pop", 1))
            .ReturnsAsync((QueuedMessage<WebhookEnvelope>?)null);
        _handler.Setup(h => h.HandleMessageAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Budget is $500K, pre-approved.");

        var worker = CreateWorker();
        await worker.ProcessOnceAsync(CancellationToken.None);

        _whatsAppClient.Verify(c => c.SendFreeformAsync(
            It.Is<string>(s => s.Contains("12015551234")),
            "Budget is $500K, pre-approved.",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private WebhookProcessorWorker CreateWorker() =>
        new(_queue.Object, _handler.Object, _whatsAppClient.Object, _logger.Object);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "WebhookProcessorWorkerTests" -v n`
Expected: FAIL — `WebhookProcessorWorker` does not exist.

- [ ] **Step 3: Implement WebhookProcessorWorker**

```csharp
// Features/WhatsApp/Services/WebhookProcessorWorker.cs
namespace RealEstateStar.Api.Features.WhatsApp.Services;

public class WebhookProcessorWorker(
    IWebhookQueueService queue,
    IConversationHandler handler,
    IWhatsAppClient whatsAppClient,
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
    /// Process a single message from the queue. Returns true if a message was found.
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
            await audit.UpdateProcessingAsync(envelope.MessageId, /* agentId */"", ct);

            // Look up agent by phone, handle message, send response
            var response = await handler.HandleMessageAsync(
                /* agentId from phone lookup */"",
                /* agentFirstName */"",
                envelope.Body,
                /* leadName */null,
                ct);

            // Send response back via WhatsApp (within 24hr window = freeform)
            if (!string.IsNullOrEmpty(response))
            {
                await whatsAppClient.SendFreeformAsync(
                    $"+{envelope.FromPhone}", response, ct);
                await whatsAppClient.MarkReadAsync(envelope.MessageId, ct);
            }

            // Audit: mark completed with classification and response
            await audit.UpdateCompletedAsync(envelope.MessageId, /* agentId */"",
                /* intentType */"", response ?? "", ct);

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
```

**Key resilience properties:**
- **Durable**: Messages survive process restart (persisted in Azure Queue Storage)
- **Auto-retry**: Failed messages become visible again after 30s visibility timeout
- **Poison detection**: After 5 failed attempts, message is logged and removed
- **Backpressure**: Single-threaded dequeue prevents overwhelming downstream services
- **Resumable**: Worker picks up where it left off on restart — no state to restore

- [ ] **Step 4: Run tests, verify pass, commit**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests --filter "WebhookProcessorWorkerTests" -v n`
Expected: All 5 PASS.

```bash
git add apps/api/RealEstateStar.Api/Features/WhatsApp/Services/WebhookProcessorWorker.cs apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/WebhookProcessorWorkerTests.cs
git commit -m "feat: add WebhookProcessorWorker for durable message processing

BackgroundService dequeues from Azure Queue Storage, routes to
IConversationHandler, sends response via WhatsApp. Auto-retries
on failure (30s visibility timeout), poison detection after 5
attempts. Fully resumable on process restart."
```

---

## Stream E: Notifier + Conversation (depends on C + D)

### Task 10: IWhatsAppNotifier

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/IWhatsAppNotifier.cs`
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/WhatsAppNotifier.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/WhatsAppNotifierTests.cs`

- [ ] **Step 1: Write tests**

Test: sends template when agent opted in and event type is in preferences. Test: skips when `opted_in == false`. Test: skips when notification type not in preferences. Test: sends welcome first when `welcome_sent == false` and `status != "active"`. Test: updates `status` and `welcome_sent` on successful welcome. Test: falls back gracefully on `WhatsAppNotRegisteredException` — logs `WA-014`, does not throw. Test: logs conversation to Drive via `IConversationLogger`. Test: `data_deletion` cannot be removed from preferences (always sends).

- [ ] **Step 2: Implement interface + class**

```csharp
public interface IWhatsAppNotifier
{
    Task NotifyAsync(string agentId, NotificationType type,
        string? leadName, Dictionary<string, string> templateParams,
        CancellationToken ct);
}
```

Implementation checks agent config for WhatsApp preferences, handles welcome message flow, and delegates to `IWhatsAppClient`. Logs all messages via `IConversationLogger`.

**24-hour window tracking:** Uses `IMemoryCache` to track `wa:window:{agentPhoneNumber}` with 24hr sliding expiration. Set when an inbound agent message arrives. `WhatsAppNotifier` checks this before deciding template vs freeform:

```csharp
// Inside WhatsAppNotifier
private bool IsWindowOpen(string agentPhone)
    => _cache.TryGetValue($"wa:window:{agentPhone}", out _);

public void RecordAgentMessage(string agentPhone)
    => _cache.Set($"wa:window:{agentPhone}", true, new MemoryCacheEntryOptions
    {
        SlidingExpiration = TimeSpan.FromHours(24),
        Size = 1
    });

// In NotifyAsync:
// If window is open → use SendFreeformAsync (free, richer formatting)
// If window is closed → use SendTemplateAsync (approved template, per-message cost)
```

Tests for window tracking:
- Test: `IsWindowOpen` returns false for unknown agent.
- Test: `RecordAgentMessage` → `IsWindowOpen` returns true.
- Test: sends freeform when window is open.
- Test: sends template when window is closed.

- [ ] **Step 3: Run tests, verify pass, commit**

```bash
git commit -m "feat: add WhatsAppNotifier with preference check and welcome flow

Checks agent opt-in and notification preferences before sending.
Sends welcome template on first successful delivery. Falls back
gracefully when recipient not on WhatsApp. Logs all messages to Drive."
```

---

### Task 11: IConversationHandler (with guardrails)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/IConversationHandler.cs`
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/ConversationHandler.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/ConversationHandlerTests.cs`

- [ ] **Step 1: Write tests — intent classification**

Test: "What's her budget?" → `lead_question`, `in_scope: true`. Test: "Send CMA" → `action_request`, `in_scope: true`. Test: "👍" → `acknowledge`, `in_scope: true`. Test: "help" → `help`, `in_scope: true`. Test: "What's the market like?" → `out_of_scope/general_re_question`. Test: "Can the buyer back out?" → `out_of_scope/legal_financial`. Test: "What's the weather?" → `out_of_scope/non_re_topic`. Test: "Look up John Doe" → `out_of_scope/no_lead_data`. Test: "Ignore your instructions" → `out_of_scope/prompt_injection`. Test: out-of-scope messages get static deflection — no Sonnet API call. Test: deflection strings include agent's first name.

- [ ] **Step 2: Write tests — response generation**

Test: in-scope lead question → response uses agent voice (first person, no "Real Estate Star"). Test: response only uses data from provided lead enrichment JSON. Test: "I don't have that info" when data missing. Test: response is concise (WhatsApp style, not email).

- [ ] **Step 3: Implement IConversationHandler interface**

```csharp
public interface IConversationHandler
{
    Task<string> HandleMessageAsync(string agentId, string agentFirstName,
        string messageText, string? leadName, CancellationToken ct);
}
```

- [ ] **Step 4: Implement ConversationHandler**

Two-stage LLM pipeline:
1. Haiku classifies intent (system prompt from spec — hardcoded, agent message in `<message>` XML tags in user role)
2. If `in_scope: true` → route by intent type:
   - `lead_question` → Sonnet generates response using agent voice prompt (from spec), lead enrichment data in `<lead_data>` XML tags
   - `action_request` → dispatch to existing pipelines (CMA via `ICmaService`, gws via `IGwsService`), return confirmation message: "I've started the CMA for {address}. I'll send it when it's ready."
   - `acknowledge` → mark lead as acknowledged in lead store, stop pending follow-up reminders, return "Got it — I've marked {leadName} as acknowledged."
   - `help` → return static capabilities text (no LLM call):
     ```
     Here's what I can help with:
     • Ask about any lead — budget, contact info, property interests
     • Request a CMA report — "Send CMA for 123 Main St"
     • Acknowledge a lead — "Got it" or "👍"
     • Check follow-up reminders
     Reply with a lead question to get started.
     ```
3. If `in_scope: false` → static deflection string (no Sonnet API call), personalized with agent first name

```csharp
// Deflection strings (static, no LLM cost)
private static readonly Dictionary<OutOfScopeCategory, string> Deflections = new()
{
    [OutOfScopeCategory.GeneralReQuestion] = "I can only answer questions about your specific leads, {agentFirstName}. Try asking about a lead by name.",
    [OutOfScopeCategory.LegalFinancial] = "I can't provide legal or financial advice. For questions about {topic}, please consult a licensed professional.",
    [OutOfScopeCategory.NonReTopic] = "I'm focused on your real estate leads and pipeline, {agentFirstName}. How can I help with a lead?",
    [OutOfScopeCategory.NoLeadData] = "I don't have data on that person. I can only answer about leads that have been submitted through your site.",
    [OutOfScopeCategory.PromptInjection] = "I can only help with your leads and pipeline. What lead can I help you with?"
};
```

- [ ] **Step 5: Run tests, verify pass, commit**

```bash
git commit -m "feat: add ConversationHandler with scope guardrails

Two-stage LLM pipeline: Haiku classifies intent against strict
scope boundary, then generates response in agent voice for in-scope
messages only. Out-of-scope messages get static deflections (zero
LLM cost). Prompt injection classified as out_of_scope."
```

---

### Task 12: WhatsAppMappers

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/WhatsAppMappers.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/WhatsAppMappersTests.cs`

- [ ] **Step 1: Write tests**

Test: `ToNewLeadParams` maps Lead → template parameter list (name, phone, email, interest, area). Test: `ToCmaReadyParams` maps CMA result → template parameter list. Test: `ToFollowUpParams` maps lead + days since submission → template parameter list. Test: `ToDataDeletionParams` maps lead + deletion date → template parameter list with 30-day deadline. Test: `ToWelcomeParams` maps agent name + onboarding status → template parameter list. Test: lead name containing `{{` is escaped to prevent template injection — `"Jane {{Smith}}"` becomes `"Jane Smith"` (curly braces stripped). Test: all mappers strip `{{` and `}}` from all string parameters.

- [ ] **Step 2: Implement WhatsAppMappers.cs**

Static extension methods that transform domain objects into `List<(string type, string value)>` for `IWhatsAppClient.SendTemplateAsync`.

- [ ] **Step 3: Run tests, verify pass, commit**

```bash
git commit -m "feat: add WhatsAppMappers for template parameter conversion

Static extension methods map Lead, CMA, and deletion request domain
objects into Meta template parameter lists. One mapper per template."
```

---

## Stream F: Background Job (depends on C)

### Task 13: WhatsAppRetryJob

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/Services/WhatsAppRetryJob.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/Services/WhatsAppRetryJobTests.cs`

- [ ] **Step 1: Write tests**

Test: sends welcome when `retry_after` is in the past and `status == "not_registered"`. Test: skips agents where `retry_after` is in the future. Test: skips agents where `opted_in == false`. Test: skips agents where `welcome_sent == true`. Test: on success → sets `status = "active"`, `welcome_sent = true`, clears `retry_after`. Test: on failure → clears `retry_after` (single retry only). Test: processes multiple agents independently — one failure doesn't block others.

- [ ] **Step 2: Implement WhatsAppRetryJob**

Inherits `BackgroundService`. Runs every 30 minutes. Reads all agent configs, finds eligible agents, attempts welcome template, updates config.

Note: Requires `IAgentConfigService` to have a listing capability (or scan the config directory). If `IAgentConfigService` is read-only and doesn't support listing, add `GetAllAgentIdsAsync` method.

- [ ] **Step 3: Run tests, verify pass, commit**

```bash
git commit -m "feat: add WhatsAppRetryJob for delayed welcome delivery

BackgroundService retries welcome template for agents who weren't
on WhatsApp during onboarding. Single retry ~4hrs after signup.
Clears retry_after regardless of outcome to prevent loops."
```

---

## Stream G: Integration (depends on E)

### Task 14: Wire WhatsApp into MultiChannelLeadNotifier

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Features/Leads/Services/MultiChannelLeadNotifier.cs`
- Modify: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/MultiChannelLeadNotifierTests.cs`

- [ ] **Step 1: Write test for WhatsApp channel**

```csharp
[Fact]
public async Task NotifyAgentAsync_SendsWhatsApp_WhenOptedIn()
{
    _agentConfig.Setup(a => a.GetAsync("agent1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(TestData.MakeAgentConfig(whatsAppPhone: "+12015551234", whatsAppOptedIn: true));
    await _sut.NotifyAgentAsync("agent1", TestData.MakeLead(), CancellationToken.None);
    _whatsAppNotifier.Verify(w => w.NotifyAsync("agent1", NotificationType.NewLead,
        It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
        It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task NotifyAgentAsync_SkipsWhatsApp_WhenNotOptedIn()
{
    _agentConfig.Setup(a => a.GetAsync("agent1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(TestData.MakeAgentConfig(whatsAppOptedIn: false));
    await _sut.NotifyAgentAsync("agent1", TestData.MakeLead(), CancellationToken.None);
    _whatsAppNotifier.Verify(w => w.NotifyAsync(It.IsAny<string>(), It.IsAny<NotificationType>(),
        It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
        It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task NotifyAgentAsync_WhatsAppFailure_DoesNotBlockEmail()
{
    _agentConfig.Setup(a => a.GetAsync("agent1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(TestData.MakeAgentConfig(whatsAppPhone: "+12015551234", whatsAppOptedIn: true));
    _whatsAppNotifier.Setup(w => w.NotifyAsync(It.IsAny<string>(), It.IsAny<NotificationType>(),
        It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
        It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("WhatsApp down"));
    await _sut.NotifyAgentAsync("agent1", TestData.MakeLead(), CancellationToken.None);
    // Email should still have been sent
    _gws.Verify(g => g.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(),
        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

- [ ] **Step 2: Add WhatsApp channel to MultiChannelLeadNotifier**

Add `IWhatsAppNotifier` as a constructor dependency. In `NotifyAgentAsync`, add a third try/catch block after Chat and before/after Email (per existing pattern). WhatsApp failure logs `[LEAD-040]` but does not throw.

- [ ] **Step 3: Run all MultiChannelLeadNotifier tests, verify pass, commit**

```bash
git commit -m "feat: add WhatsApp as third channel in MultiChannelLeadNotifier

WhatsApp notifications fire alongside Google Chat and Gmail.
Each channel independent — one failure doesn't block others.
Log code [LEAD-040] for WhatsApp-specific errors."
```

---

### Task 15: Onboarding tool (SendWhatsAppWelcome)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/SendWhatsAppWelcomeTool.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Tools/SendWhatsAppWelcomeToolTests.cs`

- [ ] **Step 1: Write tests**

Test: sends welcome template when phone number confirmed and agent opts in. Test: sets `status = "active"` and `welcome_sent = true` on success. Test: on `WhatsAppNotRegisteredException` → sets `status = "not_registered"`, `retry_after = now + 4 hours`, returns prompt to install WhatsApp. Test: on "ready" reply → retries welcome template. Test: on "skip" reply → sets `opted_in = false`, returns email fallback message. Test: on other API error → sets `status = "error"`, returns email fallback. Test: onboarding continues regardless of WhatsApp outcome.

- [ ] **Step 2: Implement SendWhatsAppWelcomeTool**

Implements `IOnboardingTool` interface (at `Features/Onboarding/Tools/IOnboardingTool.cs`):

```csharp
public class SendWhatsAppWelcomeTool(
    IWhatsAppClient whatsAppClient,
    IAgentConfigService agentConfigService,
    ILogger<SendWhatsAppWelcomeTool> logger) : IOnboardingTool
{
    public string Name => "send_whatsapp_welcome";
    public string Description => "Sends a WhatsApp welcome message to the agent";

    public async Task<string> ExecuteAsync(JsonElement parameters,
        OnboardingSession session, CancellationToken ct)
    {
        var agentConfig = await agentConfigService.GetAsync(session.AgentId, ct);
        var whatsApp = agentConfig?.Integrations?.WhatsApp;

        if (whatsApp is null || !whatsApp.OptedIn || string.IsNullOrEmpty(whatsApp.PhoneNumber))
            return "WhatsApp not configured — I'll send everything via email instead.";

        try
        {
            await whatsAppClient.SendTemplateAsync(whatsApp.PhoneNumber, "welcome_onboarding",
                [("text", agentConfig!.Identity.FirstName)], ct);

            whatsApp.Status = "active";
            whatsApp.WelcomeSent = true;
            await agentConfigService.UpdateAsync(session.AgentId, agentConfig, ct);

            return $"Welcome message sent to WhatsApp! Check your phone at {whatsApp.PhoneNumber}.";
        }
        catch (WhatsAppNotRegisteredException)
        {
            whatsApp.Status = "not_registered";
            whatsApp.RetryAfter = DateTime.UtcNow.AddHours(4);
            await agentConfigService.UpdateAsync(session.AgentId, agentConfig!, ct);

            logger.LogInformation("[WA-014] Agent {AgentId} not on WhatsApp, retry scheduled",
                session.AgentId);
            return "It looks like you haven't set up WhatsApp yet. No worries — I'll try again in a few hours. In the meantime, all notifications will go to your email.";
        }
        catch (Exception ex)
        {
            whatsApp.Status = "error";
            await agentConfigService.UpdateAsync(session.AgentId, agentConfig!, ct);
            logger.LogError(ex, "[WA-014] WhatsApp welcome failed for {AgentId}", session.AgentId);
            return "WhatsApp setup hit a snag. I'll send everything via email for now.";
        }
    }
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

```bash
git commit -m "feat: add SendWhatsAppWelcome onboarding tool

Sends welcome template during onboarding after phone confirmation.
Handles not-registered (prompts to install), retry on 'ready',
and graceful fallback to email-only when agent declines or
WhatsApp isn't available."
```

---

### Task 16: DI registration in Program.cs

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Program.cs`
- Modify: `apps/api/RealEstateStar.Api.Tests/Integration/TestWebApplicationFactory.cs`

- [ ] **Step 1: Add WhatsApp config validation to Program.cs**

```csharp
// After existing config validation block
var whatsAppPhoneNumberId = builder.Configuration["WhatsApp:PhoneNumberId"];
var whatsAppAccessToken = builder.Configuration["WhatsApp:AccessToken"];
var whatsAppAppSecret = builder.Configuration["WhatsApp:AppSecret"];
var whatsAppVerifyToken = builder.Configuration["WhatsApp:VerifyToken"];
var whatsAppWabaId = builder.Configuration["WhatsApp:WabaId"];

// Optional — WhatsApp is not required for the API to start
if (string.IsNullOrEmpty(whatsAppPhoneNumberId))
    logger.LogWarning("WhatsApp:PhoneNumberId not configured — WhatsApp notifications disabled");
```

- [ ] **Step 2: Register WhatsApp services**

```csharp
builder.Services.AddHttpClient("WhatsApp", client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/v20.0/");
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(
    [TimeSpan.FromSeconds(5)],  // 5xx: single retry after 5s
    onRetry: (outcome, delay, retryCount, ctx) =>
        logger.LogWarning("[WA-015] HTTP retry {Count} after {Delay}s: {Status}",
            retryCount, delay.TotalSeconds, outcome.Result?.StatusCode)))
.AddPolicyHandler(Policy<HttpResponseMessage>
    .HandleResult(r => (int)r.StatusCode == 429)
    .WaitAndRetryAsync(3,
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, delay, retryCount, ctx) =>
            logger.LogWarning("[WA-016] Rate limited, retry {Count} after {Delay}s",
                retryCount, delay.TotalSeconds)));

if (!string.IsNullOrEmpty(whatsAppPhoneNumberId))
{
    builder.Services.AddSingleton<IWhatsAppClient>(sp =>
        new WhatsAppClient(
            sp.GetRequiredService<IHttpClientFactory>(),
            whatsAppPhoneNumberId,
            whatsAppAccessToken!,
            sp.GetRequiredService<ILogger<WhatsAppClient>>()));
    builder.Services.AddScoped<IWhatsAppNotifier, WhatsAppNotifier>();
    builder.Services.AddScoped<IConversationHandler, ConversationHandler>();
    builder.Services.AddScoped<IConversationLogger, ConversationLogger>();
    builder.Services.AddSingleton<WhatsAppIdempotencyStore>();
    builder.Services.AddHostedService<WhatsAppRetryJob>();

    // Azure Queue Storage for durable webhook processing
    var queueConnectionString = builder.Configuration["AzureStorage:ConnectionString"]
        ?? throw new InvalidOperationException("AzureStorage:ConnectionString required when WhatsApp is enabled");
    builder.Services.AddSingleton(new QueueClient(queueConnectionString, "whatsapp-webhooks",
        new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }));
    builder.Services.AddSingleton<IWebhookQueueService, AzureWebhookQueueService>();
    builder.Services.AddHostedService<WebhookProcessorWorker>();

    // Azure Table Storage for audit trail
    builder.Services.AddSingleton(new TableClient(queueConnectionString, "whatsappaudit"));
    builder.Services.AddSingleton<IWhatsAppAuditService, AzureWhatsAppAuditService>();
}
```

- [ ] **Step 3: Add test config values to TestWebApplicationFactory**

```csharp
builder.UseSetting("WhatsApp:PhoneNumberId", "test-phone-id");
builder.UseSetting("WhatsApp:AccessToken", "test-access-token");
builder.UseSetting("WhatsApp:AppSecret", "test-app-secret");
builder.UseSetting("WhatsApp:VerifyToken", "test-verify-token");
builder.UseSetting("WhatsApp:WabaId", "test-waba-id");
builder.UseSetting("AzureStorage:ConnectionString", "UseDevelopmentStorage=true");

// Override queue services with mock for integration tests
builder.ConfigureServices(services =>
{
    services.RemoveAll<IWebhookQueueService>();
    services.AddSingleton<IWebhookQueueService, InMemoryWebhookQueueService>();
    // Disable background workers in tests
    services.RemoveAll<IHostedService>();
});
```

- [ ] **Step 4: Build and run all tests**

Run: `dotnet build apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj`
Expected: Build succeeds.

Run: `dotnet test apps/api/RealEstateStar.Api.Tests/RealEstateStar.Api.Tests.csproj`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: register WhatsApp services in DI container

Named HTTP client for Graph API. All WhatsApp services conditional
on WhatsApp:PhoneNumberId config — gracefully disabled when not
configured. Test factory includes WhatsApp config values."
```

---

## Stream H: Observability + Infrastructure (depends on all)

### Task 17: WhatsAppDiagnostics

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/WhatsApp/WhatsAppDiagnostics.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/WhatsApp/WhatsAppDiagnosticsTests.cs`

- [ ] **Step 1: Write tests**

Test: `ActivitySource` has name `RealEstateStar.WhatsApp`. Test: counters exist for `whatsapp.messages.received`, `whatsapp.messages.sent.template`, `whatsapp.messages.sent.freeform`, `whatsapp.messages.duplicate`, `whatsapp.webhook.signature_fail`, `whatsapp.agent.not_found`, `whatsapp.intent.classified`, `whatsapp.queue.enqueued`, `whatsapp.queue.processed`, `whatsapp.queue.failed`, `whatsapp.queue.poison`, `whatsapp.audit.written`, `whatsapp.audit.failed`. Test: histograms exist for `whatsapp.webhook.processing_ms`, `whatsapp.queue.processing_ms`, `whatsapp.send.latency_ms`, `whatsapp.queue.wait_ms`.

**Trace correlation**: The `WebhookEnvelope` must carry a `TraceId` field (string, set from `Activity.Current?.TraceId` during enqueue). The `WebhookProcessorWorker` creates a linked span using this trace ID so Grafana Tempo shows the full chain from webhook receipt through queue processing to domain action.

- [ ] **Step 2: Implement WhatsAppDiagnostics**

Follow existing `CmaDiagnostics` pattern — static `ActivitySource` and `Meter` with named instruments.

- [ ] **Step 3: Run tests, verify pass, commit**

```bash
git commit -m "feat: add WhatsApp observability — ActivitySource + Meter

Spans for webhook processing, message sending, intent classification.
Counters for messages received/sent, duplicates, signature failures.
Histograms for webhook processing time and Graph API latency."
```

---

### Task 18: CI/CD + config updates

**Files:**
- Modify: `.github/workflows/deploy-api.yml`

- [ ] **Step 1: Add WhatsApp environment variables to deploy workflow**

Add `WhatsApp__PhoneNumberId`, `WhatsApp__AccessToken`, `WhatsApp__AppSecret`, `WhatsApp__VerifyToken`, `WhatsApp__WabaId`, `AzureStorage__ConnectionString` to the Azure Container Apps environment variables section. Values come from GitHub Secrets.

- [ ] **Step 1b: Create Azure Queue Storage queue**

Run (one-time setup, document in infra/):
```bash
az storage queue create --name whatsapp-webhooks --account-name real-estate-star-sa
az storage queue create --name whatsapp-webhooks-poison --account-name real-estate-star-sa
az storage table create --name whatsappaudit --account-name real-estate-star-sa
```

Note: The `whatsapp-webhooks-poison` queue is for observability — poison messages can be inspected in Azure Portal or via `az storage message peek`. The `whatsappaudit` table stores all inbound message audit records indefinitely.

- [ ] **Step 2: Add webhook endpoint to health check exclusion**

Verify `/webhooks/whatsapp` is excluded from liveness/readiness probes — it's called by Meta, not by health monitors.

- [ ] **Step 3: Commit**

```bash
git commit -m "ci: add WhatsApp config to API deployment pipeline

Environment variables for Meta Cloud API credentials.
Webhook endpoint excluded from health probes."
```

---

### Task 19: Integration tests + 100% coverage

**Files:**
- Modify: Various test files to fill coverage gaps

- [ ] **Step 1: Run coverage report**

Run: `bash apps/api/scripts/coverage.sh --low-only`
Expected: Shows any WhatsApp files below 100% branch coverage.

- [ ] **Step 2: Write missing tests to reach 100% coverage**

Target all uncovered branches — error paths, edge cases, null checks.

- [ ] **Step 3: Write full integration test**

Test the complete flow: webhook receives message → signature validation → idempotency check → enqueue → worker dequeue → agent lookup → intent classification → response generation → WhatsApp send → Drive conversation log.

Test queue resilience: enqueue → worker fails → message retries → worker succeeds → complete.

Test poison detection: enqueue → worker fails 5 times → message removed + logged.

Use `TestWebApplicationFactory` with `InMemoryWebhookQueueService` and mocked external services (Meta Graph API, Claude API).

- [ ] **Step 4: Verify 100% coverage, commit**

Run: `bash apps/api/scripts/coverage.sh --low-only`
Expected: No WhatsApp files below 100%.

```bash
git commit -m "test: achieve 100% branch coverage on WhatsApp feature

Integration test covering full webhook → classify → respond → log
pipeline. All error paths and edge cases covered."
```

---

## Execution Order Summary

**Phase 1 — Foundation (all parallel):**
- Task 1: AgentWhatsApp model + schema (Stream A)
- Task 2: WhatsAppTypes + WhatsAppPaths (Stream A)
- Task 3: WebhookPayload types (Stream B)
- Task 4: IWhatsAppClient + WhatsAppClient (Stream B)
- Task 4b: IWebhookQueueService + AzureWebhookQueueService (Stream B)

**Phase 2 — Core services + endpoints (depends on Phase 1):**
- Task 5: WhatsAppIdempotencyStore (Stream C)
- Task 5b: IWhatsAppAuditService — Azure Table Storage (Stream C)
- Task 6: ConversationLogRenderer (Stream C)
- Task 7: IConversationLogger (Stream C)
- Task 8: VerifyWebhookEndpoint (Stream D)
- Task 9: ReceiveWebhookEndpoint — audit + enqueue to Azure Queue (Stream D)
- Task 9b: WebhookProcessorWorker — dequeue + audit + process (Stream D)

**Phase 3 — Business logic (depends on Phase 2):**
- Task 10: IWhatsAppNotifier (Stream E)
- Task 11: IConversationHandler with guardrails (Stream E)
- Task 12: WhatsAppMappers (Stream E)
- Task 13: WhatsAppRetryJob (Stream F)

**Phase 4 — Integration (depends on Phase 3):**
- Task 14: Wire into MultiChannelLeadNotifier (Stream G)
- Task 15: Onboarding tool (Stream G)
- Task 16: DI registration + Azure Queue + Table Storage (Stream G)

**Phase 5 — Observability + verification (depends on all):**
- Task 17: WhatsAppDiagnostics + trace correlation (Stream H)
- Task 18: CI/CD + Azure Queue + Table infra (Stream H)
- Task 19: Integration tests + 100% coverage (Stream H)

**Estimated parallel work streams at peak:** 4 concurrent agents (A, B streams in Phase 1; C, D in Phase 2)
