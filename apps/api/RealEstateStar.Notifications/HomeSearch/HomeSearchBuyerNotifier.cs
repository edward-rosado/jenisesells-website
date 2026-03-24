using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Notifications.HomeSearch;

public class HomeSearchBuyerNotifier(
    IGmailSender gmailSender,
    IFileStorageProvider fanOutStorage,
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
        var accountId = NotificationHelpers.ResolveAccountId(agent, agentId);
        var agentName = agent?.Agent?.Name ?? agentId;

        var subject = $"Your Personalized Home Search Results \u2013 {lead.BuyerDetails?.City}, {lead.BuyerDetails?.State}";
        var body = HomeSearchMarkdownRenderer.RenderEmailBody(lead, listings, agentName);

        // Step 1: Send email — failure is fatal; caller decides whether to retry
        var emailSw = Stopwatch.GetTimestamp();
        logger.LogInformation(
            "[HS-NOTIFY-001] Sending home search email to {RecipientHash} for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
            NotificationHelpers.HashEmail(lead.Email), lead.Id, agentId, correlationId);

        try
        {
            await gmailSender.SendAsync(accountId, agentId, lead.Email, subject, body, ct);
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

        // Step 2: Store listings via fan-out storage — failure is non-fatal
        var driveSw = Stopwatch.GetTimestamp();
        var folder = $"{LeadPaths.LeadFolder(lead.FullName)}/Home Search";
        var fileName = $"{DateTime.UtcNow:yyyy-MM-dd}-Home Search Results.md";
        var content = HomeSearchMarkdownRenderer.RenderListings(lead, listings, agentName);

        logger.LogInformation(
            "[HS-NOTIFY-003] Storing in fan-out storage for lead {LeadId}. Folder: {Folder}. CorrelationId: {CorrelationId}",
            lead.Id, folder, correlationId);

        try
        {
            await fanOutStorage.WriteDocumentAsync(folder, fileName, content, ct);
            HomeSearchDiagnostics.DriveDuration.Record(Stopwatch.GetElapsedTime(driveSw).TotalMilliseconds);

            logger.LogInformation(
                "[HS-NOTIFY-004] Stored for lead {LeadId}. Duration: {DurationMs}ms. CorrelationId: {CorrelationId}",
                lead.Id, Stopwatch.GetElapsedTime(driveSw).TotalMilliseconds, correlationId);
        }
        catch (Exception ex)
        {
            HomeSearchDiagnostics.DriveDuration.Record(Stopwatch.GetElapsedTime(driveSw).TotalMilliseconds);
            logger.LogError(ex,
                "[HS-NOTIFY-006] Fan-out storage failed for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
                lead.Id, agentId, correlationId);
        }
    }

}
