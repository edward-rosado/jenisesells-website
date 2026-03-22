using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Api.Logging;
using RealEstateStar.Api.Middleware;
using RealEstateStar.DataServices.Config;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Domain.Notifications.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Notifications.Templates;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using RealEstateStar.Api.Health;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using RealEstateStar.Workers.Cma;
using RealEstateStar.Workers.HomeSearch;
using RealEstateStar.Workers.Leads;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.WhatsApp;
using RealEstateStar.Clients.Gws;
using RealEstateStar.Clients.WhatsApp;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddStructuredLogging();
builder.AddObservability();

// Agent config
var configPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "config", "accounts");
builder.Services.AddSingleton<IAccountConfigService>(sp =>
    new AccountConfigService(configPath, sp.GetRequiredService<ILogger<AccountConfigService>>()));

// Data Protection — encrypt OAuth tokens at rest
var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("RealEstateStar");

if (!builder.Environment.IsDevelopment())
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

// Validate HMAC config in production — empty ApiKeys silently disables auth
if (!builder.Environment.IsDevelopment())
{
    var hmacSection = builder.Configuration.GetSection("Hmac");
    var hmacSecret = hmacSection["HmacSecret"];
    var apiKeysSection = hmacSection.GetSection("ApiKeys");
    if (string.IsNullOrWhiteSpace(hmacSecret) || !apiKeysSection.GetChildren().Any())
    {
        throw new InvalidOperationException(
            "Hmac:HmacSecret and Hmac:ApiKeys must be configured in non-Development environments. " +
            "Empty ApiKeys silently disables HMAC authentication on all lead endpoints.");
    }
}

// Consent HMAC signing (triple-write audit trail)
builder.Services.Configure<ConsentHmacOptions>(builder.Configuration.GetSection("Consent:Hmac"));

// Onboarding (session store registered early, services after config keys below)
builder.Services.AddSingleton<JsonFileSessionStore>();
builder.Services.AddSingleton<ISessionStore>(sp =>
    new EncryptingSessionStoreDecorator(
        sp.GetRequiredService<JsonFileSessionStore>(),
        sp.GetRequiredService<IDataProtectionProvider>(),
        sp.GetRequiredService<ILogger<EncryptingSessionStoreDecorator>>()));
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

// Stripe config validation (StripeService constructor validates details)
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

builder.Services.AddHttpClient(nameof(ProfileScraperService))
    .AddClaudeApiResilience(pollyLogger)
    .AddScraperApiResilience(pollyLogger);
builder.Services.AddSingleton<IDnsResolver, SystemDnsResolver>();
builder.Services.AddSingleton<IProfileScraperService>(sp =>
    new ProfileScraperService(
        sp.GetRequiredService<IHttpClientFactory>(),
        anthropicKey,
        scraperApiKey,
        sp.GetRequiredService<IDnsResolver>(),
        sp.GetRequiredService<ILogger<ProfileScraperService>>()));
builder.Services.AddHttpClient(nameof(GoogleOAuthService));
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

