using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Api.Logging;
using RealEstateStar.Api.Middleware;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.DataServices.WhatsApp;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using RealEstateStar.Api.Health;
using RealEstateStar.Workers.Lead.Orchestrator;
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.Services.AgentNotifier;
using RealEstateStar.Services.LeadCommunicator;
using RealEstateStar.Activities.Pdf;
using RealEstateStar.Clients.Anthropic;
using RealEstateStar.Clients.Azure;
using RealEstateStar.Clients.GDocs;
using RealEstateStar.Clients.GDrive;
using RealEstateStar.Clients.GSheets;
using RealEstateStar.Clients.Gmail;
using RealEstateStar.Clients.GoogleOAuth;
using RealEstateStar.Clients.Gws;
using RealEstateStar.Clients.Stripe;
using RealEstateStar.Clients.RentCast;
using RealEstateStar.Clients.Scraper;
using RealEstateStar.Clients.WhatsApp;
using RealEstateStar.DataServices;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Notifications;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.WhatsApp;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddStructuredLogging();
builder.AddObservability();

// Observability validation — fail fast if OTel exports to localhost in production
var otelEndpoint = builder.Configuration["Otel:Endpoint"] ?? "";
if (!builder.Environment.IsDevelopment() && !args.Contains("--export-openapi")
    && (otelEndpoint.Contains("localhost") || otelEndpoint.Contains("127.0.0.1")))
{
    throw new InvalidOperationException(
        $"Otel:Endpoint is '{otelEndpoint}' — telemetry would be lost in production. " +
        "Set OTEL_EXPORTER_OTLP_ENDPOINT secret in GitHub Actions to point to Grafana Cloud.");
}

// Agent config — Docker image uses /app/config/accounts, local dev uses relative path to repo root
var dockerConfigPath = Path.Combine(builder.Environment.ContentRootPath, "config", "accounts");
var localConfigPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "config", "accounts");
var configPath = Directory.Exists(dockerConfigPath) ? dockerConfigPath : localConfigPath;
Console.WriteLine($"[STARTUP] ContentRootPath: {builder.Environment.ContentRootPath}");
Console.WriteLine($"[STARTUP] DockerConfigPath exists: {Directory.Exists(dockerConfigPath)} ({dockerConfigPath})");
Console.WriteLine($"[STARTUP] LocalConfigPath exists: {Directory.Exists(localConfigPath)} ({localConfigPath})");
Console.WriteLine($"[STARTUP] Using configPath: {configPath}");
if (Directory.Exists(configPath))
{
    var dirs = Directory.GetDirectories(configPath);
    Console.WriteLine($"[STARTUP] Found {dirs.Length} agent config(s): {string.Join(", ", dirs.Select(Path.GetFileName))}");
}
else
{
    Console.Error.WriteLine($"[STARTUP-ERROR] Config directory does not exist: {configPath}");
}
builder.Services.AddDataServices(builder.Configuration, builder.Environment, configPath);

// Data Protection — encrypt OAuth tokens at rest
var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("RealEstateStar");

if (builder.Environment.IsDevelopment())
{
    // Persist DPAPI keys in dev so encrypted tokens survive restarts.
    // Without this, keys are ephemeral and tokens become undecryptable on restart.
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, ".dpapi-keys")));
}
else
{
    var kvUri = builder.Configuration["AzureKeyVault:VaultUri"];
    var blobUri = builder.Configuration["DataProtection:BlobUri"];
    if (!string.IsNullOrEmpty(kvUri) && !string.IsNullOrEmpty(blobUri))
    {
        dpBuilder
            .PersistKeysToAzureBlobStorage(new Uri(blobUri), new Azure.Identity.DefaultAzureCredential())
            .ProtectKeysWithAzureKeyVault(new Uri(kvUri + "/keys/dataprotection"), new Azure.Identity.DefaultAzureCredential());
    }
}

// HMAC authentication for lead endpoints (server-to-server from CF Worker)
builder.Services.Configure<ApiKeyHmacOptions>(builder.Configuration.GetSection("Hmac"));

