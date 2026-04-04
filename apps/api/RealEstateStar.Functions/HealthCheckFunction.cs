using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace RealEstateStar.Functions;

/// <summary>
/// Minimal health check function — zero DI, zero external dependencies.
/// Used to verify Azure Functions discovers and executes at least one function.
/// </summary>
public class HealthCheckFunction
{
    [Function("HealthCheck")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString("healthy");
        return response;
    }
}
