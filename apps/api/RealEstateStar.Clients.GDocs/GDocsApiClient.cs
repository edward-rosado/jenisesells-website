using System.Diagnostics;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.GDocs;

internal sealed class GDocsApiClient(
    IOAuthRefresher refresher,
    ILogger<GDocsApiClient> logger) : IGDocsClient
{
    public async Task<string> CreateDocumentAsync(
        string accountId,
        string agentId,
        string title,
        string content,
        CancellationToken ct)
    {
        var credential = await refresher.GetValidCredentialAsync(accountId, agentId, ct);
        if (credential is null)
        {
            logger.LogWarning(
                "[GDOCS-010] No valid token for account {AccountId}, agent {AgentId}. Skipping CreateDocument.",
                accountId, agentId);
            GDocsDiagnostics.TokenMissing.Add(1);
            return string.Empty;
        }

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDocsDiagnostics.ActivitySource.StartActivity("gdocs.create");
        activity?.SetTag("gdocs.account_id", accountId);

        try
        {
            var service = BuildDocsService(credential);

            // Create the document with a title
            var createRequest = service.Documents.Create(new Document { Title = title });
            var document = await createRequest.ExecuteAsync(ct);
            var documentId = document.DocumentId;

            // Insert the content via BatchUpdate
            if (!string.IsNullOrEmpty(content))
            {
                await InsertTextAsync(service, documentId, content, ct);
            }

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDocsDiagnostics.Operations.Add(1);
            GDocsDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDOCS-001] Document created for account {AccountId}, agent {AgentId}. DocumentId: {DocumentId}, Duration: {Duration}ms",
                accountId, agentId, documentId, durationMs);

            return documentId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDocsDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDOCS-031] CreateDocument failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task<string?> ReadDocumentAsync(
        string accountId,
        string agentId,
        string documentId,
        CancellationToken ct)
    {
        var credential = await refresher.GetValidCredentialAsync(accountId, agentId, ct);
        if (credential is null)
        {
            logger.LogWarning(
                "[GDOCS-010] No valid token for account {AccountId}, agent {AgentId}. Skipping ReadDocument.",
                accountId, agentId);
            GDocsDiagnostics.TokenMissing.Add(1);
            return null;
        }

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDocsDiagnostics.ActivitySource.StartActivity("gdocs.read");
        activity?.SetTag("gdocs.account_id", accountId);
        activity?.SetTag("gdocs.document_id", documentId);

        try
        {
            var service = BuildDocsService(credential);
            var document = await service.Documents.Get(documentId).ExecuteAsync(ct);
            var text = ExtractPlainText(document);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDocsDiagnostics.Operations.Add(1);
            GDocsDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDOCS-002] Document read for account {AccountId}, agent {AgentId}. DocumentId: {DocumentId}, Duration: {Duration}ms",
                accountId, agentId, documentId, durationMs);

            return text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDocsDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDOCS-032] ReadDocument failed for account {AccountId}, agent {AgentId}, documentId {DocumentId}",
                accountId, agentId, documentId);
            throw;
        }
    }

    public async Task UpdateDocumentAsync(
        string accountId,
        string agentId,
        string documentId,
        string content,
        CancellationToken ct)
    {
        var credential = await refresher.GetValidCredentialAsync(accountId, agentId, ct);
        if (credential is null)
        {
            logger.LogWarning(
                "[GDOCS-010] No valid token for account {AccountId}, agent {AgentId}. Skipping UpdateDocument.",
                accountId, agentId);
            GDocsDiagnostics.TokenMissing.Add(1);
            return;
        }

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDocsDiagnostics.ActivitySource.StartActivity("gdocs.update");
        activity?.SetTag("gdocs.account_id", accountId);
        activity?.SetTag("gdocs.document_id", documentId);

        try
        {
            var service = BuildDocsService(credential);

            // Get current document to find the end index for clearing
            var document = await service.Documents.Get(documentId).ExecuteAsync(ct);
            var endIndex = GetBodyEndIndex(document);

            var requests = new List<Request>();

            // Delete all existing body content (if any) before inserting new content
            if (endIndex > 1)
            {
                requests.Add(new Request
                {
                    DeleteContentRange = new DeleteContentRangeRequest
                    {
                        Range = new Google.Apis.Docs.v1.Data.Range { StartIndex = 1, EndIndex = endIndex }
                    }
                });
            }

            // Insert the new content at index 1
            if (!string.IsNullOrEmpty(content))
            {
                requests.Add(new Request
                {
                    InsertText = new InsertTextRequest
                    {
                        Text = content,
                        Location = new Location { Index = 1 }
                    }
                });
            }

            if (requests.Count > 0)
            {
                var batchUpdate = new BatchUpdateDocumentRequest { Requests = requests };
                await service.Documents.BatchUpdate(batchUpdate, documentId).ExecuteAsync(ct);
            }

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDocsDiagnostics.Operations.Add(1);
            GDocsDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDOCS-003] Document updated for account {AccountId}, agent {AgentId}. DocumentId: {DocumentId}, Duration: {Duration}ms",
                accountId, agentId, documentId, durationMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDocsDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDOCS-033] UpdateDocument failed for account {AccountId}, agent {AgentId}, documentId {DocumentId}",
                accountId, agentId, documentId);
            throw;
        }
    }

    private static async Task InsertTextAsync(
        DocsService service,
        string documentId,
        string text,
        CancellationToken ct)
    {
        var requests = new List<Request>
        {
            new()
            {
                InsertText = new InsertTextRequest
                {
                    Text = text,
                    Location = new Location { Index = 1 }
                }
            }
        };

        var batchUpdate = new BatchUpdateDocumentRequest { Requests = requests };
        await service.Documents.BatchUpdate(batchUpdate, documentId).ExecuteAsync(ct);
    }

    internal static string ExtractPlainText(Document document)
    {
        if (document.Body?.Content is null)
            return string.Empty;

        var sb = new System.Text.StringBuilder();

        foreach (var element in document.Body.Content)
        {
            if (element.Paragraph?.Elements is null)
                continue;

            foreach (var paragraphElement in element.Paragraph.Elements)
            {
                if (paragraphElement.TextRun?.Content is not null)
                    sb.Append(paragraphElement.TextRun.Content);
            }
        }

        return sb.ToString();
    }

    internal static int GetBodyEndIndex(Document document)
    {
        var content = document.Body?.Content;
        if (content is null || content.Count == 0)
            return 1;

        // The last structural element's end index is the document body end
        var lastElement = content[content.Count - 1];
        return (int)(lastElement.EndIndex ?? 1) - 1;
    }

    private static DocsService BuildDocsService(Domain.Shared.Models.OAuthCredential credential) =>
        new(RealEstateStar.Clients.GoogleOAuth.GoogleCredentialFactory.BuildInitializer(credential));
}