// Warn if HMAC config is missing — middleware will skip auth gracefully
if (!builder.Environment.IsDevelopment())
{
    var hmacSection = builder.Configuration.GetSection("Hmac");
    var hmacSecret = hmacSection["HmacSecret"];
    var apiKeysSection = hmacSection.GetSection("ApiKeys");
    if (string.IsNullOrWhiteSpace(hmacSecret) || !apiKeysSection.GetChildren().Any())
    {
        Console.Error.WriteLine(
            "[STARTUP-WARN] Hmac:HmacSecret and/or Hmac:ApiKeys not configured. " +
            "HMAC authentication is disabled on lead endpoints.");
    }
}

// Consent HMAC signing (triple-write audit trail)
builder.Services.Configure<ConsentHmacOptions>(builder.Configuration.GetSection("Consent:Hmac"));

// Onboarding (session store registered early, services after config keys below)
builder.Services.AddSingleton<SessionDataService>();
builder.Services.AddSingleton<ISessionDataService>(sp =>
    new EncryptingSessionDecorator(
        sp.GetRequiredService<SessionDataService>(),
        sp.GetRequiredService<IDataProtectionProvider>(),
        sp.GetRequiredService<ILogger<EncryptingSessionDecorator>>()));
builder.Services.AddSingleton<OnboardingStateMachine>();

// Configuration keys
var anthropicKey = builder.Configuration["Anthropic:ApiKey"]
    ?? throw new InvalidOperationException("Anthropic:ApiKey configuration is required");
var googleClientId = builder.Configuration["Google:ClientId"]
    ?? throw new InvalidOperationException("Google:ClientId configuration is required");
var googleClientSecret = builder.Configuration["Google:ClientSecret"]
    ?? throw new InvalidOperationException("Google:ClientSecret configuration is required");
var googleRedirectUri = builder.Configuration["Google:RedirectUri"]
    ?? throw new InvalidOperationException("Google:RedirectUri configuration is required");

// Stripe config validation
_ = builder.Configuration["Stripe:SecretKey"]
    ?? throw new InvalidOperationException("Stripe:SecretKey configuration is required");
_ = builder.Configuration["Stripe:WebhookSecret"]
    ?? throw new InvalidOperationException("Stripe:WebhookSecret configuration is required");
_ = builder.Configuration["Stripe:PriceId"]
    ?? throw new InvalidOperationException("Stripe:PriceId configuration is required");
_ = builder.Configuration["Platform:BaseUrl"]
    ?? throw new InvalidOperationException("Platform:BaseUrl configuration is required");

// Cloudflare config for site deployment
var cloudflareOptions = new CloudflareOptions
{
    ApiToken = builder.Configuration["Cloudflare:ApiToken"] ?? "",
    AccountId = builder.Configuration["Cloudflare:AccountId"] ?? "",
};
if (!cloudflareOptions.IsValid())
{
    Log.Warning("Cloudflare:ApiToken and/or Cloudflare:AccountId not configured — site deployment will fail if attempted");
}

// Onboarding services (need anthropicKey)
var scraperApiKey = builder.Configuration["ScraperApi:ApiKey"];
if (string.IsNullOrEmpty(scraperApiKey))
    Log.Warning("ScraperApi:ApiKey not configured — profile scraping will use direct HTTP (may be blocked by Zillow/Realtor)");

// Polly resilience logger — resolved early so AddResilienceHandler callbacks can log
var pollyLoggerFactory = LoggerFactory.Create(lb => lb.AddSerilog(Log.Logger));
var pollyLogger = pollyLoggerFactory.CreateLogger("Polly");

// Shared Anthropic client — all Claude callers use this singleton via IAnthropicClient
builder.Services.AddHttpClient("Anthropic")
    .AddClaudeApiResilience(pollyLogger);
builder.Services.AddSingleton<IAnthropicClient>(sp =>
    new AnthropicClient(
        sp.GetRequiredService<IHttpClientFactory>(),
        anthropicKey,
        sp.GetRequiredService<ILogger<AnthropicClient>>()));

