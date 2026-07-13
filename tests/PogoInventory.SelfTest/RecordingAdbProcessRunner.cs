using PogoInventory.Device.Adb;

internal sealed class RecordingAdbProcessRunner : IAdbProcessRunner
{
    private readonly Queue<AdbProcessResult> _results;

    public RecordingAdbProcessRunner(IEnumerable<AdbProcessResult> results)
    {
        _results = new Queue<AdbProcessResult>(
            results ?? throw new ArgumentNullException(nameof(results)));
    }

    public List<IReadOnlyList<string>> Commands { get; } = new();

    public Task<AdbProcessResult> ExecuteAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Commands.Add(arguments.ToArray());

        if (_results.Count == 0)
        {
            throw new InvalidOperationException("No fake ADB result remained for the requested command.");
        }

        return Task.FromResult(_results.Dequeue());
    }
}
