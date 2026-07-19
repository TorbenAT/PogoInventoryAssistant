namespace PogoInventory.Automation.Models;

public static class InventorySearchQuery
{
    public const string Unindexed = "!#AI-Indexed";
    public const int MaximumLength = 100;

    public static string Validate(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (query.Length > MaximumLength)
        {
            throw new InvalidOperationException($"Search query cannot exceed {MaximumLength} characters.");
        }

        if (query.Any(char.IsControl))
        {
            throw new InvalidOperationException("Search query cannot contain control characters.");
        }

        return query.Trim();
    }
}
