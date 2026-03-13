using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Api.Hubs;
using RealEstateStar.Api.Logging;
using RealEstateStar.Api.Middleware;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Features.Cma.Services;
using RealEstateStar.Api.Features.Cma.Services.Analysis;
using RealEstateStar.Api.Features.Cma.Services.Comps;
using RealEstateStar.Api.Features.Cma.Services.Gws;
using RealEstateStar.Api.Features.Cma.Services.Pdf;
using RealEstateStar.Api.Features.Cma.Services.Research;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using RealEstateStar.Api.Health;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddStructuredLogging();
builder.AddObservability();

// Agent config
var configPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "config", "agents");
builder.Services.AddSingleton<IAgentConfigService>(sp =>
    new AgentConfigService(configPath, sp.GetRequiredService<ILogger<AgentConfigService>>()));

// Onboarding (session store registered early, services after config keys below)
builder.Services.AddSingleton<ISessionStore, JsonFileSessionStore>();
builder.Services.AddSingleton<OnboardingStateMachine>();

// Configuration keys
var anthropicKey = builder.Configuration["Anthropic:ApiKey"]
    ?? throw new InvalidOperationException("Anthropic:ApiKey configuration is required");
var attomKey = builder.Configuration["Attom:ApiKey"];
if (string.IsNullOrEmpty(attomKey))
    Log.Warning("Attom:ApiKey not configured — ATTOM comp source will be disabled");
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

builder.Services.AddHttpClient(nameof(ProfileScraperService));
builder.Services.AddSingleton<IDnsResolver, SystemDnsResolver>();
builder.Services.AddSingleton<IProfileScraper>(sp =>
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
builder.Services.AddSingleton<IDriveFolderInitializer, DriveFolderInitializer>();
builder.Services.AddSingleton<IOnboardingTool, ScrapeUrlTool>();
builder.Services.AddSingleton<IOnboardingTool, UpdateProfileTool>();
builder.Services.AddSingleton<IOnboardingTool, SetBrandingTool>();
builder.Services.AddSingleton<IOnboardingTool, DeploySiteTool>();
builder.Services.AddSingleton<IOnboardingTool, SubmitCmaFormTool>();
builder.Services.AddSingleton<IOnboardingTool, CreateStripeSessionTool>();
builder.Services.AddSingleton<ToolDispatcher>();
builder.Services.AddSingleton<IStripeService, StripeService>();
builder.Services.AddSingleton<DomainService>();
builder.Services.AddHttpClient(nameof(OnboardingChatService));
builder.Services.AddSingleton(sp =>
    new OnboardingChatService(
        sp.GetRequiredService<IHttpClientFactory>(),
        anthropicKey,
        sp.GetRequiredService<OnboardingStateMachine>(),
        sp.GetRequiredService<ToolDispatcher>(),
        sp.GetRequiredService<ILogger<OnboardingChatService>>()));
builder.Services.AddHostedService<TrialExpiryService>();

// Comp sources — typed HttpClient registrations
builder.Services.AddHttpClient<ZillowCompSource>();
builder.Services.AddSingleton<ICompSource>(sp => sp.GetRequiredService<ZillowCompSource>());

builder.Services.AddHttpClient<RealtorComCompSource>();
builder.Services.AddSingleton<ICompSource>(sp => sp.GetRequiredService<RealtorComCompSource>());

builder.Services.AddHttpClient<RedfinCompSource>();
builder.Services.AddSingleton<ICompSource>(sp => sp.GetRequiredService<RedfinCompSource>());

if (!string.IsNullOrEmpty(attomKey))
{
    builder.Services.AddHttpClient(nameof(AttomDataCompSource));
    builder.Services.AddSingleton<ICompSource>(sp =>
        new AttomDataCompSource(
            sp.GetRequiredService<IHttpClientFactory>(),
            attomKey,
            sp.GetService<ILogger<AttomDataCompSource>>()));
}

// Core services
builder.Services.AddSingleton<CompAggregator>();

builder.Services.AddHttpClient<LeadResearchService>();
builder.Services.AddSingleton<ILeadResearchService>(sp => sp.GetRequiredService<LeadResearchService>());

builder.Services.AddSingleton<ICmaPdfGenerator, CmaPdfGenerator>();
builder.Services.AddSingleton<IGwsService, GwsService>();

builder.Services.AddHttpClient(nameof(ClaudeAnalysisService));
builder.Services.AddSingleton<IAnalysisService>(sp =>
    new ClaudeAnalysisService(
        sp.GetRequiredService<IHttpClientFactory>(),
        anthropicKey,
        sp.GetService<ILogger<ClaudeAnalysisService>>()));

// Pipeline orchestrator
builder.Services.AddSingleton<ICmaPipeline, CmaPipeline>();

// Problem details for validation errors
builder.Services.AddProblemDetails();

// Job store
builder.Services.AddMemoryCache(options => options.SizeLimit = 10_000);
builder.Services.AddSingleton<ICmaJobStore, InMemoryCmaJobStore>();

// SignalR
builder.Services.AddSignalR();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<ClaudeApiHealthCheck>("claude_api", tags: ["ready"]);

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? ["http://localhost:3000", "http://localhost:3001"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
});

// ForwardedHeaders — must be configured before rate limiter so RemoteIpAddress is correct behind proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
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

    // Stricter policy for CMA creation: 10 per hour per agent
    options.AddPolicy("cma-create", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Request.RouteValues["agentId"]?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromHours(1)
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

// SignalR hub
app.MapHub<CmaProgressHub>("/hubs/cma-progress");

// --- CMA Endpoints ---
app.MapEndpoints();

app.Run();

static async Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var result = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds
        })
    };
    await context.Response.WriteAsJsonAsync(result);
}

// Startup config — guard clauses tested via deploy health checks, not unit tests
[ExcludeFromCodeCoverage]
public partial class Program;
