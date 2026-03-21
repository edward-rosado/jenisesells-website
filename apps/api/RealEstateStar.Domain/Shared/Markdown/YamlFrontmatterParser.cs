namespace RealEstateStar.Domain.Shared.Markdown;

/// <summary>
/// Parses and mutates simple YAML frontmatter blocks in Markdown files.
/// Intentionally avoids YamlDotNet — the frontmatter format is simple enough
/// to handle with string parsing and adding a dependency is not worth it.
/// </summary>
public static class YamlFrontmatterParser
{
    private const string Fence = "---";

    /// <summary>
    /// Extracts key-value pairs from YAML frontmatter delimited by <c>---</c> fences.
    /// Returns an empty dictionary when no frontmatter is found.
    /// </summary>
    public static Dictionary<string, string> Parse(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!TryExtractFrontmatterLines(content, out var lines))
            return result;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip blank lines and YAML comments
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = trimmed[..colonIndex].Trim();
            var value = trimmed[(colonIndex + 1)..].Trim();

            value = Unquote(value);

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// Replaces <paramref name="key"/>'s value inside the frontmatter block.
    /// If the key does not exist it is appended before the closing <c>---</c>.
    /// The markdown body is never modified.
    /// </summary>
    public static string UpdateField(string content, string key, string value)
    {
        if (!TryLocateFences(content, out var firstFenceEnd, out var secondFenceStart))
            return content;

        // The region between the two fences (exclusive of the fence lines themselves)
        var frontmatterStart = firstFenceEnd;
        var frontmatterEnd = secondFenceStart;
        var frontmatter = content[frontmatterStart..frontmatterEnd];

        var lines = frontmatter.Split('\n');
        var replaced = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0) continue;

            var existingKey = trimmed[..colonIndex].Trim();
            if (!string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
                continue;

            // Preserve leading whitespace of the original line
            var leadingWhitespace = lines[i][..(lines[i].Length - lines[i].TrimStart().Length)];
            lines[i] = $"{leadingWhitespace}{key}: {value}";
            replaced = true;
            break;
        }

        string newFrontmatter;
        if (replaced)
        {
            newFrontmatter = string.Join('\n', lines);
        }
        else
        {
            // Append before the closing fence — insert the new field as a new line
            // at the end of the frontmatter block, keeping any trailing newline intact.
            var trimmedFm = frontmatter.TrimEnd('\n', '\r');
            newFrontmatter = trimmedFm + $"\n{key}: {value}\n";
        }

        return content[..frontmatterStart] + newFrontmatter + content[frontmatterEnd..];
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static bool TryExtractFrontmatterLines(string content, out string[] lines)
    {
        lines = [];

        if (!TryLocateFences(content, out var fmStart, out var fmEnd))
            return false;

        var frontmatter = content[fmStart..fmEnd];
        lines = frontmatter.Split('\n');
        return true;
    }

    /// <summary>
    /// Locates the two <c>---</c> fences in <paramref name="content"/>.
    /// On success, <paramref name="frontmatterStart"/> is the index immediately
    /// after the newline that follows the opening fence, and
    /// <paramref name="frontmatterEnd"/> is the index of the closing fence line.
    /// </summary>
    private static bool TryLocateFences(
        string content,
        out int frontmatterStart,
        out int frontmatterEnd)
    {
        frontmatterStart = 0;
        frontmatterEnd = 0;

        // Opening fence must be the very first line
        var firstNewline = content.IndexOf('\n');
        if (firstNewline < 0)
            return false;

        var firstLine = content[..firstNewline].Trim();
        if (firstLine != Fence)
            return false;

        frontmatterStart = firstNewline + 1;

        // Find the closing fence
        var closingFenceIndex = content.IndexOf('\n' + Fence, frontmatterStart);
        if (closingFenceIndex < 0)
            return false;

        frontmatterEnd = closingFenceIndex + 1; // points to the start of the closing '---' line
        return true;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                return value[1..^1];
            }
        }

        return value;
    }
}
