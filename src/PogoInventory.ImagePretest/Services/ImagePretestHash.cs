using System.Security.Cryptography;

namespace PogoInventory.ImagePretest.Services;

public static class ImagePretestHash
{
    public static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
