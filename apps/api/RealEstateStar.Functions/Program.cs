using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Clients.Azure;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Domain.Shared.Interfaces;
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

// IDistributedContentCache — Azure Table Storage when connection string is available,
// no-op (null object) fallback for local development.
var storageConnStr = builder.Configuration["AzureStorage:ConnectionString"];
if (!string.IsNullOrEmpty(storageConnStr))
{
    var contentCacheTableClient = new TableClient(storageConnStr, "contentcache");
    contentCacheTableClient.CreateIfNotExists();
    builder.Services.AddSingleton<IDistributedContentCache>(sp =>
        new TableStorageContentCache(
            contentCacheTableClient,
            sp.GetRequiredService<ILogger<TableStorageContentCache>>()));
}
else
{
    builder.Services.AddSingleton<IDistributedContentCache, NullDistributedContentCache>();
}

// IWhatsAppSender: disabled — real WhatsApp API sending will be wired in Phase 3
// when Clients.WhatsApp is added to the Functions composition root.
// Note: Functions already references Clients.Azure (Phase 2). Clients.WhatsApp will be added in Phase 3.
builder.Services.AddSingleton<IWhatsAppSender, DisabledFunctionsWhatsAppSender>();

builder.Build().Run();

/// <summary>
/// No-op <see cref="IDistributedContentCache"/> used when AzureStorage:ConnectionString is not configured.
/// Returns null for all Gets and discards all Sets — content cache misses are safe (pipeline still runs).
/// </summary>
file sealed class NullDistributedContentCache : IDistributedContentCache
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
        => Task.FromResult((T?)null);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
        => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken ct)
        => Task.CompletedTask;
}

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
