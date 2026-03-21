using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Domain.Privacy.Interfaces;

/// <summary>Marker interface for service-account-backed file storage (agent cannot access).</summary>
public interface IComplianceFileStorageProvider : IFileStorageProvider { }
