namespace RealEstateStar.Domain.Cma.Interfaces;

public interface IImageResolver
{
    Task<byte[]?> ResolveAsync(string handle, string relativePath, CancellationToken ct);
}
