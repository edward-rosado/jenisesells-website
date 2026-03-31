using System.Diagnostics;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Microsoft.Extensions.Logging;
using MimeKit;
using RealEstateStar.Clients.GoogleOAuth;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Gmail;

internal sealed class GmailReaderClient(
    IOAuthRefresher refresher,
    string clientId,
    string clientSecret,
    ILogger<GmailReaderClient> logger) : IGmailReader
{
    public Task<IReadOnlyList<EmailMessage>> GetSentEmailsAsync(
        string accountId, string agentId, int maxResults, CancellationToken ct) =>
        FetchEmailsAsync(accountId, agentId, "in:sent", maxResults, ct);

    public Task<IReadOnlyList<EmailMessage>> GetInboxEmailsAsync(
        string accountId, string agentId, int maxResults, CancellationToken ct) =>
        FetchEmailsAsync(accountId, agentId, "in:inbox", maxResults, ct);

    private async Task<IReadOnlyList<EmailMessage>> FetchEmailsAsync(
        string accountId,
        string agentId,
        string query,
        int maxResults,
        CancellationToken ct)
    {
        var credential = await refresher.GetValidCredentialAsync(accountId, agentId, ct);
        if (credential is null)
        {
            logger.LogWarning(
                "[GMAILREADER-010] No valid token for account {AccountId}, agent {AgentId}. Returning empty list.",
                accountId, agentId);
            GmailDiagnostics.TokenMissing.Add(1);
            return [];
        }

        var sw = Stopwatch.GetTimestamp();
        using var activity = GmailDiagnostics.ActivitySource.StartActivity("gmail.read");
        activity?.SetTag("gmail.account_id", accountId);
        activity?.SetTag("gmail.query", query);

        try
        {
            var gmailService = new GmailService(
                GoogleCredentialFactory.BuildInitializer(credential, clientId, clientSecret));

            // List message IDs
            var listRequest = gmailService.Users.Messages.List("me");
            listRequest.Q = query;
            listRequest.MaxResults = maxResults;
            var listResult = await listRequest.ExecuteAsync(ct);

            if (listResult.Messages is null || listResult.Messages.Count == 0)
            {
                logger.LogInformation(
                    "[GMAILREADER-011] No messages found for account {AccountId}, agent {AgentId}, query {Query}.",
                    accountId, agentId, query);
                return [];
            }

            // Fetch each message in full
            var emails = new List<EmailMessage>(listResult.Messages.Count);
            foreach (var msgRef in listResult.Messages)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var getRequest = gmailService.Users.Messages.Get("me", msgRef.Id);
                    getRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Raw;
                    var rawMessage = await getRequest.ExecuteAsync(ct);
                    var email = ParseRawMessage(rawMessage);
                    if (email is not null)
                        emails.Add(email);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex,
                        "[GMAILREADER-012] Failed to fetch message {MessageId} for account {AccountId}",
                        msgRef.Id, accountId);
                }
            }

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GmailDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GMAILREADER-001] Fetched {Count} emails for account {AccountId}, agent {AgentId}, query {Query}. Duration: {Duration}ms",
                emails.Count, accountId, agentId, query, durationMs);

            return emails;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GmailDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GMAILREADER-030] FetchEmails failed for account {AccountId}, agent {AgentId}, query {Query}",
                accountId, agentId, query);
            throw;
        }
    }

    internal static EmailMessage? ParseRawMessage(Message rawMessage)
    {
        if (rawMessage.Raw is null)
            return null;

        try
        {
            // Decode base64url
            var base64 = rawMessage.Raw
                .Replace('-', '+')
                .Replace('_', '/');
            var paddingNeeded = (4 - base64.Length % 4) % 4;
            base64 += new string('=', paddingNeeded);
            var bytes = Convert.FromBase64String(base64);

            using var stream = new MemoryStream(bytes);
            var mimeMessage = MimeMessage.Load(stream);

            var body = ExtractBody(mimeMessage);
            var from = mimeMessage.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty;
            var to = mimeMessage.To.Mailboxes.Select(m => m.Address).ToArray();
            var date = mimeMessage.Date.UtcDateTime;
            var subject = mimeMessage.Subject ?? string.Empty;

            return new EmailMessage(
                rawMessage.Id,
                subject,
                body,
                from,
                to,
                date,
                SignatureBlock: null,
                Attachments: []);
        }
        catch
        {
            return null;
        }
    }

    internal static string ExtractBody(MimeMessage mimeMessage)
    {
        // Check for top-level plain text part
        if (mimeMessage.Body is TextPart { ContentType.MediaSubtype: "plain" } plainText)
            return plainText.Text ?? string.Empty;

        // Check for top-level HTML part — strip tags
        if (mimeMessage.Body is TextPart { ContentType.MediaSubtype: "html" } htmlText)
            return StripHtml(htmlText.Text ?? string.Empty);

        // For multipart, prefer plain text
        if (mimeMessage.Body is Multipart multipart)
        {
            var plain = FindTextPart(multipart, "plain");
            if (plain is not null)
                return plain.Text ?? string.Empty;

            var html = FindTextPart(multipart, "html");
            if (html is not null)
                return StripHtml(html.Text ?? string.Empty);
        }

        return mimeMessage.TextBody ?? StripHtml(mimeMessage.HtmlBody ?? string.Empty);
    }

    private static TextPart? FindTextPart(Multipart multipart, string subtype)
    {
        foreach (var part in multipart)
        {
            if (part is TextPart tp && tp.ContentType.MediaSubtype.Equals(subtype, StringComparison.OrdinalIgnoreCase))
                return tp;
            if (part is Multipart nested)
            {
                var found = FindTextPart(nested, subtype);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Remove script/style blocks
        var result = System.Text.RegularExpressions.Regex.Replace(
            html, @"<(script|style)[^>]*>.*?</(script|style)>", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        // Remove HTML tags
        result = System.Text.RegularExpressions.Regex.Replace(result, @"<[^>]+>", string.Empty);

        // Decode HTML entities
        result = System.Net.WebUtility.HtmlDecode(result);

        // Collapse whitespace
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s{3,}", "\n\n");

        return result.Trim();
    }
}
