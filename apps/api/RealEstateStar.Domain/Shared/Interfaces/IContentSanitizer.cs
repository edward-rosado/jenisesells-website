namespace RealEstateStar.Domain.Shared.Interfaces;

public interface IContentSanitizer
{
    string Sanitize(string untrustedContent);
}