builder.Services.AddHttpClient(nameof(ProfileScraperService))
    .AddScraperApiResilience(pollyLogger);
builder.Services.AddSingleton<IDnsResolver, SystemDnsResolver>();
builder.Services.AddSingleton<IProfileScraperService>(sp =>
    new ProfileScraperService(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<IAnthropicClient>(),
        scraperApiKey,
        sp.GetRequiredService<IDnsResolver>(),
        sp.GetRequiredService<ILogger<ProfileScraperService>>()));
builder.Services.AddHttpClient(nameof(GoogleOAuthService))
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton(sp =>
    new GoogleOAuthService(
        sp.GetRequiredService<IHttpClientFactory>(),
        googleClientId,
        googleClientSecret,
        googleRedirectUri,
        sp.GetRequiredService<ILogger<GoogleOAuthService>>()));
builder.Services.AddSingleton<IOnboardingTool, GoogleAuthCardTool>();
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton(cloudflareOptions);
builder.Services.AddSingleton<ISiteDeployService>(sp =>
    new SiteDeployService(
        sp.GetRequiredService<ILogger<SiteDeployService>>(),
        sp.GetRequiredService<IProcessRunner>(),
        sp.GetRequiredService<CloudflareOptions>(),
        configPath));
builder.Services.AddSingleton<IOnboardingTool, ScrapeUrlTool>();
builder.Services.AddSingleton<IOnboardingTool, UpdateProfileTool>();
builder.Services.AddSingleton<IOnboardingTool, SetBrandingTool>();
builder.Services.AddSingleton<IOnboardingTool, DeploySiteTool>();
builder.Services.AddSingleton<IOnboardingTool, CreateStripeSessionTool>();
builder.Services.AddSingleton<ToolDispatcher>();
builder.Services.AddSingleton<IStripeService, StripeService>();
builder.Services.AddHttpClient(nameof(OnboardingChatService))
    .AddClaudeApiResilience(pollyLogger);
builder.Services.AddSingleton(sp =>
    new OnboardingChatService(
        sp.GetRequiredService<IHttpClientFactory>(),
        anthropicKey,
        sp.GetRequiredService<OnboardingStateMachine>(),
        sp.GetRequiredService<ToolDispatcher>(),
        sp.GetRequiredService<ILogger<OnboardingChatService>>()));
builder.Services.AddHostedService<TrialExpiryService>();

// Google Workspace service (Drive, Docs, Sheets, Gmail)
builder.Services.AddSingleton<IGwsService, GwsCliRunner>();

// --- Lead Feature Services ---

// Platform-tier document storage: Azure Blob when connection string is available, local filesystem fallback.
// Must stay in Program.cs — AzureBlobDocumentStore lives in Clients.Azure which DataServices cannot reference.
// Registered as keyed IDocumentStorageProvider so AddStorageProviders() can resolve it without a compile-time dep.
var storageConnStr = builder.Configuration["AzureStorage:ConnectionString"];
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

// FanOutStorageProvider + narrower interface forwarding (IDocumentStorageProvider, ISheetStorageProvider)
builder.Services.AddStorageProviders(builder.Configuration, builder.Environment);

// ITokenStore Azure override — must stay in Program.cs (AzureTableTokenStore is in Clients.Azure;
// DataServices cannot reference it). AddDataServices() registers the NullTokenStore default; this
// AddSingleton call replaces it when AzureStorage:ConnectionString is configured.
if (!string.IsNullOrEmpty(storageConnStr))
{
    builder.Services.AddSingleton<ITokenStore>(sp =>
    {
        var tableClient = new Azure.Data.Tables.TableClient(storageConnStr, "oauthtokens");
        return new AzureTableTokenStore(
            tableClient,
            sp.GetRequiredService<IDataProtectionProvider>(),
            sp.GetRequiredService<ILogger<AzureTableTokenStore>>());
    });
}

