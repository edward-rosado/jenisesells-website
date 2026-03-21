using RealEstateStar.DataServices.Leads;
using RealEstateStar.Domain.Privacy.Interfaces;

namespace RealEstateStar.DataServices.Privacy;

/// <summary>
/// Compliance storage backed by the local file system.
/// Used in development/testing when a service-account Drive is not configured.
/// </summary>
public sealed class LocalComplianceStorageProvider(string basePath) : LocalStorageProvider(basePath), IComplianceFileStorageProvider
{
}
