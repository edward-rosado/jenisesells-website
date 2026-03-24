using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Notifications.Cma;

public class CmaSellerNotifier(
    IGmailSender gmailSender,
    IFileStorageProvider fanOutStorage,
    IAccountConfigService accountConfigService,
    ILogger<CmaSellerNotifier> logger) : ICmaNotifier
{
    public async Task NotifySellerAsync(
        string agentId,
        Lead lead,
        string pdfPath,
        CmaAnalysis analysis,
        string correlationId,
        CancellationToken ct)
    {
        var agent = await accountConfigService.GetAccountAsync(agentId, ct);
        var accountId = NotificationHelpers.ResolveAccountId(agent, agentId);
        var agentName = agent?.Agent?.Name ?? agentId;
        var agentPhone = agent?.Agent?.Phone ?? "";

        var sd = lead.SellerDetails!;
        var fullAddress = $"{sd.Address}, {sd.City}, {sd.State} {sd.Zip}";
        var cmaFolder = LeadPaths.CmaFolder(lead.FullName, fullAddress);

        var subject = $"Your Comparative Market Analysis \u2013 {fullAddress}";
        var body = BuildEmailBody(lead, analysis, agentName, agentPhone);

        // Step 1: Send email with PDF attachment — failure is fatal; caller decides whether to retry
        var emailSw = Stopwatch.GetTimestamp();
        logger.LogInformation(
            "[CMA-NOTIFY-001] Sending CMA email to {RecipientHash} for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
            NotificationHelpers.HashEmail(lead.Email), lead.Id, agentId, correlationId);

        try
        {
            var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);
            var pdfFileName = Path.GetFileName(pdfPath);
            await gmailSender.SendWithAttachmentAsync(accountId, agentId, lead.Email, subject, body, pdfBytes, pdfFileName, ct);
            CmaDiagnostics.EmailDuration.Record(Stopwatch.GetElapsedTime(emailSw).TotalMilliseconds);

            logger.LogInformation(
                "[CMA-NOTIFY-002] Email sent successfully for lead {LeadId}. Duration: {DurationMs}ms. CorrelationId: {CorrelationId}",
                lead.Id, Stopwatch.GetElapsedTime(emailSw).TotalMilliseconds, correlationId);
        }
        catch (Exception ex)
        {
            CmaDiagnostics.EmailDuration.Record(Stopwatch.GetElapsedTime(emailSw).TotalMilliseconds);
            logger.LogError(ex,
                "[CMA-NOTIFY-007] Email send failed for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
                lead.Id, agentId, correlationId);
            throw;
        }

        // Step 2: Store communication record via fan-out — failure is non-fatal
        logger.LogInformation(
            "[CMA-NOTIFY-005] Storing communication record for lead {LeadId}. CorrelationId: {CorrelationId}",
            lead.Id, correlationId);

        try
        {
            var emailRecord = BuildEmailRecord(lead, subject, body, correlationId);
            var fileName = $"{DateTime.UtcNow:yyyy-MM-dd-HHmmss}-CMA Email Record.md";
            await fanOutStorage.WriteDocumentAsync(cmaFolder, fileName, emailRecord, ct);
            CmaDiagnostics.DriveDuration.Record(0); // record a nominal zero since drive is now fan-out

            logger.LogInformation(
                "[CMA-NOTIFY-006] Communication stored for lead {LeadId}. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[CMA-NOTIFY-009] Communication record failed for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
                lead.Id, agentId, correlationId);
        }
    }

    internal static string BuildEmailBody(Lead lead, CmaAnalysis analysis, string agentName, string agentPhone)
    {
        // HtmlEncode all user/agent/AI-supplied fields — this body is sent as htmlBody to Gmail.
        static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var sd = lead.SellerDetails!;
        var fullAddress = $"{H(sd.Address)}, {H(sd.City)}, {H(sd.State)} {H(sd.Zip)}";
        var sb = new StringBuilder();

        sb.AppendLine($"Hi {H(lead.FirstName)},");
        sb.AppendLine();
        sb.AppendLine($"Thank you for reaching out! {H(agentName)} has prepared a Comparative Market Analysis for your property at {fullAddress}.");
        sb.AppendLine();
        sb.AppendLine("Based on recent comparable sales in your area, here is the estimated value range:");
        sb.AppendLine();
        sb.AppendLine($"  Low:  {FormatUsd(analysis.ValueLow)}");
        sb.AppendLine($"  Mid:  {FormatUsd(analysis.ValueMid)}");
        sb.AppendLine($"  High: {FormatUsd(analysis.ValueHigh)}");
        sb.AppendLine();
        sb.AppendLine($"Market trend: {H(analysis.MarketTrend)}");
        sb.AppendLine($"Median days on market: {analysis.MedianDaysOnMarket} days");
        sb.AppendLine();
        sb.AppendLine("Your full CMA report is attached as a PDF.");
        sb.AppendLine();
        sb.AppendLine($"Ready to discuss your options? Reply to this email or call {H(agentPhone)} — we would love to walk you through the findings.");
        sb.AppendLine();
        sb.AppendLine($"Best,");
        sb.AppendLine(H(agentName));

        return sb.ToString();
    }

    internal static string BuildEmailRecord(Lead lead, string subject, string body, string correlationId)
    {
        var sb = new StringBuilder();

        sb.AppendLine("---");
        sb.AppendLine($"leadId: {lead.Id}");
        sb.AppendLine($"sentAt: {DateTime.UtcNow:o}");
        sb.AppendLine($"subject: \"{NotificationHelpers.EscapeYaml(subject)}\"");
        sb.AppendLine($"recipientEmailHash: {NotificationHelpers.HashEmail(lead.Email)}");
        sb.AppendLine($"correlationId: {correlationId}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(body);

        return sb.ToString();
    }

    private static readonly CultureInfo UsFormat = new("en-US");

    private static string FormatUsd(decimal value) =>
        $"${value.ToString("N0", UsFormat)}";
}
