using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using RealEstateStar.Api.Hubs;
using RealEstateStar.Api.Logging;
using RealEstateStar.Api.Middleware;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddStructuredLogging();

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
builder.Services.AddHealthChecks();

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
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();

// Health check
app.MapHealthChecks("/health");

// SignalR hub
app.MapHub<CmaProgressHub>("/hubs/cma-progress");

// --- CMA Endpoints ---

app.MapPost("/agents/{agentId}/cma", (
    string agentId,
    Lead lead,
    ICmaJobStore store,
    CmaPipeline pipeline,
    IHubContext<CmaProgressHub> hubContext,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var validationResults = new List<ValidationResult>();
    if (!Validator.TryValidateObject(lead, new ValidationContext(lead), validationResults, true))
        return Results.ValidationProblem(
            validationResults.GroupBy(v => v.MemberNames.FirstOrDefault() ?? "")
                .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage!).ToArray()));

    var job = CmaJob.Create(Guid.Empty, lead);
    store.Set(agentId, job);

    _ = Task.Run(async () =>
    {
        try
        {
            await pipeline.ExecuteAsync(job, agentId, lead, async status =>
            {
                store.Set(agentId, job);

                await hubContext.Clients.Group(job.Id.ToString())
                    .SendAsync("StatusUpdate", new
                    {
                        status = status.ToString().ToLowerInvariant(),
                        step = (int)status + 1,
                        totalSteps = 9,
                        message = GetStatusMessage(status)
                    });
            }, CancellationToken.None);

            store.Set(agentId, job);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CMA pipeline failed for agent {AgentId}, job {JobId}", agentId, job.Id);
            job.Fail(ex.Message);
            store.Set(agentId, job);
        }
    });

    return Results.Accepted(value: new { jobId = job.Id.ToString(), status = "processing" });
}).RequireRateLimiting("cma-create");

app.MapGet("/agents/{agentId}/cma/{jobId}/status", (string agentId, string jobId, ICmaJobStore store) =>
{
    var job = store.Get(jobId);

    if (job is null)
        return Results.NotFound();

    return Results.Ok(new
    {
        status = job.Status.ToString().ToLowerInvariant(),
        step = job.Step,
        totalSteps = job.TotalSteps,
        message = GetStatusMessage(job.Status),
        errorMessage = job.Status == CmaJobStatus.Failed ? job.ErrorMessage : null
    });
});

app.MapGet("/agents/{agentId}/leads", (string agentId, ICmaJobStore store) =>
{
    var jobs = store.GetByAgent(agentId);

    return Results.Ok(jobs.Select(j => new
    {
        id = j.Id.ToString(),
        name = j.Lead.FullName,
        address = j.Lead.FullAddress,
        timeline = j.Lead.Timeline,
        cmaStatus = j.Status.ToString().ToLowerInvariant(),
        submittedAt = j.CreatedAt,
        driveLink = j.DriveLink
    }));
});

app.Run();

static string GetStatusMessage(CmaJobStatus status) => status switch
{
    CmaJobStatus.Parsing => "Received your property details",
    CmaJobStatus.SearchingComps => "Searching MLS databases...",
    CmaJobStatus.ResearchingLead => "Researching property records...",
    CmaJobStatus.Analyzing => "Analyzing market trends...",
    CmaJobStatus.GeneratingPdf => "Generating your personalized report...",
    CmaJobStatus.OrganizingDrive => "Organizing documents...",
    CmaJobStatus.SendingEmail => "Sending report to your email...",
    CmaJobStatus.Logging => "Finalizing...",
    CmaJobStatus.Complete => "Your report has been sent to your email!",
    CmaJobStatus.Failed => "An error occurred while processing your report.",
    _ => "Processing..."
};

// Make Program accessible for integration tests
public partial class Program;
