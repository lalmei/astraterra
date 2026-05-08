using System.Security.Cryptography;
using System.Text;

namespace AstraTerra.Constellations;

public static class WorldStorageKeyResolver
{
    public static string Resolve(string worldIdentifier)
    {
        if (string.IsNullOrWhiteSpace(worldIdentifier))
        {
            throw new ArgumentException("World identifier is required.", nameof(worldIdentifier));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(worldIdentifier));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }
}
