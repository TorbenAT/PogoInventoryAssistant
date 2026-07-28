namespace PogoInventory.Semantics;

public static class SemanticConsensus
{
    public static bool TryResolve<T>(IEnumerable<T> values, out T result, int minimumAgreement = 2)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (minimumAgreement < 2) throw new ArgumentOutOfRangeException(nameof(minimumAgreement));
        var groups = values.GroupBy(value => value).OrderByDescending(group => group.Count()).ToArray();
        if (groups.Length == 0 || groups[0].Count() < minimumAgreement || groups.Skip(1).Any(group => group.Count() == groups[0].Count()))
        {
            result = default!;
            return false;
        }
        result = groups[0].Key;
        return true;
    }
}
