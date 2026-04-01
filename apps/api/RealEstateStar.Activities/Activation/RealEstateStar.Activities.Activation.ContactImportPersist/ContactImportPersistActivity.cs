using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Activities.Activation.ContactImportPersist;

/// <summary>
/// Persists imported contacts to the agent's Google Drive folder structure and ILeadStore.
///
/// Responsibilities:
/// - Creates 5 top-level pipeline stage folders in the agent's Drive
/// - Creates per-contact folders with stage-specific sub-folders
/// - Copies documents to the correct sub-folders by document type
/// - Saves/deduplicates contacts to ILeadStore by email
/// - Writes a "Client Import Summary.md" markdown table at the top level
/// </summary>
public sealed class ContactImportPersistActivity(
    IDocumentStorageProvider storage,
    IGDriveClient driveClient,
    ILeadStore leadStore,
    ILogger<ContactImportPersistActivity> logger)
    : ActivityBase(new ActivitySource("RealEstateStar.Activities.Activation.ContactImportPersist"), logger, "ContactImportPersist")
{
    internal static readonly string[] TopLevelFolders =
    [
        "1 - Leads",
        "2 - Active Clients",
        "3 - Under Contract",
        "4 - Closed",
        "5 - Inactive"
    ];

    internal const string ImportSummaryFile = "Client Import Summary.md";

    public async Task ExecuteAsync(
        string accountId,
        string agentId,
        IReadOnlyList<ImportedContact> contacts,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[CONTACT-IMPORT-010] Starting contact import persist for agentId={AgentId}, accountId={AccountId}, contactCount={Count}",
            agentId, accountId, contacts.Count);

        await ExecuteWithSpanAsync("CreateTopLevelFolders", async () =>
        {
            await CreateTopLevelFoldersAsync(accountId, agentId, ct);
        }, ct);

        await ExecuteWithSpanAsync("ProcessContacts", async () =>
        {
            await ProcessContactsAsync(accountId, agentId, contacts, ct);
        }, ct);

        await ExecuteWithSpanAsync("WriteImportSummary", async () =>
        {
            await WriteImportSummaryAsync(agentId, contacts, ct);
        }, ct);

        logger.LogInformation(
            "[CONTACT-IMPORT-090] Contact import persist complete for agentId={AgentId}, contactCount={Count}",
            agentId, contacts.Count);
    }

    // ── Top-level folder creation ──────────────────────────────────────────────

    private async Task CreateTopLevelFoldersAsync(string accountId, string agentId, CancellationToken ct)
    {
        var tasks = TopLevelFolders
            .Select(folder => driveClient.GetOrCreateFolderAsync(accountId, agentId, folder, ct))
            .ToList();

        await Task.WhenAll(tasks);
    }

    // ── Contact processing ─────────────────────────────────────────────────────

    private async Task ProcessContactsAsync(
        string accountId,
        string agentId,
        IReadOnlyList<ImportedContact> contacts,
        CancellationToken ct)
    {
        foreach (var contact in contacts)
        {
            ct.ThrowIfCancellationRequested();

            await ProcessContactAsync(accountId, agentId, contact, ct);
        }
    }

    private async Task ProcessContactAsync(
        string accountId,
        string agentId,
        ImportedContact contact,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[CONTACT-IMPORT-020] Processing contact Name={Name}, Stage={Stage}",
            contact.Name, contact.Stage);

        // Create contact folder under the stage top-level folder
        var stageFolderName = GetStageFolderName(contact.Stage);
        var contactFolderPath = $"{stageFolderName}/{contact.Name}";

        var stageFolderId = await driveClient.GetOrCreateFolderAsync(accountId, agentId, stageFolderName, ct);
        var contactFolderId = await driveClient.CreateFolderAsync(accountId, agentId, contactFolderPath, ct);

        // Create stage-specific sub-folders
        var subFolders = GetStageSubFolders(contact.Stage, contact.PropertyAddress);
        var subFolderTasks = subFolders
            .Select(sub => driveClient.CreateFolderAsync(accountId, agentId, $"{contactFolderPath}/{sub}", ct))
            .ToList();
        await Task.WhenAll(subFolderTasks);

        // Copy documents to the appropriate sub-folders
        foreach (var doc in contact.Documents)
        {
            ct.ThrowIfCancellationRequested();

            var targetSubFolder = GetDocumentSubFolder(contact.Stage, doc.Type, contact.PropertyAddress);
            if (targetSubFolder is not null)
            {
                var destFolderPath = $"{contactFolderPath}/{targetSubFolder}";
                var destFolderId = await driveClient.GetOrCreateFolderAsync(accountId, agentId, destFolderPath, ct);
                await driveClient.CopyFileAsync(accountId, agentId, doc.DriveFileId, destFolderId, doc.FileName, ct);
            }
        }

        // Persist to lead store (dedup by email)
        await PersistToLeadStoreAsync(agentId, contact, ct);
    }

    // ── Lead store persistence ─────────────────────────────────────────────────

    private async Task PersistToLeadStoreAsync(
        string agentId,
        ImportedContact contact,
        CancellationToken ct)
    {
        if (contact.Email is null)
        {
            logger.LogWarning(
                "[CONTACT-IMPORT-030] Skipping lead store persist for {Name} — no email address",
                contact.Name);
            return;
        }

        var existing = await leadStore.GetByEmailAsync(agentId, contact.Email, ct);
        if (existing is not null)
        {
            logger.LogInformation(
                "[CONTACT-IMPORT-031] Duplicate contact detected for email={Email}, updating status",
                contact.Email);

            var newStatus = MapStageToLeadStatus(contact.Stage);
            await leadStore.UpdateStatusAsync(existing, newStatus, ct);
            return;
        }

        var nameParts = SplitName(contact.Name);
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LeadType = MapRoleToLeadType(contact.Role),
            FirstName = nameParts.First,
            LastName = nameParts.Last,
            Email = contact.Email,
            Phone = contact.Phone ?? string.Empty,
            Timeline = "Imported",
            ReceivedAt = DateTime.UtcNow,
            Status = MapStageToLeadStatus(contact.Stage),
        };

        await leadStore.SaveAsync(lead, ct);

        logger.LogInformation(
            "[CONTACT-IMPORT-032] Lead saved for email={Email}, leadId={LeadId}",
            contact.Email, lead.Id);
    }

    // ── Import summary ─────────────────────────────────────────────────────────

    private async Task WriteImportSummaryAsync(
        string agentId,
        IReadOnlyList<ImportedContact> contacts,
        CancellationToken ct)
    {
        var markdown = BuildImportSummaryMarkdown(contacts);

        var existing = await storage.ReadDocumentAsync(agentId, ImportSummaryFile, ct);
        if (existing is null)
            await storage.WriteDocumentAsync(agentId, ImportSummaryFile, markdown, ct);
        else
            await storage.UpdateDocumentAsync(agentId, ImportSummaryFile, markdown, ct);
    }

    // ── Static helper methods (internal for testability) ──────────────────────

    internal static string GetStageFolderName(PipelineStage stage) => stage switch
    {
        PipelineStage.Lead => "1 - Leads",
        PipelineStage.ActiveClient => "2 - Active Clients",
        PipelineStage.UnderContract => "3 - Under Contract",
        PipelineStage.Closed => "4 - Closed",
        _ => "5 - Inactive"
    };

    internal static IReadOnlyList<string> GetStageSubFolders(PipelineStage stage, string? propertyAddress)
    {
        return stage switch
        {
            PipelineStage.Lead => BuildLeadSubFolders(propertyAddress),
            PipelineStage.ActiveClient => ["Agreements", "Documents Sent", "Communications"],
            PipelineStage.UnderContract => BuildUnderContractSubFolders(propertyAddress),
            PipelineStage.Closed => ["Audit Log", "Reports", "Communications"],
            _ => []
        };
    }

    private static IReadOnlyList<string> BuildLeadSubFolders(string? propertyAddress)
    {
        var folders = new List<string> { "Communications" };
        if (!string.IsNullOrWhiteSpace(propertyAddress))
            folders.Add(propertyAddress);
        return folders;
    }

    private static IReadOnlyList<string> BuildUnderContractSubFolders(string? propertyAddress)
    {
        var contractsFolder = string.IsNullOrWhiteSpace(propertyAddress)
            ? "Contracts"
            : $"{propertyAddress} Transaction/Contracts";

        return [contractsFolder, "Inspection", "Appraisal", "Communications"];
    }

    internal static string? GetDocumentSubFolder(
        PipelineStage stage,
        DocumentType documentType,
        string? propertyAddress)
    {
        return (stage, documentType) switch
        {
            (PipelineStage.ActiveClient, DocumentType.ListingAgreement) => "Agreements",
            (PipelineStage.ActiveClient, DocumentType.BuyerAgreement) => "Agreements",
            (PipelineStage.UnderContract, DocumentType.PurchaseContract) =>
                string.IsNullOrWhiteSpace(propertyAddress)
                    ? "Contracts"
                    : $"{propertyAddress} Transaction/Contracts",
            (PipelineStage.UnderContract, DocumentType.Inspection) => "Inspection",
            (PipelineStage.UnderContract, DocumentType.Appraisal) => "Appraisal",
            (PipelineStage.Closed, DocumentType.ClosingStatement) => "Audit Log",
            (PipelineStage.Closed, DocumentType.Cma) => "Reports",
            _ => null
        };
    }

    internal static LeadStatus MapStageToLeadStatus(PipelineStage stage) => stage switch
    {
        PipelineStage.Lead => LeadStatus.Received,
        PipelineStage.ActiveClient => LeadStatus.ActiveClient,
        PipelineStage.UnderContract => LeadStatus.UnderContract,
        PipelineStage.Closed => LeadStatus.Closed,
        _ => LeadStatus.Inactive
    };

    internal static LeadType MapRoleToLeadType(ContactRole role) => role switch
    {
        ContactRole.Buyer => LeadType.Buyer,
        ContactRole.Seller => LeadType.Seller,
        ContactRole.Both => LeadType.Both,
        _ => LeadType.Buyer
    };

    internal static string BuildImportSummaryMarkdown(IReadOnlyList<ImportedContact> contacts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Client Import Summary");
        sb.AppendLine();
        sb.AppendLine($"**Imported:** {contacts.Count} contact(s)");
        sb.AppendLine($"**Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("| Name | Email | Phone | Role | Stage | Property | Documents |");
        sb.AppendLine("|------|-------|-------|------|-------|----------|-----------|");

        foreach (var contact in contacts)
        {
            var name = EscapeMarkdownCell(contact.Name);
            var email = EscapeMarkdownCell(contact.Email ?? "—");
            var phone = EscapeMarkdownCell(contact.Phone ?? "—");
            var role = contact.Role.ToString();
            var stage = contact.Stage.ToString();
            var property = EscapeMarkdownCell(contact.PropertyAddress ?? "—");
            var docCount = contact.Documents.Count.ToString();

            sb.AppendLine($"| {name} | {email} | {phone} | {role} | {stage} | {property} | {docCount} |");
        }

        return sb.ToString();
    }

    // ── Private utilities ──────────────────────────────────────────────────────

    private static (string First, string Last) SplitName(string fullName)
    {
        var trimmed = fullName.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex < 0)
            return (trimmed, string.Empty);

        return (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..]);
    }

    private static string EscapeMarkdownCell(string value)
        => value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
