using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.DataServices.Leads;

/// <summary>
/// Writes failed lead operations to a local JSON file as a last resort.
/// Survives when Azure Table Storage is unavailable.
/// </summary>
public class LeadDeadLetterDataService(string basePath, ILogger<LeadDeadLetterDataService> logger) : ILeadDeadLetterDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task RecordAsync(Lead lead, string operation, string lastError, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(basePath);
            var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}_{lead.Id}_{operation}.json";
            var filePath = Path.Combine(basePath, fileName);

            var record = new
            {
                Timestamp = DateTime.UtcNow,
                LeadId = lead.Id,
                AgentId = lead.AgentId,
                Operation = operation,
                Error = lastError,
                Lead = new
                {
                    lead.FirstName,
                    lead.LastName,
                    lead.Email,
                    lead.Phone,
                    lead.LeadType,
                    lead.Timeline,
                    SellerAddress = lead.SellerDetails?.Address,
                    SellerCity = lead.SellerDetails?.City,
                    SellerState = lead.SellerDetails?.State,
                    SellerZip = lead.SellerDetails?.Zip,
                },
            };

            var json = JsonSerializer.Serialize(record, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);

            logger.LogWarning("[DLQ-001] Dead letter recorded for lead {LeadId}, operation: {Operation}. File: {FilePath}",
                lead.Id, operation, filePath);
        }
        catch (Exception ex)
        {
            // Last resort — if even dead letter fails, log everything we can
            logger.LogCritical(ex,
                "[DLQ-002] DEAD LETTER WRITE FAILED for lead {LeadId}. Operation: {Operation}. Error: {OriginalError}. " +
                "Lead data: {FirstName} {LastName}, {Email}, {Phone}",
                lead.Id, operation, lastError, lead.FirstName, lead.LastName, lead.Email, lead.Phone);
        }
    }
}
