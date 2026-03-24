using System.Diagnostics;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Microsoft.Extensions.Logging;
using MimeKit;
using RealEstateStar.Clients.GoogleOAuth;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Gmail;

internal sealed class GmailApiClient(
    IOAuthRefresher refresher,
    ILogger<GmailApiClient> logger) : IGmailSender
{
    public async Task SendAsync(
        string accountId,
        string agentId,
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct)
    {
        var credential = await refresher.GetValidCredentialAsync(accountId, agentId, ct);
        if (credential is null)
        {
            logger.LogWarning(
                "[GMAIL-010] No valid token for account {AccountId}, agent {AgentId}. Skipping send.",
                accountId, agentId);
            GmailDiagnostics.TokenMissing.Add(1);
            return;
        }

        var message = BuildMimeMessage(credential.Email, to, subject, htmlBody);
        await SendMessageAsync(credential, message, accountId, agentId, ct);
    }

    public async Task SendWithAttachmentAsync(
        string accountId,
        string agentId,
        string to,
        string subject,
        string htmlBody,
        byte[] attachmentBytes,
        string fileName,
        CancellationToken ct)
    {
        var credential = await refresher.GetValidCredentialAsync(accountId, agentId, ct);
        if (credential is null)
        {
            logger.LogWarning(
                "[GMAIL-010] No valid token for account {AccountId}, agent {AgentId}. Skipping send with attachment.",
                accountId, agentId);
            GmailDiagnostics.TokenMissing.Add(1);
            return;
        }

        var message = BuildMimeMessage(credential.Email, to, subject, htmlBody, attachmentBytes, fileName);
        await SendMessageAsync(credential, message, accountId, agentId, ct);
    }

    private async Task SendMessageAsync(
        Domain.Shared.Models.OAuthCredential credential,
        MimeMessage mimeMessage,
        string accountId,
        string agentId,
        CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();

        using var activity = GmailDiagnostics.ActivitySource.StartActivity("gmail.send");
        activity?.SetTag("gmail.account_id", accountId);

        try
        {
            var gmailService = BuildGmailService(credential);
            var encoded = EncodeMessage(mimeMessage);
            var request = gmailService.Users.Messages.Send(encoded, "me");
            await request.ExecuteAsync(ct);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GmailDiagnostics.Sent.Add(1);
            GmailDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GMAIL-001] Email sent for account {AccountId}, agent {AgentId}. Duration: {Duration}ms",
                accountId, agentId, durationMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GmailDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GMAIL-030] Gmail send failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    internal static MimeMessage BuildMimeMessage(
        string from,
        string to,
        string subject,
        string htmlBody,
        byte[]? attachmentBytes = null,
        string? fileName = null)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };

        if (attachmentBytes is not null && fileName is not null)
        {
            bodyBuilder.Attachments.Add(fileName, attachmentBytes);
        }

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    internal static Message EncodeMessage(MimeMessage mimeMessage)
    {
        using var stream = new MemoryStream();
        mimeMessage.WriteTo(stream);
        var raw = Convert.ToBase64String(stream.ToArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new Message { Raw = raw };
    }

    private static GmailService BuildGmailService(Domain.Shared.Models.OAuthCredential credential) =>
        new(GoogleCredentialFactory.BuildInitializer(credential));
}
