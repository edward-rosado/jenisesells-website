namespace RealEstateStar.Workers.Shared;

/// <summary>
/// A single step in a worker pipeline.
/// Each step takes a request and produces a response.
/// </summary>
public interface IWorkerStep<in TRequest, TResponse>
{
    string StepName { get; }
    Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct);
}
