namespace PogoInventory.Automation.Models;

public sealed record NormalizedSwipe
{
    public required NormalizedPoint Start { get; init; }
    public required NormalizedPoint End { get; init; }
    public int DurationMilliseconds { get; init; } = 320;

    public void Validate(string name)
    {
        ArgumentNullException.ThrowIfNull(Start);
        ArgumentNullException.ThrowIfNull(End);
        Start.Validate($"{name}.start");
        End.Validate($"{name}.end");

        if (DurationMilliseconds is < 50 or > 5000)
        {
            throw new ArgumentOutOfRangeException(
                name,
                "Swipe duration must be between 50 and 5000 milliseconds.");
        }

        if (Math.Abs(Start.X - End.X) < 0.05 &&
            Math.Abs(Start.Y - End.Y) < 0.05)
        {
            throw new ArgumentException(
                "Swipe start and end must be meaningfully different.",
                name);
        }
    }
}
