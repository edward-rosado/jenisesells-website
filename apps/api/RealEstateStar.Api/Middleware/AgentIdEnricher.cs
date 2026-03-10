using Serilog;

namespace RealEstateStar.Api.Middleware;

public static class AgentIdEnricher
{
    public static void EnrichFromRequest(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        var agentId = httpContext.Request.RouteValues["agentId"] as string;
        if (agentId is not null)
            diagnosticContext.Set("AgentId", agentId);
    }
}
