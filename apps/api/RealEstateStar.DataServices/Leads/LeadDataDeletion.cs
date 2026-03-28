using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.DataServices.Leads;

public class LeadDataDeletion(
    ILeadStore leadStore,
    IMarketingConsentLog consentLog,
    IDeletionAuditLog auditLog,
    IDocumentStorageProvider storage,
    IGwsService gwsService,
    ILogger<LeadDataDeletion> logger) : ILeadDataDeletion
{
    private const string TokenFolder = "Deletion Tokens";
    private static readonly TimeSpan TokenExpiry = TimeSpan.FromHours(24);

    // ── Initiation ────────────────────────────────────────────────────────────

    public async Task<string> InitiateDeletionRequestAsync(string agentId, string email, CancellationToken ct)
    {
        logger.LogInformation("[LEAD-050] Initiating deletion request for agent {AgentId}", agentId);

        // Generate 128-bit cryptographically random token
        var tokenBytes = RandomNumberGenerator.GetBytes(16);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        // Hash the token for storage (never store the raw token)
        var tokenHash = ComputeTokenHash(token);

        // Build token metadata
        var lead = await leadStore.GetByEmailAsync(agentId, email, ct);
        var leadId = lead?.Id ?? Guid.Empty;

        var tokenData = new DeletionTokenData(email, DateTime.UtcNow.Add(TokenExpiry), agentId);
        var tokenJson = JsonSerializer.Serialize(tokenData);

        // Store the hashed token in Drive
        var tokenFolder = LeadPaths.DeletionTokensFolder(agentId);
        var tokenFile = $"{tokenHash}.json";
        await storage.EnsureFolderExistsAsync(tokenFolder, ct);
        await storage.WriteDocumentAsync(tokenFolder, tokenFile, tokenJson, ct);

        logger.LogInformation("[LEAD-051] Deletion token stored for agent {AgentId}", agentId);

        // Send verification email via GWS
        // We use gwsService directly because we need the agent's own email as the sender account
        // but we're sending TO the lead's email
        await gwsService.SendEmailAsync(
            agentId,
            email,
            "Your Data Deletion Request — Verification Required",
            BuildVerificationEmailBody(token),
            null,
            ct);

        logger.LogInformation("[LEAD-052] Verification email sent for agent {AgentId}", agentId);

        // Record initiation in deletion audit log
        if (leadId != Guid.Empty)
        {
            await auditLog.RecordInitiationAsync(agentId, leadId, email, ct);
            logger.LogInformation("[LEAD-053] Deletion initiation recorded in audit log for lead {LeadId}", leadId);
        }
        else
        {
            logger.LogWarning("[LEAD-054] No lead found for email during deletion initiation for agent {AgentId}", agentId);
        }

        return token;
    }

    // ── Execution ─────────────────────────────────────────────────────────────

    public async Task<DeleteResult> ExecuteDeletionAsync(
        string agentId,
        string email,
        string token,
        string reason,
        CancellationToken ct)
    {
        logger.LogInformation("[LEAD-055] Executing deletion for agent {AgentId}", agentId);

        // Validate token
        var tokenHash = ComputeTokenHash(token);
        var tokenFolder = LeadPaths.DeletionTokensFolder(agentId);
        var tokenFile = $"{tokenHash}.json";

        var tokenJson = await storage.ReadDocumentAsync(tokenFolder, tokenFile, ct);
        if (tokenJson is null)
        {
            logger.LogWarning("[LEAD-056] Deletion token not found for agent {AgentId}", agentId);
            return new DeleteResult(false, [], "Invalid or expired deletion token.");
        }

        DeletionTokenData tokenData;
        try
        {
            tokenData = JsonSerializer.Deserialize<DeletionTokenData>(tokenJson)
                ?? throw new InvalidOperationException("Token data deserialized to null.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LEAD-057] Failed to deserialize deletion token for agent {AgentId}", agentId);
            return new DeleteResult(false, [], "Invalid deletion token format.");
        }

        // Check expiry
        if (DateTime.UtcNow > tokenData.ExpiresAt)
        {
            logger.LogWarning("[LEAD-058] Deletion token expired for agent {AgentId}", agentId);
            return new DeleteResult(false, [], "Deletion token has expired.");
        }

        // Validate email matches token
        if (!string.Equals(tokenData.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[LEAD-059] Deletion token email mismatch for agent {AgentId}", agentId);
            return new DeleteResult(false, [], "Invalid or expired deletion token.");
        }

        // Find the lead
        var lead = await leadStore.GetByEmailAsync(agentId, email, ct);
        if (lead is null)
        {
            logger.LogWarning("[LEAD-060] Lead not found for deletion for agent {AgentId}", agentId);
            return new DeleteResult(false, [], "Lead not found.");
        }

        var deletedItems = new List<string>();
        var leadFolder = LeadPaths.LeadFolder(lead.FullName);

        // Delete Lead Profile.md
        await storage.DeleteDocumentAsync(leadFolder, "Lead Profile.md", ct);
        deletedItems.Add("Lead Profile.md");
        logger.LogInformation("[LEAD-061] Deleted Lead Profile.md for lead {LeadId}", lead.Id);

        // Delete Research & Insights.md
        await storage.DeleteDocumentAsync(leadFolder, "Research & Insights.md", ct);
        deletedItems.Add("Research & Insights.md");
        logger.LogInformation("[LEAD-062] Deleted Research & Insights.md for lead {LeadId}", lead.Id);

        // Delete home search files
        var homeSearchFolder = $"{leadFolder}/Home Search";
        var homeSearchFiles = await storage.ListDocumentsAsync(homeSearchFolder, ct);
        foreach (var file in homeSearchFiles)
        {
            await storage.DeleteDocumentAsync(homeSearchFolder, file, ct);
            deletedItems.Add($"Home Search/{file}");
        }

        if (homeSearchFiles.Count > 0)
        {
            logger.LogInformation("[LEAD-063] Deleted {Count} home search file(s) for lead {LeadId}", homeSearchFiles.Count, lead.Id);
        }

        // Redact consent log rows
        await consentLog.RedactAsync(agentId, email, ct);
        deletedItems.Add("Consent log entries (redacted)");
        logger.LogInformation("[LEAD-064] Redacted consent log rows for lead {LeadId}", lead.Id);

        // Record completion in deletion audit log (email is [REDACTED] in completion record per DeletionAuditLog)
        await auditLog.RecordCompletionAsync(agentId, lead.Id, ct);
        logger.LogInformation("[LEAD-065] Deletion completion recorded in audit log for lead {LeadId}", lead.Id);

        // Clean up token file
        await storage.DeleteDocumentAsync(tokenFolder, tokenFile, ct);
        logger.LogInformation("[LEAD-066] Deletion token cleaned up for agent {AgentId}", agentId);

        return new DeleteResult(true, deletedItems);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    internal static string ComputeTokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildVerificationEmailBody(string token) =>
        $"""
        You have requested deletion of your personal data.

        Please use the following verification token to confirm your request:

        Token: {token}

        This token expires in 24 hours. If you did not make this request, please ignore this email.

        To complete the deletion, provide this token along with your email address.
        """;

    private record DeletionTokenData(string Email, DateTime ExpiresAt, string AgentId);
}
