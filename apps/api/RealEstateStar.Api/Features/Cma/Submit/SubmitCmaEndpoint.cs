using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.SignalR;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Api.Hubs;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.GetStatus;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Features.Cma.Services;

namespace RealEstateStar.Api.Features.Cma.Submit;

public class SubmitCmaEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/agents/{agentId}/cma", Handle)
            .RequireRateLimiting("cma-create");

    internal static IResult Handle(
        string agentId,
        SubmitCmaRequest request,
        ICmaJobStore store,
        ICmaPipeline pipeline,
        IHubContext<CmaProgressHub> hubContext,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var lead = request.ToLead();

        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(lead, new ValidationContext(lead), validationResults, true))
            return Results.ValidationProblem(GroupValidationErrors(validationResults));

        var job = CmaJob.Create(agentId, lead);
        store.Set(agentId, job);
        CmaDiagnostics.CmaCreated.Add(1, new KeyValuePair<string, object?>("agent.id", agentId));

        _ = Task.Run(async () =>
        {
            try
            {
                await pipeline.ExecuteAsync(job, agentId, lead, async status =>
                {
                    job.AdvanceTo(status);
                    store.Set(agentId, job);
                    await hubContext.Clients.Group(job.Id.ToString())
                        .SendAsync("StatusUpdate", new GetStatusResponse
                        {
                            Status = status,
                            Step = (int)status + 1,
                            TotalSteps = 9,
                            Message = StatusMessages.Get(status)
                        }, CancellationToken.None);
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CMA pipeline failed at step {Step} for agent {AgentId}, job {JobId}",
                    job.Status.ToString(), agentId, job.Id);

                var userMessage = ex is ArgumentException or InvalidOperationException
                    ? ex.Message
                    : "Pipeline execution failed. Please try again or contact support.";
                job.Fail(userMessage);
                store.Set(agentId, job);
            }
        });

        return Results.Accepted(value: new SubmitCmaResponse
        {
            JobId = job.Id.ToString(),
            Status = "processing"
        });
    }

    internal static Dictionary<string, string[]> GroupValidationErrors(List<ValidationResult> results) =>
        results.GroupBy(v => v.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage!).ToArray());
}
