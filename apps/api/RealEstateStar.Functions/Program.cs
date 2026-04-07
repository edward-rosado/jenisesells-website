using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RealEstateStar.Activities.Activation.BrandMerge;
using RealEstateStar.Activities.Activation.ContactImportPersist;
using RealEstateStar.Activities.Activation.PersistAgentProfile;
using RealEstateStar.Activities.Lead.ContactDetection;
using RealEstateStar.Activities.Pdf;
using RealEstateStar.Clients.Anthropic;
using RealEstateStar.Clients.Azure;
using RealEstateStar.Clients.GDocs;
using RealEstateStar.Clients.GDrive;
using RealEstateStar.Clients.GSheets;
using RealEstateStar.Clients.Gmail;
using RealEstateStar.Clients.GoogleOAuth;
using RealEstateStar.Clients.RentCast;
using RealEstateStar.Clients.Gws;
using RealEstateStar.Clients.Scraper;
using RealEstateStar.DataServices;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Services.AgentConfig;
using RealEstateStar.Services.AgentNotifier;
using RealEstateStar.Services.BrandMerge;
using RealEstateStar.Services.LeadCommunicator;
using RealEstateStar.Services.WelcomeNotification;
using RealEstateStar.Workers.Activation.Orchestrator;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Workers.Lead.Orchestrator;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.WhatsApp;
using RealEstateStar.Domain.Shared;
using Microsoft.AspNetCore.DataProtection;
using Serilog;

try
{
var builder = FunctionsApplication.CreateBuilder(args);

// NOTE: ConfigureFunctionsWebApplication() removed — ASP.NET Core HTTP proxying
// middleware may not work on Azure Linux Consumption plan. Using the simpler
// HttpRequestData/HttpResponseData model instead (no IActionResult, no HttpRequest).

// Serilog with OTLP log export — mirrors Api's AddStructuredLogging().
// Without this, logs only go to console (lost on Consumption plan).
var serilogConfig = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "RealEstateStar.Functions")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

var otlpLogEndpoint = builder.Configuration["Otel:Endpoint"];
var otlpLogHeaders = builder.Configuration["Otel:Headers"];
if (!string.IsNullOrEmpty(otlpLogEndpoint))
{
    serilogConfig.WriteTo.OpenTelemetry(opts =>
    {
        opts.Endpoint = otlpLogEndpoint;
        opts.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
        if (!string.IsNullOrEmpty(otlpLogHeaders))
        {
            var parts = otlpLogHeaders.Split('=', 2);
            if (parts.Length == 2)
                opts.Headers = new Dictionary<string, string> { [parts[0]] = parts[1] };
        }
        opts.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "real-estate-star-functions"
        };
    });
}

serilogConfig
    .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning);

Log.Logger = serilogConfig.CreateLogger();
builder.Services.AddSerilog();

