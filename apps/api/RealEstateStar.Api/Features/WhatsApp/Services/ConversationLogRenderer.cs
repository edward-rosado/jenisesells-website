namespace RealEstateStar.Api.Features.WhatsApp.Services;

/// <summary>
/// Pure static utility for rendering WhatsApp conversations as human-readable markdown.
/// Used by ConversationLogger to format messages for storage in Google Drive.
/// </summary>
public static class ConversationLogRenderer
{
    /// <summary>
    /// Renders the markdown header for a WhatsApp conversation log.
    /// </summary>
    /// <param name="leadName">The lead's display name</param>
    /// <param name="submittedDate">When the conversation was submitted</param>
    /// <param name="score">Lead quality score (0-100)</param>
    /// <returns>Formatted markdown header</returns>
    public static string RenderHeader(string leadName, DateTime submittedDate, int score)
    {
        var lines = new[]
        {
            $"# WhatsApp Conversation — {leadName}",
            "",
            $"- **Score:** {score}/100",
            $"- **Submitted:** {submittedDate:yyyy-MM-dd}",
            ""
        };

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Renders a single WhatsApp message with timestamp, sender, and blockquoted body.
    /// </summary>
    /// <param name="timestamp">When the message was sent</param>
    /// <param name="sender">Who sent the message</param>
    /// <param name="body">Message content (may contain newlines)</param>
    /// <param name="templateName">Optional template name (e.g., "new_lead_notification")</param>
    /// <returns>Formatted markdown message</returns>
    public static string RenderMessage(DateTime timestamp, string sender, string body, string? templateName)
    {
        var timeStr = timestamp.ToString("h:mm tt");
        var templateTag = templateName != null ? $" (template: {templateName})" : "";

        var header = $"{timeStr} — {sender}{templateTag}";
        var quotedBody = QuoteLines(body);

        return $"{header}\n{quotedBody}";
    }

    /// <summary>
    /// Renders a date separator header.
    /// </summary>
    /// <param name="date">The date to format</param>
    /// <returns>Formatted markdown date header</returns>
    public static string RenderDateHeader(DateTime date)
    {
        return $"### {date:MMM dd, yyyy}";
    }

    /// <summary>
    /// Prefixes each line of text with "> " for markdown blockquote.
    /// </summary>
    private static string QuoteLines(string text)
    {
        var lines = text.Split('\n');
        var quotedLines = lines.Select(line => $"> {line}");
        return string.Join("\n", quotedLines);
    }
}
