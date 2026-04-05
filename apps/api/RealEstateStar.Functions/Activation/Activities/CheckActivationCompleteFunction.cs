using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 0 activity: checks whether the activation pipeline has already run
/// by verifying all required files exist in document storage.
///
/// Mirrors the check in <c>ActivationOrchestrator.IsAlreadyCompleteAsync</c>.
/// Required files must all be present for the activation to be considered complete.
/// </summary>
public sealed class CheckActivationCompleteFunction(
    IDocumentStorageProvider storage,
    ILogger<CheckActivationCompleteFunction> logger)
{
    // These must match ActivationOrchestrator.RequiredAgentFiles + RequiredAccountFiles
    internal static readonly IReadOnlyList<string> RequiredAgentFiles =
    [
        "Voice Skill.md",
        "Personality Skill.md",
        "Marketing Style.md",
        "Sales Pipeline.md",
        "Coaching Report.md",
        "Agent Discovery.md",
        "Branding Kit.md",
        "Email Signature.md",
        "headshot.jpg",
        "Drive Index.md",
    ];

    internal static readonly IReadOnlyList<string> RequiredAccountFiles =
    [
        "Brand Profile.md",
        "Brand Voice.md",
    ];

    // Spanish skill files required when agent supports "es" locale
    internal static readonly IReadOnlyList<string> SpanishAgentFiles =
    [
        "Voice Skill.es.md",
        "Personality Skill.es.md",
    ];

    [Function(ActivityNames.CheckActivationComplete)]
    public async Task<string> RunAsync(
        [ActivityTrigger] CheckActivationCompleteInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-010] Checking activation complete for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        var agentFolder = $"real-estate-star/{input.AgentId}";
        var accountFolder = $"real-estate-star/{input.AccountId}";

        var checkTasks = new List<Task<bool>>();

        foreach (var file in RequiredAgentFiles)
            checkTasks.Add(FileExistsAsync(agentFolder, file, ct));

        foreach (var file in RequiredAccountFiles)
            checkTasks.Add(FileExistsAsync(accountFolder, file, ct));

        // If the agent supports Spanish, also require localized skill files
        if (input.Languages is not null && input.Languages.Contains("es", StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in SpanishAgentFiles)
                checkTasks.Add(FileExistsAsync(agentFolder, file, ct));
        }

        var results = await Task.WhenAll(checkTasks);
        var isComplete = results.All(exists => exists);

        logger.LogInformation(
            "[ACTV-FN-011] Activation complete check: {IsComplete} for agentId={AgentId}",
            isComplete, input.AgentId);

        return JsonSerializer.Serialize(new CheckActivationCompleteOutput { IsComplete = isComplete });
    }

    private async Task<bool> FileExistsAsync(string folder, string file, CancellationToken ct)
    {
        var content = await storage.ReadDocumentAsync(folder, file, ct);
        return content is not null;
    }
}
