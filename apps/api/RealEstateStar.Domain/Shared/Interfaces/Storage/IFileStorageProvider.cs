namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

/// <summary>
/// Combined storage interface extending both document and sheet capabilities.
/// New code should depend on IDocumentStorageProvider or ISheetStorageProvider
/// depending on which capability is needed. IFileStorageProvider remains for
/// backward compatibility with existing implementations.
/// </summary>
public interface IFileStorageProvider : IDocumentStorageProvider, ISheetStorageProvider { }
