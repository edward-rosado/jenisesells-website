using RealEstateStar.DataServices.Leads;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Api.Services.Gws;

public class GwsService(ILogger<GwsService>? logger = null) : IGwsService
{
    /// <summary>
    /// Retry + circuit breaker pipeline for gws CLI process calls.
    /// 3 retries with exponential backoff (1s, 2s, 4s) + jitter.
    /// Circuit breaker: 5 failures in 60s → 2 min break (prevents wasting 90s+ on a dead CLI).
    /// Log codes: [GWS-020] retry, [GWS-022] CB opened, [GWS-023] CB closed.
    /// </summary>
    private readonly ResiliencePipeline _retryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(1),
            UseJitter = true,
            ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            OnRetry = args =>
            {
                logger?.LogWarning(
                    "[GWS-020] gws CLI retry {Attempt}/{MaxAttempts} after {DelayMs}ms. Error: {Error}",
                    args.AttemptNumber + 1, 3,
                    args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Exception?.Message);
                return ValueTask.CompletedTask;
            }
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 1.0,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(60),
            BreakDuration = TimeSpan.FromMinutes(2),
            ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            OnOpened = args =>
            {
                logger?.LogError(
                    "[GWS-022] gws CLI circuit OPEN for {BreakDurationSec}s. Last error: {Error}",
                    args.BreakDuration.TotalSeconds,
                    args.Outcome.Exception?.Message);
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                logger?.LogInformation("[GWS-023] gws CLI circuit CLOSED — resuming normal operations.");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    public async Task<string> CreateDriveFolderAsync(string agentEmail, string folderPath, CancellationToken ct)
    {
        logger?.LogInformation("Creating Drive folder {FolderPath} for {AgentEmail}", folderPath, HashEmail(agentEmail));
        return await RunGwsAsync(ct, "drive", "mkdir", "--user", agentEmail, folderPath);
    }

    public async Task<string> UploadFileAsync(string agentEmail, string folderPath, string filePath, CancellationToken ct)
    {
        logger?.LogInformation("Uploading {FilePath} to {FolderPath} for {AgentEmail}", filePath, folderPath, HashEmail(agentEmail));
        return await RunGwsAsync(ct, "drive", "upload", "--user", agentEmail, "--parent", folderPath, filePath);
    }

    public async Task<string> CreateDocAsync(string agentEmail, string folderPath, string title, string content, CancellationToken ct)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, content, ct);
            logger?.LogInformation("Creating Doc '{Title}' in {FolderPath} for {AgentEmail}", title, folderPath, HashEmail(agentEmail));
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

    public async Task SendEmailAsync(string agentEmail, string to, string subject, string body, string? attachmentPath, CancellationToken ct)
    {
        logger?.LogInformation("Sending email from {AgentEmail} to {To} subject '{Subject}'", HashEmail(agentEmail), to, subject);

        var args = new List<string> { "gmail", "send", "--user", agentEmail, "--to", to, "--subject", subject, "--body", body };

        if (!string.IsNullOrWhiteSpace(attachmentPath))
        {
            args.Add("--attachment");
            args.Add(attachmentPath);
        }

        await RunGwsAsync(ct, [.. args]);
    }

