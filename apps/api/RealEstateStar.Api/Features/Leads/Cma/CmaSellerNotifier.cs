using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using RealEstateStar.Api.Common;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Gws;

namespace RealEstateStar.Api.Features.Leads.Cma;

public class CmaSellerNotifier(
    IGwsService gwsService,
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
        var agentEmail = agent?.Agent?.Email ?? "";
        var agentName = agent?.Agent?.Name ?? agentId;
        var agentPhone = agent?.Agent?.Phone ?? "";

        var sd = lead.SellerDetails!;
        var fullAddress = $"{sd.Address}, {sd.City}, {sd.State} {sd.Zip}";
        var cmaFolder = LeadPaths.CmaFolder(lead.FullName, fullAddress);

        var subject = $"Your Comparative Market Analysis \u2013 {fullAddress}";
        var body = BuildEmailBody(lead, analysis, agentName, agentPhone);

        // Step 1: Send email — failure is fatal; caller decides whether to retry
        var emailSw = Stopwatch.GetTimestamp();
        logger.LogInformation(
            "[CMA-NOTIFY-001] Sending CMA email to {RecipientHash} for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
            HashEmail(lead.Email), lead.Id, agentId, correlationId);

        try
        {
            await gwsService.SendEmailAsync(agentEmail, lead.Email, subject, body, pdfPath, ct);
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

        // Step 2: Upload PDF to Drive — failure is non-fatal
        var driveSw = Stopwatch.GetTimestamp();
        logger.LogInformation(
            "[CMA-NOTIFY-003] Uploading PDF to Drive for lead {LeadId}. Folder: {Folder}. CorrelationId: {CorrelationId}",
            lead.Id, cmaFolder, correlationId);

        try
        {
            await gwsService.UploadFileAsync(agentEmail, cmaFolder, pdfPath, ct);
            CmaDiagnostics.DriveDuration.Record(Stopwatch.GetElapsedTime(driveSw).TotalMilliseconds);

            logger.LogInformation(
                "[CMA-NOTIFY-004] Upload complete for lead {LeadId}. Duration: {DurationMs}ms. CorrelationId: {CorrelationId}",
                lead.Id, Stopwatch.GetElapsedTime(driveSw).TotalMilliseconds, correlationId);
        }
        catch (Exception ex)
        {
            CmaDiagnostics.DriveDuration.Record(Stopwatch.GetElapsedTime(driveSw).TotalMilliseconds);
            logger.LogError(ex,
                "[CMA-NOTIFY-008] Drive upload failed for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
                lead.Id, agentId, correlationId);
        }

        // Step 3: Store communication record — failure is non-fatal
        logger.LogInformation(
            "[CMA-NOTIFY-005] Storing communication record for lead {LeadId}. CorrelationId: {CorrelationId}",
            lead.Id, correlationId);

        try
        {
            var emailRecord = BuildEmailRecord(lead, subject, body, correlationId);
            await gwsService.CreateDocAsync(agentEmail, cmaFolder, "CMA Email Record", emailRecord, ct);

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
        var sd = lead.SellerDetails!;
        var fullAddress = $"{sd.Address}, {sd.City}, {sd.State} {sd.Zip}";
        var sb = new StringBuilder();

        sb.AppendLine($"Hi {lead.FirstName},");
        sb.AppendLine();
        sb.AppendLine($"Thank you for reaching out! {agentName} has prepared a Comparative Market Analysis for your property at {fullAddress}.");
        sb.AppendLine();
        sb.AppendLine("Based on recent comparable sales in your area, here is the estimated value range:");
        sb.AppendLine();
        sb.AppendLine($"  Low:  {analysis.ValueLow:C0}");
        sb.AppendLine($"  Mid:  {analysis.ValueMid:C0}");
        sb.AppendLine($"  High: {analysis.ValueHigh:C0}");
        sb.AppendLine();
        sb.AppendLine($"Market trend: {analysis.MarketTrend}");
        sb.AppendLine($"Median days on market: {analysis.MedianDaysOnMarket} days");
        sb.AppendLine();
        sb.AppendLine("Your full CMA report is attached as a PDF.");
        sb.AppendLine();
        sb.AppendLine($"Ready to discuss your options? Reply to this email or call {agentPhone} — we would love to walk you through the findings.");
        sb.AppendLine();
        sb.AppendLine($"Best,");
        sb.AppendLine(agentName);

        return sb.ToString();
    }

    internal static string BuildEmailRecord(Lead lead, string subject, string body, string correlationId)
    {
        var sb = new StringBuilder();

        sb.AppendLine("---");
        sb.AppendLine($"leadId: {lead.Id}");
        sb.AppendLine($"sentAt: {DateTime.UtcNow:o}");
        sb.AppendLine($"subject: \"{EscapeYaml(subject)}\"");
        sb.AppendLine($"recipientEmailHash: {HashEmail(lead.Email)}");
        sb.AppendLine($"correlationId: {correlationId}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(body);

        return sb.ToString();
    }

    private static string HashEmail(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    private static string EscapeYaml(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