// ── Observability ─────────────────────────────────────────────────────────────
var rawOtelEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317";
var otlpHeaders = builder.Configuration["Otel:Headers"] ?? "";
var useHttpProtobuf = !string.IsNullOrEmpty(otlpHeaders);
var otlpBase = new Uri(rawOtelEndpoint.TrimEnd('/') + "/");
var tracesEndpoint = useHttpProtobuf ? new Uri(otlpBase, "v1/traces") : otlpBase;
var metricsEndpoint = useHttpProtobuf ? new Uri(otlpBase, "v1/metrics") : otlpBase;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("real-estate-star-functions"))
    .WithTracing(tracing => tracing
        .AddSource(LeadDiagnostics.ServiceName)
        .AddSource(CmaDiagnostics.ServiceName)
        .AddSource(HomeSearchDiagnostics.ServiceName)
        .AddSource(OrchestratorDiagnostics.ServiceName)
        .AddSource(ScraperDiagnostics.ServiceName)
        .AddSource(WhatsAppDiagnostics.ServiceName)
        .AddSource(ClaudeDiagnostics.ServiceName)
        .AddSource(GmailDiagnostics.ServiceName)
        .AddSource(GDriveDiagnostics.ServiceName)
        .AddSource(GDocsDiagnostics.ServiceName)
        .AddSource(GSheetsDiagnostics.ServiceName)
        .AddSource(TokenStoreDiagnostics.ServiceName)
        .AddSource(QueueDiagnostics.ServiceName)
        .AddSource(FanOutDiagnostics.ServiceName)
        .AddSource(RentCastDiagnostics.ServiceName)
        .AddSource("RealEstateStar.Pdf")
        .AddSource("RealEstateStar.LeadCommunicator")
        .AddSource("RealEstateStar.AgentNotifier")
        .AddSource("RealEstateStar.Activation")
        .AddSource("RealEstateStar.AgentContext")
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = tracesEndpoint;
            options.Protocol = useHttpProtobuf ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
            if (!string.IsNullOrEmpty(otlpHeaders))
                options.Headers = otlpHeaders;
        }))
    .WithMetrics(metrics => metrics
        .AddMeter(LeadDiagnostics.ServiceName)
        .AddMeter(CmaDiagnostics.ServiceName)
        .AddMeter(HomeSearchDiagnostics.ServiceName)
        .AddMeter(OrchestratorDiagnostics.ServiceName)
        .AddMeter(ScraperDiagnostics.ServiceName)
        .AddMeter(WhatsAppDiagnostics.ServiceName)
        .AddMeter(ClaudeDiagnostics.ServiceName)
        .AddMeter(GmailDiagnostics.ServiceName)
        .AddMeter(GDriveDiagnostics.ServiceName)
        .AddMeter(GDocsDiagnostics.ServiceName)
        .AddMeter(GSheetsDiagnostics.ServiceName)
        .AddMeter(TokenStoreDiagnostics.ServiceName)
        .AddMeter(QueueDiagnostics.ServiceName)
        .AddMeter(FanOutDiagnostics.ServiceName)
        .AddMeter(RentCastDiagnostics.ServiceName)
        .AddMeter("RealEstateStar.Pdf")
        .AddMeter("RealEstateStar.LeadCommunicator")
        .AddMeter("RealEstateStar.AgentNotifier")
        .AddMeter("RealEstateStar.Activation")
        .AddMeter("RealEstateStar.AgentContext")
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = metricsEndpoint;
            options.Protocol = useHttpProtobuf ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
            if (!string.IsNullOrEmpty(otlpHeaders))
                options.Headers = otlpHeaders;
        }));

// ── Phase 1: WhatsApp webhook + retry functions ──────────────────────────────
// Register IConversationHandler, IIntentClassifier, IResponseGenerator, WhatsAppRetryJob.
// Feature flag Features:WhatsApp:UseBackgroundService is always false in the Functions host
// (there are no BackgroundServices here — queue/timer triggers handle that role).
builder.Configuration["Features:WhatsApp:UseBackgroundService"] = "false";
builder.Services.AddWhatsAppWorkers(builder.Configuration);

// IWhatsAppAuditService: disabled implementation — real Azure Table audit will be wired
// in Phase 3 when Clients.WhatsApp is added to the Functions composition root.
builder.Services.AddSingleton<IWhatsAppAuditService, DisabledWhatsAppAuditService>();

// IWebhookQueueService: disabled — queue messages arrive via QueueTrigger binding,
// not through IWebhookQueueService.DequeueAsync. Registered for DI completeness.
builder.Services.AddSingleton<IWebhookQueueService, DisabledWebhookQueueService>();

// IWhatsAppSender: disabled — real WhatsApp API sending will be wired in Phase 3
// when Clients.WhatsApp is added to the Functions composition root.
builder.Services.AddSingleton<IWhatsAppSender, DisabledFunctionsWhatsAppSender>();

// ── Configuration key warnings ────────────────────────────────────────────────
var anthropicKey = builder.Configuration["Anthropic:ApiKey"];
if (string.IsNullOrWhiteSpace(anthropicKey))
    Log.Warning("[STARTUP-WARN] Anthropic:ApiKey not configured — Claude API calls will fail");

var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];
if (string.IsNullOrWhiteSpace(googleClientId) || string.IsNullOrWhiteSpace(googleClientSecret))
    Log.Warning("[STARTUP-WARN] Google:ClientId or Google:ClientSecret not configured — Google API calls will fail");

var scraperApiKey = builder.Configuration["ScraperApi:ApiKey"];
if (string.IsNullOrEmpty(scraperApiKey))
    Log.Warning("[STARTUP-WARN] ScraperApi:ApiKey not configured — profile scraping will use direct HTTP");

