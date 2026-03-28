using System.Security.Cryptography;
using System.Text;

namespace RealEstateStar.Domain.Shared;

public static class EmailHasher
{
    public static string Hash(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant().Trim()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
