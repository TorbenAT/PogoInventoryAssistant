using System.Security.Cryptography;
using System.Text;

namespace PogoInventory.CalcyProbe.Services;

public static class ProbeHash
{
    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)))
            .ToLowerInvariant();
}