var rentCastKey = builder.Configuration["RentCast:ApiKey"];
if (string.IsNullOrWhiteSpace(rentCastKey))
    Log.Warning("[STARTUP-WARN] RentCast:ApiKey not configured — CMA comp fetch will return empty results");

// ── Storage: IDistributedContentCache ──────────────────────────────────────
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

// ── Azure queues: IActivationQueue + ILeadOrchestrationQueue ─────────────────
if (!string.IsNullOrEmpty(storageConnStr))
{
    var activationQueueClient = new QueueClient(
        storageConnStr,
        "activation-requests",
        new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
    activationQueueClient.CreateIfNotExists();
    builder.Services.AddSingleton<RealEstateStar.Domain.Activation.Interfaces.IActivationQueue>(sp =>
        new AzureQueueActivationStore(
            activationQueueClient,
            sp.GetRequiredService<ILogger<AzureQueueActivationStore>>()));

    var leadQueueClient = new QueueClient(
        storageConnStr,
        "lead-requests",
        new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
    leadQueueClient.CreateIfNotExists();
    builder.Services.AddSingleton<RealEstateStar.Domain.Leads.Interfaces.ILeadOrchestrationQueue>(sp =>
        new AzureQueueLeadStore(
            leadQueueClient,
            sp.GetRequiredService<ILogger<AzureQueueLeadStore>>()));

    Log.Information("[STARTUP-085] Durable queues: activation-requests, lead-requests (Azure Queue Storage)");
}
else
{
    builder.Services.AddSingleton<RealEstateStar.Domain.Activation.Interfaces.IActivationQueue, InMemoryActivationQueue>();
    builder.Services.AddSingleton<RealEstateStar.Domain.Leads.Interfaces.ILeadOrchestrationQueue, InMemoryLeadOrchestrationQueue>();
    Log.Warning("[STARTUP-086] AzureStorage:ConnectionString not configured — queues are in-memory (messages lost on restart)");
}

// ── IIdempotencyStore ─────────────────────────────────────────────────────────
if (!string.IsNullOrEmpty(storageConnStr))
{
    builder.Services.AddSingleton<IIdempotencyStore>(sp =>
    {
        var tableClient = new TableClient(storageConnStr, "idempotency");
        tableClient.CreateIfNotExists();
        return new TableStorageIdempotencyStore(
            tableClient,
            sp.GetRequiredService<ILogger<TableStorageIdempotencyStore>>());
    });
    Log.Information("[STARTUP-087] IIdempotencyStore: TableStorageIdempotencyStore (table: idempotency)");
}
else
{
    builder.Services.AddSingleton<IIdempotencyStore, NullIdempotencyStore>();
    Log.Warning("[STARTUP-088] AzureStorage:ConnectionString not configured — idempotency store is no-op");
}

// ── Platform-tier document storage (AzureBlobDocumentStore when available) ───
// Must stay in Program.cs — AzureBlobDocumentStore lives in Clients.Azure which DataServices cannot reference.
if (!string.IsNullOrEmpty(storageConnStr))
{
    builder.Services.AddKeyedSingleton<IDocumentStorageProvider>(
        StorageServiceCollectionExtensions.PlatformDocumentStoreKey,
        (sp, _) => new AzureBlobDocumentStore(
            new Azure.Storage.Blobs.BlobContainerClient(storageConnStr, "lead-documents"),
            sp.GetRequiredService<ILogger<AzureBlobDocumentStore>>()));
    Log.Information("[STARTUP-080] Platform document store: AzureBlobDocumentStore (container: lead-documents)");
}
else
{
    Log.Warning("[STARTUP-081] AzureStorage:ConnectionString not configured — Platform tier using LocalStorageProvider");
}

// ── Staged content provider (ephemeral blob staging for activation Drive contents) ──
if (!string.IsNullOrEmpty(storageConnStr))
{
    builder.Services.AddSingleton<RealEstateStar.Domain.Activation.Interfaces.IStagedContentProvider>(sp =>
        new BlobStagedContentProvider(
            new Azure.Storage.Blobs.BlobContainerClient(storageConnStr, "lead-documents"),
            sp.GetRequiredService<ILogger<BlobStagedContentProvider>>()));
    Log.Information("[STARTUP-089] IStagedContentProvider: BlobStagedContentProvider (container: lead-documents)");
}

builder.Services.AddStorageProviders(builder.Configuration, builder.Environment);

// ── Agent config path ─────────────────────────────────────────────────────────
var dockerConfigPath = Path.Combine(builder.Environment.ContentRootPath, "config", "accounts");
var localConfigPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "config", "accounts");
var configPath = Directory.Exists(dockerConfigPath) ? dockerConfigPath : localConfigPath;
if (!Directory.Exists(configPath))
    Log.Debug("[STARTUP-DEBUG] Agent config directory not found: {ConfigPath} (expected on Azure — config loaded from blob)", configPath);

// ── Data Protection — must match Api's key ring to decrypt OAuth tokens ──────
var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("RealEstateStar");

var dpBlobUri = builder.Configuration["DataProtection:BlobUri"];
if (!string.IsNullOrEmpty(dpBlobUri) && !string.IsNullOrEmpty(storageConnStr))
{
    // Use connection-string auth (no Managed Identity needed on Consumption plan)
    var blobUri = new Uri(dpBlobUri);
    var containerName = blobUri.Segments.Length > 1 ? blobUri.Segments[1].TrimEnd('/') : "dataprotection";
    var blobName = blobUri.Segments.Length > 2 ? string.Join("", blobUri.Segments[2..]) : "keys.xml";
    var blobClient = new Azure.Storage.Blobs.BlobContainerClient(storageConnStr, containerName)
        .GetBlobClient(blobName);
    dpBuilder.PersistKeysToAzureBlobStorage(blobClient);
    Log.Information("[STARTUP-082] DataProtection keys: shared blob at {BlobUri} (connection string auth)", dpBlobUri);
}
else if (!string.IsNullOrEmpty(dpBlobUri))
{
    // Fallback: Managed Identity / DefaultAzureCredential
    dpBuilder.PersistKeysToAzureBlobStorage(new Uri(dpBlobUri), new Azure.Identity.DefaultAzureCredential());
    Log.Information("[STARTUP-082] DataProtection keys: shared blob at {BlobUri} (DefaultAzureCredential)", dpBlobUri);
}
else
{
    Log.Warning("[STARTUP-083] No DataProtection:BlobUri — token decryption will fail for encrypted tokens");
}

// ── DataServices ──────────────────────────────────────────────────────────────
builder.Services.AddDataServices(builder.Configuration, builder.Environment, configPath);

// ── ITokenStore — AzureTableTokenStore to read/refresh encrypted OAuth tokens ─
if (!string.IsNullOrEmpty(storageConnStr))
{
    builder.Services.AddSingleton<ITokenStore>(sp =>
    {
        var tableClient = new TableClient(storageConnStr, "oauthtokens");
        return new AzureTableTokenStore(
            tableClient,
            sp.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(),
            sp.GetRequiredService<ILogger<AzureTableTokenStore>>());
    });
    Log.Information("[STARTUP-084] ITokenStore: AzureTableTokenStore (table: oauthtokens)");
}

// ── Memory cache ───────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Polly resilience logger ────────────────────────────────────────────────────
var pollyLoggerFactory = LoggerFactory.Create(lb => lb.AddSerilog(Log.Logger));
var pollyLogger = pollyLoggerFactory.CreateLogger("Polly");

// ── IAnthropicClient ──────────────────────────────────────────────────────────
// NOTE: AddClaudeApiResilience is in RealEstateStar.Api.Infrastructure and cannot be referenced here.
// Using plain HttpClient — Durable Functions has its own retry semantics via orchestrator retries.
builder.Services.AddHttpClient("Anthropic");
builder.Services.AddSingleton<IAnthropicClient>(sp =>
    new AnthropicClient(
        sp.GetRequiredService<IHttpClientFactory>(),
        anthropicKey ?? string.Empty,
        sp.GetRequiredService<ILogger<AnthropicClient>>()));

// ── IOAuthRefresher + Google clients ──────────────────────────────────────────
builder.Services.AddHttpClient("GoogleOAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<IOAuthRefresher>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleOAuth");
    return new GoogleOAuthRefresher(
        sp.GetRequiredService<ITokenStore>(),
        googleClientId ?? string.Empty,
        googleClientSecret ?? string.Empty,
        httpClient,
        sp.GetRequiredService<ILogger<GoogleOAuthRefresher>>());
});
builder.Services.AddGmailSender(googleClientId ?? string.Empty, googleClientSecret ?? string.Empty);
builder.Services.AddGmailReader(googleClientId ?? string.Empty, googleClientSecret ?? string.Empty);
builder.Services.AddGDriveClient(googleClientId ?? string.Empty, googleClientSecret ?? string.Empty);
builder.Services.AddGDocsClient(googleClientId ?? string.Empty, googleClientSecret ?? string.Empty);
builder.Services.AddGSheetsClient(googleClientId ?? string.Empty, googleClientSecret ?? string.Empty);

