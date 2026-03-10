namespace RealEstateStar.Api.Features.Cma.Services;

public interface ICmaPipeline
{
    Task ExecuteAsync(CmaJob job, string agentId, Lead lead, Func<CmaJobStatus, Task> onStatusChange, CancellationToken ct);
}
