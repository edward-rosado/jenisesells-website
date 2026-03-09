using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.SignalR;
using RealEstateStar.Api.Hubs;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<ICmaJobStore, InMemoryCmaJobStore>();

// SignalR
builder.Services.AddSignalR();

var app = builder.Build();

app.UseHttpsRedirection();

// SignalR hub
app.MapHub<CmaProgressHub>("/hubs/cma-progress");

// --- CMA Endpoints ---

app.MapPost("/agents/{agentId}/cma", (
    string agentId,
    Lead lead,
    ICmaJobStore store,
    CmaPipeline pipeline,
    IHubContext<CmaProgressHub> hubContext,
    ILogger<Program> logger) =>
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
            await pipeline.ExecuteAsync(job, agentId, lead, status =>
            {
                store.Set(agentId, job);

                hubContext.Clients.Group(job.Id.ToString())
                    .SendAsync("StatusUpdate", new
                    {
                        status = status.ToString().ToLowerInvariant(),
                        step = (int)status + 1,
                        totalSteps = 9,
                        message = GetStatusMessage(status)
                    });
            });

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
});

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