// ── Google Workspace service (Drive, Docs, Sheets, Gmail) ─────────────────────
builder.Services.AddSingleton<IGwsService, GwsCliRunner>();

// ── Scraper client ─────────────────────────────────────────────────────────────
builder.Services.AddScraperClient(builder.Configuration, pollyLogger);

// ── RentCast client ────────────────────────────────────────────────────────────
builder.Services.AddRentCastClient(builder.Configuration, pollyLogger);

// ── Image resolver (used by CmaPdfGenerator) ──────────────────────────────────
// LocalFirstImageResolver requires IWebHostEnvironment (ASP.NET Core specific — not available in Functions host).
// Register a no-op resolver; PDF renders without agent images.
// TODO(phase-5): Move LocalFirstImageResolver to Activities.Pdf or create a Functions-compatible variant.
builder.Services.AddHttpClient("image-resolver", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RealEstateStar-CmaPdfGenerator/1.0");
});
builder.Services.AddSingleton<IImageResolver, NullImageResolver>();

// ── Lead pipeline services ────────────────────────────────────────────────────
builder.Services.AddLeadOrchestrator();
builder.Services.AddPdfService();
builder.Services.AddAgentNotifier();
builder.Services.AddLeadCommunicator();
builder.Services.AddAgentConfigService();
builder.Services.AddCmaPipeline();
builder.Services.AddHomeSearchPipeline(builder.Configuration);

