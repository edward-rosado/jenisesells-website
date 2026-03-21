namespace RealEstateStar.Domain.Shared.Models;

public record DriveActivityEvent(
    string Action,
    string FileName,
    string FolderPath,
    string? DestinationParent,
    DateTime Timestamp);
