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

// Job store
builder.Services.AddSingleton<ICmaJobStore, InMemoryCmaJobStore>();

// SignalR
builder.Services.AddSignalR();

var app = builder.Build();

app.UseHttpsRedirection();

// --- CMA Endpoints ---

app.MapPost("/agents/{agentId}/cma", (string agentId, Lead lead, ICmaJobStore store) =>
{
    var job = CmaJob.Create(Guid.Empty, lead);
    store.Set(agentId, job);

    // TODO: Fire CmaPipeline in background once Task 12 is complete
    // Task.Run(() => pipeline.RunAsync(agentId, job));

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
        message = GetStatusMessage(job.Status)
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
    _ => "Processing..."
};

// Make Program accessible for integration tests
public partial class Program;
