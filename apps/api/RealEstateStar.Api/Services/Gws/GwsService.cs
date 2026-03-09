using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Services.Gws;

public class GwsService(ILogger<GwsService>? logger = null) : IGwsService
{
    public async Task<string> CreateDriveFolderAsync(string agentEmail, string folderPath, CancellationToken ct = default)
    {
        logger?.LogInformation("Creating Drive folder {FolderPath} for {Email}", folderPath, agentEmail);
        return await RunGwsAsync(ct, "drive", "mkdir", "--user", agentEmail, folderPath);
    }

    public async Task<string> UploadFileAsync(string agentEmail, string folderPath, string filePath, CancellationToken ct = default)
    {
        logger?.LogInformation("Uploading {FilePath} to {FolderPath} for {Email}", filePath, folderPath, agentEmail);
        return await RunGwsAsync(ct, "drive", "upload", "--user", agentEmail, "--parent", folderPath, filePath);
    }

    public async Task<string> CreateDocAsync(string agentEmail, string folderPath, string title, string content, CancellationToken ct = default)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, content, ct);
            logger?.LogInformation("Creating Doc '{Title}' in {FolderPath} for {Email}", title, folderPath, agentEmail);
            return await RunGwsAsync(ct, "docs", "create", "--user", agentEmail, "--parent", folderPath, "--title", title, "--body-file", tempFile);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch (IOException ex)
            {
                logger?.LogWarning(ex, "Failed to delete temp file {TempFile}", tempFile);
            }
        }
    }

    public async Task SendEmailAsync(string agentEmail, string to, string subject, string body, string? attachmentPath = null, CancellationToken ct = default)
    {
        logger?.LogInformation("Sending email from {Email} to {To} subject '{Subject}'", agentEmail, to, subject);

        var args = new List<string> { "gmail", "send", "--user", agentEmail, "--to", to, "--subject", subject, "--body", body };

        if (!string.IsNullOrWhiteSpace(attachmentPath))
        {
            args.Add("--attachment");
            args.Add(attachmentPath);
        }

        await RunGwsAsync(ct, [.. args]);
    }

    public async Task AppendSheetRowAsync(string agentEmail, string spreadsheetId, List<string> values, CancellationToken ct = default)
    {
        var csv = string.Join(",", values.Select(v => $"\"{v.Replace("\"", "\"\"")}\""));
        logger?.LogInformation("Appending row to sheet {SpreadsheetId} for {Email}", spreadsheetId, agentEmail);
        await RunGwsAsync(ct, "sheets", "append", "--user", agentEmail, "--spreadsheet", spreadsheetId, "--values", csv);
    }

    private async Task<string> RunGwsAsync(CancellationToken ct, params string[] args)
    {
        logger?.LogDebug("Running: gws {Args}", string.Join(" ", args));

        var psi = new ProcessStartInfo("gws")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };

        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            logger?.LogError("gws exited with code {ExitCode}: {Stderr}", process.ExitCode, stderr);
            throw new InvalidOperationException($"gws failed (exit code {process.ExitCode}): {stderr.Trim()}");
        }

        return stdout.Trim();
    }

    // --- Static helper methods (testable without gws installed) ---

    public static string BuildLeadFolderPath(string leadName, string address) =>
        $"Real Estate Star/1 - Leads/{leadName}/{address}";

    public static string BuildLeadBriefContent(
        string leadName,
        string address,
        string timeline,
        DateTime submittedAt,
        string? occupation,
        string? employer,
        DateOnly? purchaseDate,
        decimal? purchasePrice,
        string? ownershipDuration,
        string? equityRange,
        string? lifeEvent,
        int? beds,
        int? baths,
        int? sqft,
        int? yearBuilt,
        string? lotSize,
        decimal? taxAssessment,
        decimal? annualTax,
        int compCount,
        string searchRadius,
        string valueRange,
        int medianDom,
        string marketTrend,
        List<string> conversationStarters,
        string leadEmail,
        string leadPhone,
        string pdfLink)
    {
        var firstName = leadName.Split(' ')[0];
        var sb = new StringBuilder();

        sb.AppendLine($"New Lead Brief - {leadName}");
        sb.AppendLine("========================================");
        sb.AppendLine();
        sb.AppendLine($"Property: {address}");
        sb.AppendLine($"Timeline: {timeline}");
        sb.AppendLine($"Submitted: {submittedAt:MMMM d, yyyy} at {submittedAt:h:mm tt}");
        sb.AppendLine();

        sb.AppendLine($"About {firstName}:");
        if (occupation is not null || employer is not null)
            sb.AppendLine($"  {occupation} at {employer}");
        if (purchaseDate.HasValue && purchasePrice.HasValue)
            sb.AppendLine($"  Purchased {address} in {purchaseDate.Value:MMMM yyyy} for {purchasePrice.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}");
        if (ownershipDuration is not null)
            sb.AppendLine($"  Owned for {ownershipDuration}");
        if (equityRange is not null)
            sb.AppendLine($"  Estimated equity: {equityRange}");
        if (lifeEvent is not null)
            sb.AppendLine($"  {lifeEvent}");
        sb.AppendLine();

        sb.AppendLine("Property Details (public records):");
        if (beds.HasValue && baths.HasValue && sqft.HasValue && yearBuilt.HasValue)
            sb.AppendLine($"  {beds} bed / {baths} bath / {sqft.Value:N0} sqft, built {yearBuilt}");
        if (lotSize is not null)
            sb.AppendLine($"  Lot: {lotSize}");
        if (taxAssessment.HasValue)
            sb.AppendLine($"  Current tax assessment: {taxAssessment.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}");
        if (annualTax.HasValue)
            sb.AppendLine($"  Annual property taxes: {annualTax.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}");
        sb.AppendLine();

        sb.AppendLine("Market Context:");
        sb.AppendLine($"  {compCount} comparable sales found in {searchRadius}");
        sb.AppendLine($"  Estimated current value: {valueRange}");
        sb.AppendLine($"  Median days on market: {medianDom}");
        sb.AppendLine($"  Market trending: {marketTrend} market");
        sb.AppendLine();

        sb.AppendLine("Conversation Starters:");
        foreach (var starter in conversationStarters)
            sb.AppendLine($"  \"{starter}\"");
        sb.AppendLine();

        sb.AppendLine($"CMA Status: Sent to {leadEmail}");
        sb.AppendLine($"CMA Report: {pdfLink}");
        sb.AppendLine();

        sb.AppendLine("Recommended Next Steps:");
        var priorityAction = timeline switch
        {
            "ASAP" => "Call within 1 hour — this lead is ready NOW",
            "1-3 months" => "Call within 2 hours — serious seller, time-sensitive",
            _ => "Call within 24 hours — build the relationship early"
        };
        sb.AppendLine($"  1. {priorityAction}");
        sb.AppendLine("  2. Reference their situation naturally in conversation");
        sb.AppendLine("  3. Schedule walkthrough");
        sb.AppendLine("  4. Prepare listing agreement");
        sb.AppendLine();

        sb.AppendLine("Contact:");
        sb.AppendLine($"  Phone: {leadPhone}");
        sb.AppendLine($"  Email: {leadEmail}");

        return sb.ToString();
    }
}
