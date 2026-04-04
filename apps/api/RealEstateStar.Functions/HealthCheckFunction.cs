using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace RealEstateStar.Functions;

/// <summary>
/// Minimal health check function — no DI, no external dependencies.
/// Used to verify Azure Functions discovers at least one function.
/// </summary>
public class HealthCheckFunction
{
    private readonly ILogger<HealthCheckFunction> _logger;

    public HealthCheckFunction(ILogger<HealthCheckFunction> logger)
    {
        _logger = logger;
    }

    [Function("HealthCheck")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        _logger.LogInformation("[FUNC-HEALTH] Health check invoked");
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString("healthy");
        return response;
    }
}
