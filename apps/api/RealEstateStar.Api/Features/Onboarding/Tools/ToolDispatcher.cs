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
        {
            logger.LogError("[TOOL-010] Unknown tool requested: {ToolName} for session {SessionId}. " +
                "Available tools: {Available}",
                toolName, session.Id, string.Join(", ", _tools.Keys));
            throw new InvalidOperationException($"[TOOL-010] Unknown tool: {toolName}");
        }

        logger.LogInformation("[TOOL-011] Dispatching tool {ToolName} for session {SessionId} in state {State}",
            toolName, session.Id, session.CurrentState);

        try
        {
            var result = await tool.ExecuteAsync(parameters, session, ct);
            logger.LogInformation("[TOOL-012] Tool {ToolName} completed for session {SessionId}, result length={Len}",
                toolName, session.Id, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TOOL-013] Tool {ToolName} failed for session {SessionId}. " +
                "ExType={ExType}, Message={ExMessage}",
                toolName, session.Id, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public bool HasTool(string toolName) => _tools.ContainsKey(toolName);
}
