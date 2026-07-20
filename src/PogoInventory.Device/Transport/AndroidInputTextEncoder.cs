using System.Text;

namespace PogoInventory.Device.Transport;

/// <summary>
/// Encodes ordinary Pokémon GO search text for the remote Android shell used by
/// <c>adb shell input text</c>. Callers never pass shell syntax.
/// </summary>
public static class AndroidInputTextEncoder
{
    public const int MaximumLength = 100;

    public static string EncodeInventorySearchQuery(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (query.Length > MaximumLength || query.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Inventory search text must contain 1-{MaximumLength} non-control characters.",
                nameof(query));
        }

        var value = query.Trim();
        if (value.Length == 0)
        {
            throw new ArgumentException("Inventory search text cannot be empty.", nameof(query));
        }

        var encoded = new StringBuilder(value.Length * 2);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == ' ')
            {
                encoded.Append("%s");
                continue;
            }

            if (character == '#' && index == 0)
            {
                encoded.Append("%s#");
                continue;
            }

            if (character == '#')
            {
                encoded.Append("\\#");
                continue;
            }

            if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or '!' or ':')
            {
                encoded.Append(character);
                continue;
            }

            encoded.Append('\\').Append(character);
        }

        return encoded.ToString();
    }
}
