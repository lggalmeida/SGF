using System.Security.Cryptography;
using System.Text;

namespace Sgf.Infrastructure.Identity.Authentication;

public sealed class RefreshTokenGenerator
{
    public string CreateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
