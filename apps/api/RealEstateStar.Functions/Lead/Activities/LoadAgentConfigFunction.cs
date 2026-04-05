using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Loads agent account config and maps it to <see cref="AgentNotificationConfig"/>.
/// </summary>
public sealed class LoadAgentConfigFunction(
    IAccountConfigService accountConfigService,
    ILogger<LoadAgentConfigFunction> logger)
{
    [Function("LoadAgentConfig")]
    public async Task<string> RunAsync(
        [ActivityTrigger] LoadAgentConfigInput input,
        CancellationToken ct)
    {
        var accountConfig = await accountConfigService.GetAccountAsync(input.AgentId, ct);
        if (accountConfig is null)
        {
            logger.LogWarning("[LAC-001] Agent config not found for {AgentId}. CorrelationId={CorrelationId}",
                input.AgentId, input.CorrelationId);
            return JsonSerializer.Serialize(new LoadAgentConfigOutput { Found = false });
        }

        var agentConfig = new AgentNotificationConfig
        {
            AgentId = input.AgentId,
            Handle = accountConfig.Handle,
            Name = accountConfig.Agent?.Name ?? input.AgentId,
            FirstName = accountConfig.Agent?.Name?.Split(' ').FirstOrDefault() ?? input.AgentId,
            Email = accountConfig.Agent?.Email ?? string.Empty,
            Phone = accountConfig.Agent?.Phone ?? string.Empty,
            LicenseNumber = accountConfig.Agent?.LicenseNumber ?? string.Empty,
            BrokerageName = accountConfig.Brokerage?.Name ?? string.Empty,
            PrimaryColor = accountConfig.Branding?.PrimaryColor ?? "#000000",
            AccentColor = accountConfig.Branding?.AccentColor ?? "#000000",
            State = accountConfig.Location?.State ?? string.Empty,
            ServiceAreas = accountConfig.Location?.ServiceAreas ?? [],
            WhatsAppPhoneNumberId = accountConfig.Integrations?.WhatsApp?.PhoneNumber
        };

        logger.LogInformation("[LAC-010] Agent config loaded for {AgentId}. CorrelationId={CorrelationId}",
            input.AgentId, input.CorrelationId);

        return JsonSerializer.Serialize(new LoadAgentConfigOutput
        {
            Found = true,
            AgentNotificationConfig = agentConfig
        });
    }
}