// Storage provider — local by default, GDrive requires per-request agentEmail (scoped)
// GDriveStorageProvider needs agentEmail from route, so it's resolved per-request via endpoint logic.
// For DI container, register LocalStorageProvider as the default singleton.
builder.Services.AddSingleton<IFileStorageProvider>(sp =>
    new LocalStorageProvider(
        builder.Configuration["Storage:BasePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "data")));

// Lead feature services
builder.Services.AddSingleton<ILeadStore, GDriveLeadStore>();
builder.Services.AddSingleton<IMarketingConsentLog, MarketingConsentLog>();
builder.Services.AddSingleton<ILeadDataDeletion, GDriveLeadDataDeletion>();
builder.Services.AddSingleton<IDeletionAuditLog, DeletionAuditLog>();
builder.Services.AddSingleton<ILeadNotifier, MultiChannelLeadNotifier>();

// Compliance consent triple-write services
// IComplianceFileStorageProvider: service-account Drive in prod, local filesystem in dev
builder.Services.AddSingleton<IComplianceFileStorageProvider>(sp =>
    new LocalComplianceStorageProvider(
        builder.Configuration["Storage:ComplianceBasePath"] ??
        Path.Combine(builder.Environment.ContentRootPath, "data", "compliance")));

// IComplianceConsentWriter: backed by ComplianceConsentWriter
builder.Services.AddSingleton<IComplianceConsentWriter, ComplianceConsentWriter>();

// IConsentAuditService: Azure Table in prod, no-op in dev
builder.Services.AddSingleton<IConsentAuditService>(sp =>
{
    var connStr = builder.Configuration["AzureStorage:ConnectionString"];
    if (string.IsNullOrEmpty(connStr))
        return new NullConsentAuditService();

    var tableClient = new Azure.Data.Tables.TableClient(connStr, "consentaudit");
    return new ConsentAuditService(tableClient, sp.GetRequiredService<ILogger<ConsentAuditService>>());
});

// Notification dead letter store (Azure Table Storage; no-op when connection string is absent)
builder.Services.AddSingleton<IFailedNotificationStore>(sp =>
{
    var connStr = builder.Configuration["AzureStorage:ConnectionString"];
    if (string.IsNullOrEmpty(connStr))
        return new NullFailedNotificationStore();

    var tableClient = new Azure.Data.Tables.TableClient(connStr, "failednotifications");
    return new FailedNotificationStore(tableClient, sp.GetRequiredService<ILogger<FailedNotificationStore>>());
});

// GDPR data export
builder.Services.AddSingleton<ILeadDataExport, LeadDataExport>();

// Email template rendering (privacy footer with unsubscribe/view-data links)
builder.Services.AddSingleton<IEmailTemplateRenderer, PrivacyFooterRenderer>();

// Consent token store (Azure Table; in-memory fallback when not configured)
builder.Services.AddSingleton<ConsentTokenStore>(sp =>
{
    var connStr = builder.Configuration["AzureStorage:ConnectionString"];
    if (string.IsNullOrEmpty(connStr))
        return new ConsentTokenStore(
            new Azure.Data.Tables.TableClient("UseDevelopmentStorage=true", "consenttokens"),
            sp.GetRequiredService<ILogger<ConsentTokenStore>>());

    var tableClient = new Azure.Data.Tables.TableClient(connStr, "consenttokens");
    return new ConsentTokenStore(tableClient, sp.GetRequiredService<ILogger<ConsentTokenStore>>());
});

// Background lead processing (replaces fire-and-forget Task.Run)
builder.Services.AddSingleton<LeadProcessingChannel>();
builder.Services.AddSingleton<CmaProcessingChannel>();
builder.Services.AddSingleton<HomeSearchProcessingChannel>();
builder.Services.AddHostedService<LeadProcessingWorker>();
builder.Services.AddHostedService<CmaProcessingWorker>();
builder.Services.AddHostedService<HomeSearchProcessingWorker>();

// Lead enrichment — typed HttpClient with Polly resilience
builder.Services.AddHttpClient(nameof(ScraperLeadEnricher))
    .AddClaudeApiResilience(pollyLogger);
builder.Services.AddSingleton<ILeadEnricher>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<ScraperLeadEnricher>>();
    return new ScraperLeadEnricher(factory, anthropicKey, scraperApiKey ?? "", logger);
});

// Home search — scraper-based (uses both nameof and "ScraperAPI" named clients)
builder.Services.AddHttpClient(nameof(ScraperHomeSearchProvider))
    .AddClaudeApiResilience(pollyLogger);
builder.Services.AddSingleton<IHomeSearchProvider>(sp =>
    new ScraperHomeSearchProvider(
        sp.GetRequiredService<IHttpClientFactory>(),
        scraperApiKey ?? "",
        anthropicKey,
        sp.GetRequiredService<ILogger<ScraperHomeSearchProvider>>()));

// CMA pipeline services
builder.Services.AddHttpClient(nameof(ScraperCompSource))
    .AddClaudeApiResilience(pollyLogger);
builder.Services.AddHttpClient(nameof(ClaudeCmaAnalyzer))
    .AddClaudeApiResilience(pollyLogger);
builder.Services.AddSingleton<ICompAggregator>(sp =>
    new CompAggregator(
        sp.GetServices<ICompSource>(),
        sp.GetRequiredService<ILogger<CompAggregator>>()));
builder.Services.AddSingleton<ICompSource>(sp =>
    new ScraperCompSource(
        sp.GetRequiredService<IHttpClientFactory>(),
        scraperApiKey ?? "", anthropicKey,
        CompSource.Zillow, "https://www.zillow.com/homedetails/{slug}",
        sp.GetRequiredService<ILogger<ScraperCompSource>>()));
builder.Services.AddSingleton<ICompSource>(sp =>
    new ScraperCompSource(
        sp.GetRequiredService<IHttpClientFactory>(),
        scraperApiKey ?? "", anthropicKey,
        CompSource.Redfin, "https://www.redfin.com/homes/{slug}",
        sp.GetRequiredService<ILogger<ScraperCompSource>>()));
builder.Services.AddSingleton<ICompSource>(sp =>
    new ScraperCompSource(
        sp.GetRequiredService<IHttpClientFactory>(),
        scraperApiKey ?? "", anthropicKey,
        CompSource.RealtorCom, "https://www.realtor.com/realestateandhomes-detail/{slug}",
        sp.GetRequiredService<ILogger<ScraperCompSource>>()));
builder.Services.AddSingleton<ICmaAnalyzer>(sp =>
    new ClaudeCmaAnalyzer(
        sp.GetRequiredService<IHttpClientFactory>(),
        anthropicKey,
        sp.GetRequiredService<ILogger<ClaudeCmaAnalyzer>>()));
builder.Services.AddSingleton<ICmaPdfGenerator, CmaPdfGenerator>();
builder.Services.AddSingleton<ICmaNotifier, CmaSellerNotifier>();
builder.Services.AddSingleton<IHomeSearchNotifier, HomeSearchBuyerNotifier>();

// Named HttpClients used by services with hardcoded client names
builder.Services.AddHttpClient("ScraperAPI")
    .AddScraperApiResilience(pollyLogger);
builder.Services.AddHttpClient("GoogleChat")
    .AddGoogleChatResilience(pollyLogger);

// Drive change monitor
builder.Services.AddSingleton<DriveChangeMonitor>();

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

// ------------------------------------------------------------------
// Lead notification channel stubs (email — noop until email channel built)
// ------------------------------------------------------------------
builder.Services.AddSingleton<IEmailNotifier, NoopEmailNotifier>();

// ------------------------------------------------------------------
// WhatsApp services — always register intent/response stubs so
// ConversationHandler can be resolved even without WhatsApp config.
// Azure queue/table services are conditional on PhoneNumberId being set.
// ------------------------------------------------------------------
builder.Services.AddSingleton<IIntentClassifier, NoopIntentClassifier>();
builder.Services.AddSingleton<IResponseGenerator, NoopResponseGenerator>();
builder.Services.AddSingleton<IConversationLogger, ConversationLogger>();
builder.Services.AddSingleton<IConversationHandler, ConversationHandler>();

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

    builder.Services.AddHostedService<WebhookProcessorWorker>();
    builder.Services.AddHostedService<WhatsAppRetryJob>();
}
else
{
    // Register null-object implementations so any endpoint that resolves
    // IWhatsAppNotifier / IWhatsAppAuditService / IWebhookQueueService
    // still compiles and fails gracefully with a clear log rather than a DI exception.
    builder.Services.AddSingleton<IWhatsAppNotifier, DisabledWhatsAppNotifier>();
    builder.Services.AddSingleton<IWhatsAppAuditService, DisabledWhatsAppAuditService>();
    builder.Services.AddSingleton<IWebhookQueueService, DisabledWebhookQueueService>();
    builder.Services.AddSingleton<WhatsAppIdempotencyStore>();
}

