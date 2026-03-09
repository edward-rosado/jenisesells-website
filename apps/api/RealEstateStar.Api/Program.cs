using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Endpoints;
using RealEstateStar.Api.Hubs;
using RealEstateStar.Api.Logging;
using RealEstateStar.Api.Middleware;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RealEstateStar.Api.Health;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddStructuredLogging();
builder.AddObservability();

// Agent config
var configPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "config", "agents");
builder.Services.AddSingleton<IAgentConfigService>(sp =>
    new AgentConfigService(configPath, sp.GetRequiredService<ILogger<AgentConfigService>>()));

// HTTP clients
builder.Services.AddHttpClient();

// Comp sources
builder.Services.AddSingleton<ICompSource>(sp =>
    new ZillowCompSource(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetService<ILogger<ZillowCompSource>>()));
builder.Services.AddSingleton<ICompSource>(sp =>
    new RealtorComCompSource(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetService<ILogger<RealtorComCompSource>>()));
builder.Services.AddSingleton<ICompSource>(sp =>
    new RedfinCompSource(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetService<ILogger<RedfinCompSource>>()));
builder.Services.AddSingleton<ICompSource>(sp =>
    new AttomDataCompSource(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
        builder.Configuration["Attom:ApiKey"] ?? "",
        sp.GetService<ILogger<AttomDataCompSource>>()));

// Core services
builder.Services.AddSingleton<CompAggregator>();
builder.Services.AddSingleton<ILeadResearchService>(sp =>
    new LeadResearchService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetService<ILogger<LeadResearchService>>()));
builder.Services.AddSingleton<ICmaPdfGenerator, CmaPdfGenerator>();
builder.Services.AddSingleton<IGwsService>(sp =>
    new GwsService(sp.GetService<ILogger<GwsService>>()));
builder.Services.AddSingleton<IAnalysisService>(sp =>
    new ClaudeAnalysisService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
        builder.Configuration["Anthropic:ApiKey"] ?? "",
        sp.GetService<ILogger<ClaudeAnalysisService>>()));

// Pipeline orchestrator
builder.Services.AddSingleton<CmaPipeline>();

// Problem details for validation errors
builder.Services.AddProblemDetails();

// Job store
builder.Services.AddMemoryCache(options => options.SizeLimit = 10_000);
builder.Services.AddSingleton<ICmaJobStore, InMemoryCmaJobStore>();

// SignalR
builder.Services.AddSignalR();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<GwsCliHealthCheck>("gws_cli", tags: ["ready"])
    .AddCheck<ClaudeApiHealthCheck>("claude_api", tags: ["ready"]);

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
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
});

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

app.UseMiddleware<CorrelationIdMiddleware>();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Api-Version"] = "1.0";
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

// Make Program accessible for integration tests
public partial class Program;
