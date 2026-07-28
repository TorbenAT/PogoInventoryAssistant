using System.Globalization;
using System.Text.Json;
using PogoInventory.Streaming.Semantics;

namespace PogoInventory.Streaming.Semantics.Shadow;

public sealed class SemanticFieldAnalyzerAdapter<T> : IShadowSemanticAnalyzer
{
    private readonly ISemanticFieldAnalyzer<T> _inner;
    private readonly Func<T?, string?> _formatter;

    public SemanticFieldAnalyzerAdapter(
        ISemanticFieldAnalyzer<T> inner,
        Func<T?, string?>? formatter = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _formatter = formatter ?? DefaultFormat;
    }

    public string Name => _inner.Method;

    public ValueTask<IReadOnlyList<ShadowFieldCandidate>> AnalyzeAsync(
        ShadowFrameInput frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        cancellationToken.ThrowIfCancellationRequested();
        var evidence = _inner.Analyze(frame.SemanticObservation);
        var candidate = new ShadowFieldCandidate(
            Name,
            evidence.FieldName,
            evidence.Status,
            evidence.Value is null ? null : _formatter(evidence.Value),
            evidence.Confidence,
            $"SEMANTIC_{evidence.Status.ToString().ToUpperInvariant()}",
            frame.FrameId,
            frame.EvidenceHash,
            new Dictionary<string, string>
            {
                ["adapter"] = nameof(SemanticFieldAnalyzerAdapter<T>),
                ["sourceFrameId"] = evidence.FrameId.ToString(CultureInfo.InvariantCulture)
            });
        candidate.Validate();
        return ValueTask.FromResult<IReadOnlyList<ShadowFieldCandidate>>(new[] { candidate });
    }

    private static string? DefaultFormat(T? value)
    {
        if (value is null) return null;
        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : JsonSerializer.Serialize(value);
    }
}

public sealed class DelegateShadowAnalyzer : IShadowSemanticAnalyzer
{
    private readonly Func<ShadowFrameInput, CancellationToken, ValueTask<IReadOnlyList<ShadowFieldCandidate>>> _analyze;

    public DelegateShadowAnalyzer(
        string name,
        Func<ShadowFrameInput, CancellationToken, ValueTask<IReadOnlyList<ShadowFieldCandidate>>> analyze)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Analyzer name is required.", nameof(name))
            : name;
        _analyze = analyze ?? throw new ArgumentNullException(nameof(analyze));
    }

    public string Name { get; }

    public ValueTask<IReadOnlyList<ShadowFieldCandidate>> AnalyzeAsync(
        ShadowFrameInput frame,
        CancellationToken cancellationToken = default) =>
        _analyze(frame, cancellationToken);
}

public sealed class EmptyShadowReferenceProvider : IShadowReferenceProvider
{
    public string Name => "none";

    public ValueTask<IReadOnlyList<ShadowReferenceReading>> GetReferenceAsync(
        ShadowFrameInput frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<ShadowReferenceReading>>(Array.Empty<ShadowReferenceReading>());
    }
}