    public async Task AppendSheetRowAsync(string agentEmail, string spreadsheetId, List<string> values, CancellationToken ct)
    {
        var csv = string.Join(",", values.Select(v => $"\"{v.Replace("\"", "\"\"")}\""));
        logger?.LogInformation("Appending row to sheet {SpreadsheetId} for {AgentEmail}", spreadsheetId, HashEmail(agentEmail));
        await RunGwsAsync(ct, "sheets", "append", "--user", agentEmail, "--spreadsheet", spreadsheetId, "--values", csv);
    }

    public async Task<string> QueryDriveActivityAsync(
        string agentEmail, string ancestorFolder, DateTime since, CancellationToken ct)
    {
        return await RunGwsAsync(ct,
            "drive", "activity",
            "--user", agentEmail,
            "--ancestor", ancestorFolder,
            "--after", since.ToString("O"),
            "--format", "json");
    }

    private async Task<string> RunGwsAsync(CancellationToken ct, params string[] args)
    {
        return await _retryPipeline.ExecuteAsync(async token => await RunGwsCoreAsync(token, args), ct);
    }

    private async Task<string> RunGwsCoreAsync(CancellationToken ct, string[] args)
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

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeoutCts.Token);

        if (process.ExitCode != 0)
        {
            logger?.LogError("[GWS-021] gws process failed with exit code {ExitCode}: {Stderr}", process.ExitCode, stderr);
            throw new InvalidOperationException($"Google Workspace operation failed (exit code {process.ExitCode})");
        }

        return stdout.Trim();
    }

    public async Task<string?> DownloadDocAsync(string agentEmail, string folder, string fileName, CancellationToken ct)
    {
        try
        {
            return await RunGwsAsync(ct, "drive", "download", "--user", agentEmail, "--parent", folder, "--name", fileName);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[GWS-010] Failed to download doc '{FileName}' from '{Folder}'", fileName, folder);
            return null;
        }
    }

    public async Task UpdateDocAsync(string agentEmail, string folder, string fileName, string content, CancellationToken ct)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, content, ct);
            logger?.LogInformation("Updating Doc '{FileName}' in {FolderPath} for {AgentEmail}", fileName, folder, HashEmail(agentEmail));
            await RunGwsAsync(ct, "docs", "update", "--user", agentEmail, "--parent", folder, "--name", fileName, "--body-file", tempFile);
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

    public async Task DeleteDocAsync(string agentEmail, string folder, string fileName, CancellationToken ct)
    {
        logger?.LogInformation("Deleting Doc '{FileName}' in {FolderPath} for {AgentEmail}", fileName, folder, HashEmail(agentEmail));
        await RunGwsAsync(ct, "drive", "delete", "--user", agentEmail, "--parent", folder, "--name", fileName);
    }

    public async Task<List<string>> ListFilesAsync(string agentEmail, string folder, CancellationToken ct)
    {
        logger?.LogInformation("Listing files in {FolderPath} for {AgentEmail}", folder, HashEmail(agentEmail));
        var output = await RunGwsAsync(ct, "drive", "list", "--user", agentEmail, "--parent", folder, "--format", "names");
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    public async Task<List<List<string>>> ReadSheetAsync(string agentEmail, string sheetName, CancellationToken ct)
    {
        logger?.LogInformation("Reading sheet {SheetName} for {AgentEmail}", sheetName, HashEmail(agentEmail));
        var output = await RunGwsAsync(ct, "sheets", "read", "--user", agentEmail, "--spreadsheet", sheetName, "--format", "csv");
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(',').Select(v => v.Trim('"')).ToList())
            .ToList();
    }

    public async Task UpdateSheetRowsAsync(string agentEmail, string sheetName, string filterColumn, string filterValue, string replacementValue, CancellationToken ct)
    {
        logger?.LogInformation("Updating rows in sheet {SheetName} for {AgentEmail}", sheetName, HashEmail(agentEmail));
        await RunGwsAsync(ct, "sheets", "update-rows", "--user", agentEmail, "--spreadsheet", sheetName,
            "--filter-column", filterColumn, "--filter-value", filterValue, "--replacement", replacementValue);
    }

    private static string HashEmail(string email)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    // --- Static helper methods (testable without gws installed) ---

    public static string BuildLeadFolderPath(string leadName, string address) =>
        $"Real Estate Star/1 - Leads/{leadName}/{address}";

    public static string BuildLeadBriefContent(LeadBriefData data)
    {
        var firstName = data.LeadName.Split(' ')[0];
        var sb = new StringBuilder();

        sb.AppendLine($"New Lead Brief - {data.LeadName}");
        sb.AppendLine("========================================");
        sb.AppendLine();
        sb.AppendLine($"Property: {data.Address}");
        sb.AppendLine($"Timeline: {data.Timeline}");
        sb.AppendLine($"Submitted: {data.SubmittedAt:MMMM d, yyyy} at {data.SubmittedAt:h:mm tt}");
        sb.AppendLine();

        sb.AppendLine($"About {firstName}:");
        if (data.Occupation is not null || data.Employer is not null)
            sb.AppendLine($"  {data.Occupation} at {data.Employer}");
        if (data.PurchaseDate.HasValue && data.PurchasePrice.HasValue)
            sb.AppendLine($"  Purchased {data.Address} in {data.PurchaseDate.Value:MMMM yyyy} for {data.PurchasePrice.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}");
        if (data.OwnershipDuration is not null)
            sb.AppendLine($"  Owned for {data.OwnershipDuration}");
        if (data.EquityRange is not null)
            sb.AppendLine($"  Estimated equity: {data.EquityRange}");
        if (data.LifeEvent is not null)
            sb.AppendLine($"  {data.LifeEvent}");
        sb.AppendLine();

        sb.AppendLine("Property Details (public records):");
        if (data.Beds.HasValue && data.Baths.HasValue && data.Sqft.HasValue && data.YearBuilt.HasValue)
            sb.AppendLine($"  {data.Beds} bed / {data.Baths} bath / {data.Sqft.Value:N0} sqft, built {data.YearBuilt}");
        if (data.LotSize is not null)
            sb.AppendLine($"  Lot: {data.LotSize}");
        if (data.TaxAssessment.HasValue)
            sb.AppendLine($"  Current tax assessment: {data.TaxAssessment.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}");
        if (data.AnnualTax.HasValue)
            sb.AppendLine($"  Annual property taxes: {data.AnnualTax.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}");
        sb.AppendLine();

        sb.AppendLine("Market Context:");
        sb.AppendLine($"  {data.CompCount} comparable sales found in {data.SearchRadius}");
        sb.AppendLine($"  Estimated current value: {data.ValueRange}");
        sb.AppendLine($"  Median days on market: {data.MedianDom}");
        sb.AppendLine($"  Market trending: {data.MarketTrend} market");
        sb.AppendLine();

        sb.AppendLine("Conversation Starters:");
        foreach (var starter in data.ConversationStarters)
            sb.AppendLine($"  \"{starter}\"");
        sb.AppendLine();

        sb.AppendLine($"CMA Status: Sent to {data.LeadEmail}");
        sb.AppendLine($"CMA Report: {data.PdfLink}");
        sb.AppendLine();

        sb.AppendLine("Recommended Next Steps:");
        var priorityAction = data.Timeline switch
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
        sb.AppendLine($"  Phone: {data.LeadPhone}");
        sb.AppendLine($"  Email: {data.LeadEmail}");

        return sb.ToString();
    }
}