// Gmail API client — IGmailSender backed by GmailApiClient (needs IOAuthRefresher)
builder.Services.AddHttpClient("GoogleOAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<IOAuthRefresher>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleOAuth");
    return new GoogleOAuthRefresher(
        sp.GetRequiredService<ITokenStore>(),
        googleClientId,
        googleClientSecret,
        httpClient,
        sp.GetRequiredService<ILogger<GoogleOAuthRefresher>>());
});
builder.Services.AddGmailSender(googleClientId, googleClientSecret);

// Google Drive, Docs, Sheets API clients — all backed by IOAuthRefresher
builder.Services.AddGDriveClient(googleClientId, googleClientSecret);
builder.Services.AddGDocsClient(googleClientId, googleClientSecret);
builder.Services.AddGSheetsClient(googleClientId, googleClientSecret);

// Scraper client (options, IScraperClient, HttpClient "ScraperAPI" with resilience)
builder.Services.AddScraperClient(builder.Configuration, pollyLogger);

// Lead pipeline — new decomposed orchestrator wiring
// TODO: Phase 5 — register IDiagnosticsProvider implementations via AddAllDiagnosticsProviders()
builder.Services.AddLeadOrchestrator();
builder.Services.AddPdfService();
builder.Services.AddAgentNotifier();
builder.Services.AddLeadCommunicator();

// RentCast client (options, IRentCastClient, HttpClient "RentCast" with resilience)
builder.Services.AddRentCastClient(builder.Configuration, pollyLogger);

// CMA pipeline (channel, worker, RentCastCompSource, ICompSource, ICompAggregator, ICmaAnalyzer)
builder.Services.AddCmaPipeline();

// Home search pipeline (channel, worker, IHomeSearchProvider)
builder.Services.AddHomeSearchPipeline(builder.Configuration);

var rentCastKey = builder.Configuration["RentCast:ApiKey"];
if (!string.IsNullOrWhiteSpace(rentCastKey))
    Log.Information("[STARTUP-090] RentCast API configured. Monthly limit warning threshold: {Percent}%",
        builder.Configuration.GetValue<int>("RentCast:MonthlyLimitWarningPercent", 80));
else
    Log.Warning("[STARTUP-091] RentCast:ApiKey not configured — CMA comp fetch will return empty results");

// Image resolver — local-first (agent-site public dir → live site HTTP fallback)
builder.Services.AddHttpClient("image-resolver", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RealEstateStar-CmaPdfGenerator/1.0");
});
builder.Services.AddSingleton<IImageResolver, LocalFirstImageResolver>();

// Named HttpClient used by GoogleChat resilience (ScraperAPI handled by AddScraperClient above)
builder.Services.AddHttpClient("GoogleChat")
    .AddGoogleChatResilience(pollyLogger);

// ------------------------------------------------------------------
// WhatsApp config validation
// ------------------------------------------------------------------
var whatsAppPhoneNumberId = builder.Configuration["WhatsApp:PhoneNumberId"];
var whatsAppAccessToken = builder.Configuration["WhatsApp:AccessToken"];
var whatsAppAppSecret = builder.Configuration["WhatsApp:AppSecret"];
var whatsAppVerifyToken = builder.Configuration["WhatsApp:VerifyToken"];
var whatsAppWabaId = builder.Configuration["WhatsApp:WabaId"];

if (string.IsNullOrEmpty(whatsAppPhoneNumberId))
    Log.Warning("WhatsApp:PhoneNumberId not configured — WhatsApp notifications disabled");

// Notification services (IEmailTemplateRenderer, IEmailNotifier)
builder.Services.AddNotificationServices();

// ------------------------------------------------------------------
// WhatsApp services — intent/response/conversation stubs + conditional
// hosted services are registered by AddWhatsAppWorkers.
// Azure queue/table, IWhatsAppSender, IWhatsAppNotifier, IConversationLogger
// must stay here (they depend on Clients.WhatsApp which Workers.* cannot reference).
// ------------------------------------------------------------------
builder.Services.AddWhatsAppWorkers(builder.Configuration);

builder.Services.AddSingleton<IConversationLogger, ConversationLogger>();

