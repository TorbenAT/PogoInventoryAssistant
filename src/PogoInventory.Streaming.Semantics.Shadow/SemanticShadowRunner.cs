using System.Diagnostics;

namespace PogoInventory.Streaming.Semantics.Shadow;

public sealed class SemanticShadowRunner
{
    private readonly ShadowComparisonEngine _comparison = new();

    public async Task<ShadowSessionReport> RunAsync(
        string sessionId,
        IAsyncEnumerable<ShadowFrameInput> frames,
        IReadOnlyList<IShadowSemanticAnalyzer> analyzers,
        IShadowReferenceProvider? referenceProvider = null,
        SemanticShadowOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(analyzers);
        options ??= new SemanticShadowOptions();
        options.Validate();
        if (options.RequireAtLeastOneAnalyzer && analyzers.Count == 0)
            throw new ArgumentException("At least one shadow analyzer is required.", nameof(analyzers));

        var orderedAnalyzers = analyzers.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        if (orderedAnalyzers.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != orderedAnalyzers.Length)
            throw new ArgumentException("Shadow analyzer names must be unique.", nameof(analyzers));

        referenceProvider ??= new EmptyShadowReferenceProvider();
        var started = DateTimeOffset.UtcNow;
        var results = new List<ShadowFrameResult>();
        var timedOut = false;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(options.MaximumDuration);
        using var concurrency = new SemaphoreSlim(options.MaximumAnalyzerConcurrency);

        try
        {
            await foreach (var frame in frames.WithCancellation(linked.Token).ConfigureAwait(false))
            {
                frame.Validate();
                var executionTasks = orderedAnalyzers
                    .Select(analyzer => ExecuteAsync(analyzer, frame, options.AnalyzerTimeout, concurrency, linked.Token))
                    .ToArray();
                var executions = await Task.WhenAll(executionTasks).ConfigureAwait(false);

                IReadOnlyList<ShadowReferenceReading> references;
                string? referenceError = null;
                try
                {
                    references = await referenceProvider.GetReferenceAsync(frame, linked.Token)
                        .AsTask()
                        .WaitAsync(options.AnalyzerTimeout, linked.Token)
                        .ConfigureAwait(false);
                    foreach (var reference in references) reference.Validate();
                }
                catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    references = Array.Empty<ShadowReferenceReading>();
                    referenceError = error.GetType().Name;
                }

                var candidates = executions.SelectMany(x => x.Candidates)
                    .OrderBy(x => x.FieldName, StringComparer.Ordinal)
                    .ThenBy(x => x.Analyzer, StringComparer.Ordinal)
                    .ToArray();
                var comparisons = _comparison.Compare(candidates, references);
                if (referenceError is not null)
                {
                    comparisons = comparisons.Append(
                        new ShadowFieldComparison(
                            "_reference",
                            ShadowComparisonKind.NoKnownCandidate,
                            Array.Empty<string>(),
                            null,
                            new[] { referenceProvider.Name },
                            $"REFERENCE_PROVIDER_{referenceError.ToUpperInvariant()}"))
                        .OrderBy(x => x.FieldName, StringComparer.Ordinal)
                        .ToArray();
                }

                results.Add(new ShadowFrameResult(
                    frame.FrameId,
                    frame.Metadata.Timestamp.CapturedAtUtc,
                    frame.EvidenceHash,
                    frame.Roles.Order(StringComparer.Ordinal).ToArray(),
                    executions.OrderBy(x => x.Analyzer, StringComparer.Ordinal).ToArray(),
                    references.OrderBy(x => x.FieldName, StringComparer.Ordinal).ThenBy(x => x.Provider, StringComparer.Ordinal).ToArray(),
                    comparisons));

                if (results.Count >= options.MaximumFrames)
                    break;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && linked.IsCancellationRequested)
        {
            timedOut = true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var allExecutions = results.SelectMany(x => x.Executions).ToArray();
        var finalStatus = results.Count == 0
            ? (timedOut ? "TimedOutNoFrames" : "NoFrames")
            : (timedOut ? "TimedOutWithFrames" : "Completed");

        return new ShadowSessionReport
        {
            SessionId = sessionId,
            StartedUtc = started,
            EndedUtc = DateTimeOffset.UtcNow,
            FinalStatus = finalStatus,
            Frames = results,
            AnalyzerFaults = allExecutions.Count(x => x.Status == ShadowAnalyzerExecutionStatus.Faulted),
            AnalyzerTimeouts = allExecutions.Count(x => x.Status == ShadowAnalyzerExecutionStatus.TimedOut),
            KnownCandidates = allExecutions.SelectMany(x => x.Candidates).Count(x => x.Status == PogoInventory.Streaming.Semantics.FieldReadingStatus.Known),
            ComparisonConflicts = results.SelectMany(x => x.Comparisons).Count(x =>
                x.Kind is ShadowComparisonKind.AnalyzerConflict or ShadowComparisonKind.ReferenceConflict),
            TimedOut = timedOut
        };
    }

    private static async Task<ShadowAnalyzerExecution> ExecuteAsync(
        IShadowSemanticAnalyzer analyzer,
        ShadowFrameInput frame,
        TimeSpan timeout,
        SemaphoreSlim concurrency,
        CancellationToken cancellationToken)
    {
        await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var analyzerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            analyzerCancellation.CancelAfter(timeout);
            try
            {
                var candidates = await analyzer.AnalyzeAsync(frame, analyzerCancellation.Token)
                    .AsTask()
                    .WaitAsync(analyzerCancellation.Token)
                    .ConfigureAwait(false);
                var normalized = candidates.Select(candidate => candidate with
                    {
                        Analyzer = analyzer.Name,
                        FrameId = frame.FrameId,
                        EvidenceHash = frame.EvidenceHash
                    })
                    .OrderBy(x => x.FieldName, StringComparer.Ordinal)
                    .ToArray();
                foreach (var candidate in normalized) candidate.Validate();
                return new ShadowAnalyzerExecution(
                    analyzer.Name,
                    ShadowAnalyzerExecutionStatus.Completed,
                    stopwatch.Elapsed.TotalMilliseconds,
                    normalized,
                    null);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new ShadowAnalyzerExecution(
                    analyzer.Name,
                    ShadowAnalyzerExecutionStatus.TimedOut,
                    stopwatch.Elapsed.TotalMilliseconds,
                    Array.Empty<ShadowFieldCandidate>(),
                    "ANALYZER_TIMEOUT");
            }
            catch (Exception error)
            {
                return new ShadowAnalyzerExecution(
                    analyzer.Name,
                    ShadowAnalyzerExecutionStatus.Faulted,
                    stopwatch.Elapsed.TotalMilliseconds,
                    Array.Empty<ShadowFieldCandidate>(),
                    $"{error.GetType().Name}: {error.Message}");
            }
        }
        finally
        {
            concurrency.Release();
        }
    }
}
