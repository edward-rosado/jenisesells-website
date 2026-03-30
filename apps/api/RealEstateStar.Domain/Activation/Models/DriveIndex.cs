namespace RealEstateStar.Domain.Activation.Models;

public sealed record DriveIndex(
    string FolderId,
    IReadOnlyList<DriveFile> Files,
    IReadOnlyDictionary<string, string> Contents,
    IReadOnlyList<string> DiscoveredUrls);

public sealed record DriveFile(
    string Id,
    string Name,
    string MimeType,
    string Category,
    DateTime ModifiedDate);
