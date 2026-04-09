namespace RealEstateStar.Domain.Activation.Models;

public sealed record DocumentExtraction(
    string DriveFileId,
    string FileName,
    DocumentType Type,
    IReadOnlyList<ExtractedClient> Clients,
    ExtractedProperty? Property,
    DateTime? Date,
    ExtractedKeyTerms? KeyTerms,
    string? InferredPath = null,
    ExtractedAgentIdentity? AgentIdentity = null,
    string? Language = null,
    string? TransactionStatus = null,
    IReadOnlyList<string>? ServiceAreas = null,
    string? Notes = null);

public sealed record ExtractedAgentIdentity(
    string? Name,
    string? BrokerageName,
    string? LicenseNumber,
    string? Phone,
    string? Email);

public sealed record ExtractedClient(
    string Name,
    ContactRole Role,
    string? Email,
    string? Phone);

public sealed record ExtractedProperty(
    string Address,
    string? City,
    string? State,
    string? Zip);

public sealed record ExtractedKeyTerms(
    string? Price,
    string? Commission,
    IReadOnlyList<string> Contingencies);