builder.Services.AddHttpClient("WhatsApp", client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/v20.0/");
}).AddWhatsAppResilience();

if (!string.IsNullOrEmpty(whatsAppPhoneNumberId))
{
    builder.Services.AddSingleton<IWhatsAppSender>(sp =>
        new WhatsAppApiClient(
            sp.GetRequiredService<IHttpClientFactory>(),
            whatsAppPhoneNumberId,
            whatsAppAccessToken!,
            sp.GetRequiredService<ILogger<WhatsAppApiClient>>()));
    builder.Services.AddSingleton<WhatsAppIdempotencyStore>();
    builder.Services.AddSingleton<IWhatsAppNotifier, WhatsAppNotifier>();

    // Azure Storage — required when WhatsApp is enabled
    var storageConnectionString = builder.Configuration["AzureStorage:ConnectionString"]
        ?? throw new InvalidOperationException(
            "AzureStorage:ConnectionString required when WhatsApp is enabled");

    builder.Services.AddSingleton(new Azure.Storage.Queues.QueueClient(
        storageConnectionString,
        "whatsapp-webhooks",
        new Azure.Storage.Queues.QueueClientOptions
        {
            MessageEncoding = Azure.Storage.Queues.QueueMessageEncoding.Base64
        }));
    builder.Services.AddSingleton<IWebhookQueueService, AzureWebhookQueueService>();

    builder.Services.AddSingleton(new Azure.Data.Tables.TableClient(
        storageConnectionString, "whatsappaudit"));
    builder.Services.AddSingleton<IWhatsAppAuditService, AzureWhatsAppAuditService>();
}
else
{
    // Register null-object implementations so any endpoint that resolves
    // IWhatsAppNotifier / IWhatsAppAuditService / IWebhookQueueService / IWhatsAppSender
    // still compiles and fails gracefully with a clear log rather than a DI exception.
    builder.Services.AddSingleton<IWhatsAppSender, DisabledWhatsAppSender>();
    builder.Services.AddSingleton<IWhatsAppNotifier, DisabledWhatsAppNotifier>();
    builder.Services.AddSingleton<IWhatsAppAuditService, DisabledWhatsAppAuditService>();
    builder.Services.AddSingleton<IWebhookQueueService, DisabledWebhookQueueService>();
    builder.Services.AddSingleton<WhatsAppIdempotencyStore>();
}

// Memory cache for WhatsApp 24hr window tracking + any future caching
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 100_000;
});

// OpenAPI
builder.Services.AddOpenApi();

// Problem details for validation errors
builder.Services.AddProblemDetails();

// SignalR
builder.Services.AddSignalR();

