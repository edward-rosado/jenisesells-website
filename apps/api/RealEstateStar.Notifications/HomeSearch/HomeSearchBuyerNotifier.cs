using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Notifications.HomeSearch;

public class HomeSearchBuyerNotifier(
    IGwsService gwsService,
    IAccountConfigService accountConfigService,
    ILogger<HomeSearchBuyerNotifier> logger) : IHomeSearchNotifier
{
    public async Task NotifyBuyerAsync(
        string agentId,
        Lead lead,
        List<Listing> listings,
        string correlationId,
        CancellationToken ct)
    {
        var agent = await accountConfigService.GetAccountAsync(agentId, ct);
        var agentEmail = agent?.Agent?.Email ?? "";
        var agentName = agent?.Agent?.Name ?? agentId;

        var subject = $"Your Personalized Home Search Results \u2013 {lead.BuyerDetails?.City}, {lead.BuyerDetails?.State}";
        var body = HomeSearchMarkdownRenderer.RenderEmailBody(lead, listings, agentName);

        // Step 1: Send email — failure is fatal; caller decides whether to retry
        var emailSw = Stopwatch.GetTimestamp();
        logger.LogInformation(
            "[HS-NOTIFY-001] Sending home search email to {RecipientHash} for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
            HashEmail(lead.Email), lead.Id, agentId, correlationId);

        try
        {
            await gwsService.SendEmailAsync(agentEmail, lead.Email, subject, body, null, ct);
            HomeSearchDiagnostics.EmailDuration.Record(Stopwatch.GetElapsedTime(emailSw).TotalMilliseconds);

            logger.LogInformation(
                "[HS-NOTIFY-002] Email sent for lead {LeadId}. Duration: {DurationMs}ms. CorrelationId: {CorrelationId}",
                lead.Id, Stopwatch.GetElapsedTime(emailSw).TotalMilliseconds, correlationId);
        }
        catch (Exception ex)
        {
            HomeSearchDiagnostics.EmailDuration.Record(Stopwatch.GetElapsedTime(emailSw).TotalMilliseconds);
            logger.LogError(ex,
                "[HS-NOTIFY-005] Email failed for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
                lead.Id, agentId, correlationId);
            throw;
        }

        // Step 2: Store listings in Drive — failure is non-fatal
        var driveSw = Stopwatch.GetTimestamp();
        var folder = $"{LeadPaths.LeadFolder(lead.FullName)}/Home Search";
        var fileName = $"{DateTime.UtcNow:yyyy-MM-dd}-Home Search Results";
        var content = HomeSearchMarkdownRenderer.RenderListings(lead, listings, agentName);

        logger.LogInformation(
            "[HS-NOTIFY-003] Storing in Drive for lead {LeadId}. Folder: {Folder}. CorrelationId: {CorrelationId}",
            lead.Id, folder, correlationId);

        try
        {
            await gwsService.CreateDocAsync(agentEmail, folder, fileName, content, ct);
            HomeSearchDiagnostics.DriveDuration.Record(Stopwatch.GetElapsedTime(driveSw).TotalMilliseconds);

            logger.LogInformation(
                "[HS-NOTIFY-004] Stored for lead {LeadId}. Duration: {DurationMs}ms. CorrelationId: {CorrelationId}",
                lead.Id, Stopwatch.GetElapsedTime(driveSw).TotalMilliseconds, correlationId);
        }
        catch (Exception ex)
        {
            HomeSearchDiagnostics.DriveDuration.Record(Stopwatch.GetElapsedTime(driveSw).TotalMilliseconds);
            logger.LogError(ex,
                "[HS-NOTIFY-006] Drive storage failed for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
                lead.Id, agentId, correlationId);
        }
    }

    private static string HashEmail(string email)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
