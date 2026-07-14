using System.Security.Cryptography;

namespace PogoInventory.Verification.Services;

public static class VerificationHash
{
    public static async Task<string> Sha256Async(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