// Health checks
builder.Services.AddSingleton<BackgroundServiceHealthTracker>();
builder.Services.AddHealthChecks()
    .AddCheck<OtlpExportHealthCheck>("otlp_export", tags: ["ready"])
    .AddCheck<ClaudeApiHealthCheck>("claude_api", tags: ["ready"])
    .AddCheck<GoogleDriveHealthCheck>("google_drive", tags: ["ready"])
    .AddCheck<ScraperApiHealthCheck>("scraper_api", tags: ["ready"])
    .AddCheck<RentCastHealthCheck>("rentcast_api", tags: ["ready"])
    .AddCheck<TurnstileHealthCheck>("turnstile", tags: ["ready"])
    .AddCheck<BackgroundServiceHealthCheck>("background_workers", tags: ["ready", "workers"]);

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000", "http://localhost:3001"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (allowedOrigins.Contains(origin)) return true;

                // Allow *.localhost:{port} subdomains in development (e.g. jenise-buckalew.localhost:3000)
                if (builder.Environment.IsDevelopment())
                {
                    var uri = new Uri(origin);
                    return uri.Host == "localhost" || uri.Host.EndsWith(".localhost");
                }

                if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                {
                    // Allow agent site subdomains (*.real-estate-star.com)
                    if (originUri.Host.EndsWith(".real-estate-star.com", StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Allow Cloudflare Workers preview deploys (*.workers.dev)
                    if (originUri.Host.EndsWith(".workers.dev", StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
});

// ForwardedHeaders — must be configured before rate limiter so RemoteIpAddress is correct behind proxy.
// KnownIPNetworks and KnownProxies are cleared because Cloudflare + Azure Container Apps use rotating IPs
// that cannot be enumerated statically. All forwarded headers from the proxy chain are trusted.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global: 100 requests per minute per IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Session creation: 5 per hour per IP (session creation triggers Claude API calls)
    options.AddPolicy("session-create", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1)
            }));

    // Chat messages: 20 per minute per session ID
    options.AddPolicy("chat-message", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Request.RouteValues["sessionId"]?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Lead creation: 20 per hour per IP
    options.AddPolicy("lead-create", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromHours(1) }));

    // Deletion requests: 5 per hour per IP
    options.AddPolicy("deletion-request", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1) }));

    // Data deletion: 10 per hour per IP
    options.AddPolicy("delete-data", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));

    // Lead opt-out: 10 per hour per IP
    options.AddPolicy("lead-opt-out", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));

    // Lead export: 10 per hour per IP
    options.AddPolicy("lead-export", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));

    // Lead delete: 10 per hour per IP
    options.AddPolicy("lead-delete", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));

    // Telemetry events: 60 per minute per IP (one event per second per visitor, generous for analytics)
    options.AddPolicy("telemetry", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

// Endpoint auto-registration
builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

var app = builder.Build();

// Global exception handler — returns RFC 7807 ProblemDetails
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();

        if (exceptionFeature is not null)
        {
            logger.LogError(exceptionFeature.Error, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://tools.ietf.org/html/rfc7807"
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

// ForwardedHeaders MUST be first — it resolves the real client IP from X-Forwarded-For
// so that rate limiting, logging, and all downstream middleware see the correct RemoteIpAddress.
app.UseForwardedHeaders();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiKeyHmacMiddleware>();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Api-Version"] = "1.0";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = AgentIdEnricher.EnrichFromRequest;
});

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();

// Liveness probe — no dependency checks, just "am I running?"
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness probe — checks external dependencies
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
});

// Worker health — checks background service activity
app.MapHealthChecks("/health/workers", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("workers"),
    ResponseWriter = WriteHealthResponse
});

// OpenAPI spec
app.MapOpenApi();

// --- All Endpoints ---
app.MapEndpoints();

// Agent transfer — placeholder until inter-agent handoff is implemented
app.MapPost("/internal/agents/{agentId}/transfer", () => Results.StatusCode(501));

// CLI: dotnet run -- --export-openapi [path]
// Starts the server briefly, fetches the OpenAPI spec, writes to file, and exits.
if (args.Contains("--export-openapi"))
{
    var outputPath = args.SkipWhile(a => a != "--export-openapi").Skip(1).FirstOrDefault()
        ?? "openapi.json";
    var cts = new CancellationTokenSource();
    _ = app.RunAsync(cts.Token);
    using var http = new HttpClient();
    // Use the first configured URL from launchSettings or default
    var specUrl = "http://localhost:5135/openapi/v1.json";
    for (var i = 0; i < 20; i++)
    {
        try
        {
            var spec = await http.GetStringAsync(specUrl);
            await File.WriteAllTextAsync(outputPath, spec);
            Console.WriteLine($"OpenAPI spec written to {Path.GetFullPath(outputPath)}");
            cts.Cancel();
            return;
        }
        catch (HttpRequestException)
        {
            await Task.Delay(500);
        }
    }
    Console.Error.WriteLine("Failed to fetch OpenAPI spec after retries");
    cts.Cancel();
    Environment.Exit(1);
}

app.Run();

static async Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var result = new
    {
        status = report.Status.ToString(),
        entries = report.Entries.ToDictionary(
            e => e.Key,
            e => new
            {
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.ToString()
            })
    };
    await context.Response.WriteAsJsonAsync(result);
}

// Startup config — guard clauses tested via deploy health checks, not unit tests
[ExcludeFromCodeCoverage]
public partial class Program;
