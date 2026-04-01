using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Activities.Lead.ContactDetection;

/// <summary>
/// Orchestrates contact detection from Drive document extractions and Gmail inbox emails.
/// Flattens Drive extraction clients, splits inbox by lead-generator vs general,
/// parses lead-gen emails via regex, sends general emails to Claude in batches,
/// then merges, deduplicates, and classifies all contacts.
/// </summary>
public sealed class ContactDetectionActivity : ActivityBase
{
    private readonly EmailContactExtractor _emailExtractor;
    private readonly ILogger<ContactDetectionActivity> _logger;

    public ContactDetectionActivity(
        IAnthropicClient anthropicClient,
        ILoggerFactory loggerFactory)
        : base(ContactDetectionDiagnostics.ActivitySource,
            loggerFactory.CreateLogger<ContactDetectionActivity>(),
            "ContactDetection")
    {
        _emailExtractor = new EmailContactExtractor(anthropicClient,
            loggerFactory.CreateLogger<EmailContactExtractor>());
        _logger = loggerFactory.CreateLogger<ContactDetectionActivity>();
    }

    /// <summary>
    /// Detects and classifies contacts from Drive document extractions and inbox emails.
    /// </summary>
    /// <param name="driveExtractions">Document extractions from the agent's Google Drive.</param>
    /// <param name="emailCorpus">The agent's Gmail corpus (inbox + sent emails + signature).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Classified and deduplicated list of imported contacts.</returns>
    public async Task<List<ImportedContact>> ExecuteAsync(
        IReadOnlyList<DocumentExtraction> driveExtractions,
        EmailCorpus emailCorpus,
        CancellationToken ct)
    {
        List<ImportedContact> result = null!;

        await ExecuteWithSpanAsync("Execute", async () =>
        {
            result = await RunAsync(driveExtractions, emailCorpus, ct);
        }, ct);

        return result;
    }

    private async Task<List<ImportedContact>> RunAsync(
        IReadOnlyList<DocumentExtraction> driveExtractions,
        EmailCorpus emailCorpus,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "[CONTACT-DETECT-010] Starting contact detection. Extractions: {ExtractionCount}, InboxEmails: {InboxCount}",
            driveExtractions.Count, emailCorpus.InboxEmails.Count);

        // Step 1: Flatten Drive extraction clients into (client, document) pairs
        var driveEntries = FlattenDriveExtractions(driveExtractions);

        // Step 2: Split inbox emails into lead-generator vs general
        var (leadGenEmails, generalEmails) = SplitInboxEmails(emailCorpus.InboxEmails);

        _logger.LogInformation(
            "[CONTACT-DETECT-020] Email split — LeadGen: {LeadGenCount}, General: {GeneralCount}",
            leadGenEmails.Count, generalEmails.Count);

        // Step 3: Parse lead-gen emails via regex
        var leadGenClients = ParseLeadGeneratorEmails(leadGenEmails);

        // Step 4: Extract general inbox contacts via Claude in batches
        var generalClients = await _emailExtractor.ExtractAsync(generalEmails, ct);

        // Step 5: Merge all sources into a unified (client, document) list
        var allEntries = new List<(ExtractedClient Client, DocumentReference? Document)>(
            driveEntries.Count + leadGenClients.Count + generalClients.Count);

        allEntries.AddRange(driveEntries);
        allEntries.AddRange(leadGenClients.Select(c => (c, (DocumentReference?)null)));
        allEntries.AddRange(generalClients.Select(c => (c, (DocumentReference?)null)));

        _logger.LogInformation(
            "[CONTACT-DETECT-030] Total entries before dedup: {Count} (Drive: {Drive}, LeadGen: {LeadGen}, General: {General})",
            allEntries.Count, driveEntries.Count, leadGenClients.Count, generalClients.Count);

        // Step 6: Classify and deduplicate
        var inputCount = allEntries.Count;
        var contacts = ContactClassifier.ClassifyAndDedup(allEntries);
        var mergedCount = inputCount - contacts.Count;

        // Step 7: Record OTel metrics
        RecordMetrics(contacts, mergedCount);

        _logger.LogInformation(
            "[CONTACT-DETECT-090] Contact detection complete. Total: {Total}, Merged: {Merged}",
            contacts.Count, mergedCount);

        return contacts.ToList();
    }

    internal static List<(ExtractedClient Client, DocumentReference? Document)> FlattenDriveExtractions(
        IReadOnlyList<DocumentExtraction> extractions)
    {
        var entries = new List<(ExtractedClient, DocumentReference?)>();

        foreach (var extraction in extractions)
        {
            var docRef = new DocumentReference(
                DriveFileId: extraction.DriveFileId,
                FileName: extraction.FileName,
                Type: extraction.Type,
                Date: extraction.Date);

            foreach (var client in extraction.Clients)
            {
                entries.Add((client, docRef));
            }
        }

        return entries;
    }

    internal static (List<EmailMessage> LeadGen, List<EmailMessage> General) SplitInboxEmails(
        IReadOnlyList<EmailMessage> inboxEmails)
    {
        var leadGen = new List<EmailMessage>();
        var general = new List<EmailMessage>();

        foreach (var email in inboxEmails)
        {
            if (LeadGeneratorPatterns.IsLeadGeneratorEmail(email.From))
                leadGen.Add(email);
            else
                general.Add(email);
        }

        return (leadGen, general);
    }

    internal static List<ExtractedClient> ParseLeadGeneratorEmails(IReadOnlyList<EmailMessage> emails)
    {
        var clients = new List<ExtractedClient>();

        foreach (var email in emails)
        {
            var client = LeadGeneratorPatterns.ParseLeadFromEmail(email.Subject, email.Body, email.From);
            if (client is not null)
                clients.Add(client);
        }

        return clients;
    }

    private static void RecordMetrics(IReadOnlyList<ImportedContact> contacts, int mergedCount)
    {
        // Count contacts by stage for the counter tags
        var stageCounts = contacts
            .GroupBy(c => c.Stage)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (stage, count) in stageCounts)
        {
            ContactDetectionDiagnostics.ContactsImported.Add(count,
                new KeyValuePair<string, object?>("stage", stage.ToString()));
        }

        if (mergedCount > 0)
        {
            ContactDetectionDiagnostics.DuplicatesMerged.Add(mergedCount);
        }
    }
}
