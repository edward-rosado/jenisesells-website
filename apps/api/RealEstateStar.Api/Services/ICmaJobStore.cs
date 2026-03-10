using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Services;

public interface ICmaJobStore
{
    CmaJob? Get(string jobId);
    void Set(string agentId, CmaJob job);
    IEnumerable<CmaJob> GetByAgent(string agentId);
}
