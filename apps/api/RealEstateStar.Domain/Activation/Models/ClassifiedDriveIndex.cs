using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RealEstateStar.Domain.Activation.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FileCategory
{
    PropertyPhoto,
    PropertyDocument,
    Marketing,
    Cma,
    Branding,
    Contract,
    Disclosure,
    Personal,
    Other
}

public sealed record ClassifiedFile(
    string Id,
    string Name,
    string MimeType,
    string FolderPath,
    FileCategory Category,
    string? PropertyAddress,
    string Confidence);

public sealed record PropertyGroup(
    string Address,
    string Slug,
    IReadOnlyList<ClassifiedFile> Photos,
    IReadOnlyList<ClassifiedFile> Documents);

public sealed record ClassifiedDriveIndex(
    string FolderId,
    IReadOnlyList<ClassifiedFile> Files,
    IReadOnlyDictionary<string, string> Contents,
    IReadOnlyList<string> DiscoveredUrls,
    IReadOnlyList<PropertyGroup> Properties)
{
    public static string ToSlug(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return string.Empty;

        var lower = address.Trim().ToLowerInvariant();
        var cleaned = Regex.Replace(lower, @"[^a-z0-9\s-]", "");
        var hyphenated = Regex.Replace(cleaned, @"[\s]+", "-");
        var collapsed = Regex.Replace(hyphenated, @"-{2,}", "-");
        return collapsed.Trim('-');
    }

    public static IReadOnlyList<PropertyGroup> ComputePropertyGroups(
        IReadOnlyList<ClassifiedFile> files)
    {
        var propertyFiles = files
            .Where(f => f.PropertyAddress is not null &&
                        (f.Category == FileCategory.PropertyPhoto ||
                         f.Category == FileCategory.PropertyDocument))
            .ToList();

        return propertyFiles
            .GroupBy(f => f.PropertyAddress!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PropertyGroup(
                Address: g.Key,
                Slug: ToSlug(g.Key),
                Photos: g.Where(f => f.Category == FileCategory.PropertyPhoto).ToList(),
                Documents: g.Where(f => f.Category == FileCategory.PropertyDocument).ToList()))
            .ToList();
    }
}
