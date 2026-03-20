namespace RealEstateStar.Api.Features.Leads;

public record DriveActivityEvent(
    string Action,
    string FileName,
    string FolderPath,
    string? DestinationParent,
    DateTime Timestamp);