// ------------------------------------------------------------------
// Lead notification orchestrator
// ------------------------------------------------------------------
builder.Services.AddSingleton<CascadingAgentNotifier>();

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
    .AddCheck<ClaudeApiHealthCheck>("claude_api", tags: ["ready"])
    .AddCheck<GoogleDriveHealthCheck>("google_drive", tags: ["ready"])
    .AddCheck<ScraperApiHealthCheck>("scraper_api", tags: ["ready"])
    .AddCheck<TurnstileHealthCheck>("turnstile", tags: ["ready"])
    .AddCheck<BackgroundServiceHealthCheck>("background_workers", tags: ["ready", "workers"]);

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000", "http://localhost:3001"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .SetIsOriginAllowed(origin =>
            {
                if (allowedOrigins.Contains(origin)) return true;

                // Allow *.localhost:{port} subdomains in development (e.g. jenise-buckalew.localhost:3000)
                if (builder.Environment.IsDevelopment())
                {
                    var uri = new Uri(origin);
                    return uri.Host == "localhost" || uri.Host.EndsWith(".localhost");
                }

                // Allow Cloudflare Pages preview deploys (*.pages.dev)
                if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri) &&
                    originUri.Host.EndsWith(".real-estate-star-agents.pages.dev", StringComparison.OrdinalIgnoreCase))
                {
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
