using System.Text.Json;

namespace RealEstateStar.Api.Features.Leads.Services;

/// <summary>
/// Parses JSON output from `gws drive activity --format json` into DriveActivityEvent list.
/// </summary>
public static class DriveActivityParser
{
    /// <summary>
    /// Parses a JSON array from gws CLI output into a list of DriveActivityEvent records.
    /// Expected shape per element:
    /// {
    ///   "action": "Move" | "Create" | "Edit" | "Delete" | "Rename",
    ///   "fileName": "Lead Profile.md",
    ///   "folderPath": "Real Estate Star/1 - Leads/Jane Doe",
    ///   "destinationParent": "Real Estate Star/2 - Active Clients/Jane Doe",  // Move only
    ///   "timestamp": "2026-03-19T12:00:00Z"
    /// }
    /// </summary>
    public static List<DriveActivityEvent> Parse(string gwsJsonOutput)
    {
        if (string.IsNullOrWhiteSpace(gwsJsonOutput))
            return [];

        using var doc = JsonDocument.Parse(gwsJsonOutput);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var events = new List<DriveActivityEvent>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var action = element.TryGetProperty("action", out var actionProp)
                ? MapAction(actionProp.GetString() ?? "")
                : "Unknown";

            var fileName = element.TryGetProperty("fileName", out var fileProp)
                ? fileProp.GetString() ?? ""
                : "";

            var folderPath = element.TryGetProperty("folderPath", out var folderProp)
                ? folderProp.GetString() ?? ""
                : "";

            string? destinationParent = null;
            if (element.TryGetProperty("destinationParent", out var destProp) &&
                destProp.ValueKind != JsonValueKind.Null)
            {
                destinationParent = destProp.GetString();
            }

            DateTime timestamp = DateTime.UtcNow;
            if (element.TryGetProperty("timestamp", out var tsProp))
            {
                DateTime.TryParse(
                    tsProp.GetString(),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out timestamp);
            }

            events.Add(new DriveActivityEvent(action, fileName, folderPath, destinationParent, timestamp));
        }

        return events;
    }

    private static string MapAction(string raw) => raw.ToLowerInvariant() switch
    {
        "move" => "Move",
        "create" => "Create",
        "edit" => "Edit",
        "delete" => "Delete",
        "rename" => "Rename",
        _ => raw
    };
}
