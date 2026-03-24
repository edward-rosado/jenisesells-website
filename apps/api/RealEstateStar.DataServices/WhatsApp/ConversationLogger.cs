using RealEstateStar.Domain.WhatsApp.Interfaces;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.WhatsApp;

public class ConversationLogger(
    IDocumentStorageProvider storage,
    ILogger<ConversationLogger> logger) : IConversationLogger
{
    private const string FileName = "conversation.md";

    public async Task LogMessagesAsync(string agentId, string? leadName,
        List<(DateTime timestamp, string sender, string body, string? templateName)> messages,
        CancellationToken ct)
    {
        var folder = ResolveFolder(leadName);

        try
        {
            var existing = await storage.ReadDocumentAsync(folder, FileName, ct);

            var sections = new List<string>();

            if (existing is null)
            {
                var displayName = leadName ?? "General";
                sections.Add(ConversationLogRenderer.RenderHeader(displayName, DateTime.UtcNow, 0));
            }
            else
            {
                sections.Add(existing);
            }

            DateTime? lastDate = null;

            foreach (var (ts, sender, body, templateName) in messages)
            {
                if (lastDate is null || ts.Date != lastDate.Value.Date)
                {
                    sections.Add(ConversationLogRenderer.RenderDateHeader(ts));
                    lastDate = ts;
                }

                sections.Add(ConversationLogRenderer.RenderMessage(ts, sender, body, templateName));
            }

            var content = string.Join("\n\n", sections);

            await storage.WriteDocumentAsync(folder, FileName, content, ct);

            logger.LogInformation("[WA-010] Conversation log updated for agent {AgentId} lead {LeadName}",
                agentId, leadName ?? "general");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[WA-011] Drive write failed for agent {AgentId} lead {LeadName}",
                agentId, leadName ?? "general");
        }
    }

    private static string ResolveFolder(string? leadName)
    {
        if (leadName is not null)
        {
            // LeadConversation returns "folder/file.md" — extract the directory portion
            var fullPath = WhatsAppPaths.LeadConversation(leadName);
            return fullPath[..fullPath.LastIndexOf('/')];
        }

        // GeneralConversation = "Real Estate Star/WhatsApp/General.md" — extract directory
        var generalPath = WhatsAppPaths.GeneralConversation;
        return generalPath[..generalPath.LastIndexOf('/')];
    }
}
