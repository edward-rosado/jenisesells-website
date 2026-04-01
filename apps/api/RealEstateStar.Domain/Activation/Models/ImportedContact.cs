namespace RealEstateStar.Domain.Activation.Models;

public sealed record ImportedContact(
    string Name,
    string? Email,
    string? Phone,
    ContactRole Role,
    PipelineStage Stage,
    string? PropertyAddress,
    IReadOnlyList<DocumentReference> Documents);

public sealed record DocumentReference(
    string DriveFileId,
    string FileName,
    DocumentType Type,
    DateTime? Date);
