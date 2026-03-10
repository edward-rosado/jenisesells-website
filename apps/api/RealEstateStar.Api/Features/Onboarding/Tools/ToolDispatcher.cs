using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class ToolDispatcher(IEnumerable<IOnboardingTool> tools, ILogger<ToolDispatcher> logger)
{
    private readonly Dictionary<string, IOnboardingTool> _tools =
        tools.ToDictionary(t => t.Name);

    public async Task<string> DispatchAsync(
        string toolName,
        JsonElement parameters,
        OnboardingSession session,
        CancellationToken ct)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            throw new InvalidOperationException($"Unknown tool: {toolName}");

        try
        {
            return await tool.ExecuteAsync(parameters, session, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool {ToolName} failed for session {SessionId}", toolName, session.Id);
            throw;
        }
    }

    public bool HasTool(string toolName) => _tools.ContainsKey(toolName);
}
