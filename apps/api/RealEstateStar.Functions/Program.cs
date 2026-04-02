using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Workers.WhatsApp;
using Serilog;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSerilog();

// ── Phase 1: WhatsApp webhook + retry functions ──────────────────────────────
// Register IConversationHandler, IIntentClassifier, IResponseGenerator, WhatsAppRetryJob.
// Feature flag Features:WhatsApp:UseBackgroundService is always false in the Functions host
// (there are no BackgroundServices here — queue/timer triggers handle that role).
builder.Configuration["Features:WhatsApp:UseBackgroundService"] = "false";
builder.Services.AddWhatsAppWorkers(builder.Configuration);

// IWhatsAppAuditService: disabled implementation — real Azure Table audit will be wired
// in Phase 3 when Clients.Azure is added to the Functions composition root.
builder.Services.AddSingleton<IWhatsAppAuditService, DisabledWhatsAppAuditService>();

// IWebhookQueueService: disabled — queue messages arrive via QueueTrigger binding,
// not through IWebhookQueueService.DequeueAsync. Registered for DI completeness.
builder.Services.AddSingleton<IWebhookQueueService, DisabledWebhookQueueService>();

// IWhatsAppSender: disabled — real WhatsApp API sending will be wired in Phase 3
// when Clients.WhatsApp is added to the Functions composition root.
// Note: Functions cannot reference Clients.* directly per architecture rules —
// the real sender will be registered via a dedicated WhatsApp DI extension in Phase 3.
builder.Services.AddSingleton<IWhatsAppSender, DisabledFunctionsWhatsAppSender>();

builder.Build().Run();

/// <summary>
/// Minimal no-op WhatsApp sender for use in the Functions host during Phase 1-2.
/// Phase 3 will replace this with the real WhatsApp API client.
/// </summary>
file sealed class DisabledFunctionsWhatsAppSender : IWhatsAppSender
{
    public Task<string> SendTemplateAsync(string toPhoneNumber, string templateName,
        List<(string type, string value)> parameters, CancellationToken ct)
        => Task.FromResult(string.Empty);

    public Task<string> SendFreeformAsync(string toPhoneNumber, string text, CancellationToken ct)
        => Task.FromResult(string.Empty);

    public Task MarkReadAsync(string messageId, CancellationToken ct)
        => Task.CompletedTask;
}
