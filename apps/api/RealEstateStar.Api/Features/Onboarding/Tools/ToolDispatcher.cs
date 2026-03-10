using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class ToolDispatcher(IEnumerable<IOnboardingTool> tools)
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

        return await tool.ExecuteAsync(parameters, session, ct);
    }

    public bool HasTool(string toolName) => _tools.ContainsKey(toolName);
}