// Named HttpClient for AgentDiscovery worker (used during activation)
builder.Services.AddHttpClient("AgentDiscovery", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (compatible; RealEstateStar-Activation/1.0; +https://real-estate-star.com)");
});

// ── Activation pipeline services ─────────────────────────────────────────────
builder.Services.AddActivationPipeline();
builder.Services.AddBrandMergeService();
builder.Services.AddWelcomeNotificationService();
builder.Services.AddPersistAgentProfileActivity();
builder.Services.AddBrandMergeActivity();
builder.Services.AddContactImportPersistActivity();
builder.Services.AddTransient<ContactDetectionActivity>();

var app = builder.Build();
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation("[STARTUP] Functions app built successfully. Starting host...");
app.Run();
}
catch (Exception ex)
{
    // Write startup error to Azure Table for debugging (Functions host silently swallows worker crashes)
    try
    {
        var connStr = Environment.GetEnvironmentVariable("AzureStorage__ConnectionString")
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (!string.IsNullOrEmpty(connStr))
        {
            var tableClient = new Azure.Data.Tables.TableClient(connStr, "functionsstartuperrors");
            tableClient.CreateIfNotExists();
            tableClient.UpsertEntity(new Azure.Data.Tables.TableEntity("startup", DateTime.UtcNow.ToString("o"))
            {
                ["Error"] = ex.ToString()[..Math.Min(ex.ToString().Length, 32000)],
                ["Message"] = ex.Message,
                ["Type"] = ex.GetType().FullName
            });
        }
    }
    catch { /* best effort */ }
    throw;
}

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

/// <summary>
/// No-op <see cref="IImageResolver"/> for the Functions host.
/// LocalFirstImageResolver requires <c>IWebHostEnvironment</c> (ASP.NET Core) which is unavailable here.
/// PDF generation proceeds without agent headshots/logos.
/// TODO(phase-5): Replace with a Functions-compatible implementation.
/// </summary>
file sealed class NullImageResolver : IImageResolver
{
    public Task<byte[]?> ResolveAsync(string handle, string relativePath, CancellationToken ct)
        => Task.FromResult((byte[]?)null);
}
