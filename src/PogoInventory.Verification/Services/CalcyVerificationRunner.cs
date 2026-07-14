using System.Text.Json;
using PogoInventory.Observations.Models;
using PogoInventory.Observations.Parsing;
using PogoInventory.Verification.Models;
using PogoInventory.Verification.Reporting;

namespace PogoInventory.Verification.Services;

public sealed class CalcyVerificationRunner
{
    private readonly CalcyRawTextParser _parser;

    public CalcyVerificationRunner(CalcyRawTextParser? parser = null)
    {
        _parser = parser ?? new CalcyRawTextParser();
    }

    public async Task<CalcyVerificationReport> RunAsync(
        string manifestPath,
        string evidenceRoot,
        string outputDirectory,
        CalcyTextParserProfile? parserProfile = null,
        CancellationToken cancellationToken = default)
    {
        var manifest = await CalcyVerificationManifestLoader.LoadForRunAsync(
            manifestPath,
            cancellationToken);
        var manifestHash = await VerificationHash.Sha256Async(
            manifestPath,
            cancellationToken);
        ValidateEvidencePaths(manifest, evidenceRoot);
        var results = new List<CalcyVerificationCaseResult>();

        foreach (var item in manifest.Cases)
        {
            results.Add(await EvaluateAsync(
                item,
                evidenceRoot,
                parserProfile,
                cancellationToken));
        }

        var exact = results.Count(x => x.Outcome == CalcyVerificationOutcome.ExactComplete);
        var safeIncomplete = results.Count(x => x.Outcome == CalcyVerificationOutcome.SafeIncomplete);
        var incorrectIncomplete = results.Count(x => x.Outcome == CalcyVerificationOutcome.IncorrectIncomplete);
        var wrongComplete = results.Count(x => x.Outcome == CalcyVerificationOutcome.WrongComplete);
        var conflicting = results.Count(x => x.Outcome == CalcyVerificationOutcome.Conflicting);
        var failed = results.Count(x => x.Outcome == CalcyVerificationOutcome.Failed);
        var unavailable = results.Count(x => x.Outcome == CalcyVerificationOutcome.Unavailable);
        var invalid = results.Count(x => x.Outcome == CalcyVerificationOutcome.InvalidEvidence);
        var rate = results.Count == 0 ? 0 : exact / (double)results.Count;
        var zeroFalseComplete = wrongComplete == 0;
        var safe = results.Count >= manifest.MinimumCases && zeroFalseComplete && invalid == 0;
        var recommended = safe &&
            rate >= manifest.MinimumExactCompleteRate &&
            incorrectIncomplete == 0 &&
            conflicting == 0 &&
            failed == 0;

        var detail = BuildGateDetail(
            manifest,
            results.Count,
            rate,
            wrongComplete,
            incorrectIncomplete,
            conflicting,
            failed,
            invalid,
            recommended);

        var report = new CalcyVerificationReport
        {
            ManifestName = manifest.Name,
            ManifestSha256 = manifestHash,
            Mechanism = manifest.Mechanism,
            ProviderVersion = manifest.ProviderVersion,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            MinimumCases = manifest.MinimumCases,
            MinimumExactCompleteRate = manifest.MinimumExactCompleteRate,
            CaseCount = results.Count,
            ExactCompleteCount = exact,
            SafeIncompleteCount = safeIncomplete,
            IncorrectIncompleteCount = incorrectIncomplete,
            WrongCompleteCount = wrongComplete,
            ConflictingCount = conflicting,
            FailedCount = failed,
            UnavailableCount = unavailable,
            InvalidEvidenceCount = invalid,
            ExactCompleteRate = rate,
            ZeroFalseComplete = zeroFalseComplete,
            SafeForLongScan = safe,
            RecommendedForLongScan = recommended,
            GateDetail = detail,
            Cases = results
        };
        report.Validate();
        await CalcyVerificationReportWriter.WriteAsync(
            report,
            outputDirectory,
            cancellationToken);
        return report;
    }


    private static void ValidateEvidencePaths(
        CalcyVerificationManifest manifest,
        string evidenceRoot)
    {
        foreach (var item in manifest.Cases)
        {
            if (!string.IsNullOrWhiteSpace(item.ObservationPath))
            {
                _ = VerificationPathSafety.ResolveInside(evidenceRoot, item.ObservationPath);
            }
            foreach (var source in item.Sources.Values)
            {
                _ = VerificationPathSafety.ResolveInside(evidenceRoot, source);
            }
        }
    }

    private async Task<CalcyVerificationCaseResult> EvaluateAsync(
        CalcyVerificationCase item,
        string evidenceRoot,
        CalcyTextParserProfile? parserProfile,
        CancellationToken cancellationToken)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            CalcyObservation observation;
            if (!string.IsNullOrWhiteSpace(item.ObservationPath))
            {
                var path = VerificationPathSafety.ResolveInside(
                    evidenceRoot,
                    item.ObservationPath);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("Observation evidence file was not found.", path);
                }

                hashes["observation"] = await VerificationHash.Sha256Async(path, cancellationToken);
                observation = JsonSerializer.Deserialize<CalcyObservation>(
                    await File.ReadAllTextAsync(path, cancellationToken),
                    VerificationJson.CreateOptions()) ?? throw new InvalidOperationException(
                        "Observation evidence contained no data.");
                observation.Validate();
            }
            else
            {
                if (parserProfile is null)
                {
                    throw new InvalidOperationException(
                        "A parser profile is required for verification cases that contain raw sources.");
                }

                var sources = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var source in item.Sources)
                {
                    var path = VerificationPathSafety.ResolveInside(evidenceRoot, source.Value);
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException(
                            $"Raw evidence source '{source.Key}' was not found.",
                            path);
                    }

                    hashes[source.Key] = await VerificationHash.Sha256Async(path, cancellationToken);
                    sources[source.Key] = await File.ReadAllTextAsync(path, cancellationToken);
                }

                observation = _parser.Parse(
                    parserProfile,
                    new CalcyRawOutputBundle { Sources = sources },
                    $"Verification:{item.Id}");
            }

            return Compare(item, observation, hashes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new CalcyVerificationCaseResult
            {
                Id = item.Id,
                Expected = item.Expected,
                Outcome = CalcyVerificationOutcome.InvalidEvidence,
                EvidenceSha256 = hashes,
                ErrorDetail = exception.Message
            };
        }
    }

    private static CalcyVerificationCaseResult Compare(
        CalcyVerificationCase item,
        CalcyObservation observation,
        IReadOnlyDictionary<string, string> hashes)
    {
        var mismatches = CompareCore(item.Expected, observation);
        var outcome = observation.Status switch
        {
            CalcyObservationStatus.Complete => mismatches.Count == 0
                ? CalcyVerificationOutcome.ExactComplete
                : CalcyVerificationOutcome.WrongComplete,
            CalcyObservationStatus.Partial => mismatches.Count == 0
                ? CalcyVerificationOutcome.SafeIncomplete
                : CalcyVerificationOutcome.IncorrectIncomplete,
            CalcyObservationStatus.Conflicting => CalcyVerificationOutcome.Conflicting,
            CalcyObservationStatus.Failed => CalcyVerificationOutcome.Failed,
            CalcyObservationStatus.Unavailable => CalcyVerificationOutcome.Unavailable,
            _ => CalcyVerificationOutcome.InvalidEvidence
        };

        return new CalcyVerificationCaseResult
        {
            Id = item.Id,
            Expected = item.Expected,
            Observed = observation,
            Outcome = outcome,
            Mismatches = mismatches,
            EvidenceSha256 = hashes,
            ErrorDetail = observation.ErrorDetail
        };
    }

    private static IReadOnlyList<string> CompareCore(
        ExpectedPokemonObservation expected,
        CalcyObservation observed)
    {
        var mismatches = new List<string>();
        var identityCompared = false;
        var identityMatched = false;
        if (expected.PokedexNumber is not null && observed.PokedexNumber is not null)
        {
            identityCompared = true;
            identityMatched = expected.PokedexNumber == observed.PokedexNumber;
        }
        else if (!string.IsNullOrWhiteSpace(expected.Species) &&
                 !string.IsNullOrWhiteSpace(observed.Species))
        {
            identityCompared = true;
            identityMatched = string.Equals(
                expected.Species.Trim(),
                observed.Species.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        if (!identityCompared)
        {
            mismatches.Add("Identity is missing from the observation.");
        }
        else if (!identityMatched)
        {
            mismatches.Add(
                $"Identity expected '{expected.Species ?? expected.PokedexNumber?.ToString()}', " +
                $"observed '{observed.Species ?? observed.PokedexNumber?.ToString()}'.");
        }

        CompareValue("CP", expected.Cp, observed.Cp, mismatches);
        CompareValue("Attack IV", expected.AttackIv, observed.AttackIv, mismatches);
        CompareValue("Defense IV", expected.DefenseIv, observed.DefenseIv, mismatches);
        CompareValue("HP IV", expected.HpIv, observed.HpIv, mismatches);
        return mismatches;
    }

    private static void CompareValue(
        string name,
        int? expected,
        int? observed,
        ICollection<string> mismatches)
    {
        if (observed is null)
        {
            return;
        }

        if (expected != observed)
        {
            mismatches.Add($"{name} expected {expected}, observed {observed}.");
        }
    }

    private static string BuildGateDetail(
        CalcyVerificationManifest manifest,
        int count,
        double rate,
        int wrongComplete,
        int incorrectIncomplete,
        int conflicting,
        int failed,
        int invalid,
        bool recommended)
    {
        if (recommended)
        {
            return "Provider passed the long-scan gate.";
        }

        var reasons = new List<string>();
        if (count < manifest.MinimumCases)
        {
            reasons.Add($"Only {count} of {manifest.MinimumCases} required cases were verified");
        }
        if (wrongComplete > 0)
        {
            reasons.Add($"{wrongComplete} false Complete observation(s) were found");
        }
        if (rate < manifest.MinimumExactCompleteRate)
        {
            reasons.Add(
                $"Exact Complete rate {rate:P1} is below {manifest.MinimumExactCompleteRate:P1}");
        }
        if (incorrectIncomplete > 0)
        {
            reasons.Add($"{incorrectIncomplete} incomplete observation(s) contained wrong values");
        }
        if (conflicting > 0)
        {
            reasons.Add($"{conflicting} conflicting observation(s) were found");
        }
        if (failed > 0)
        {
            reasons.Add($"{failed} provider failure(s) were found");
        }
        if (invalid > 0)
        {
            reasons.Add($"{invalid} evidence file(s) were invalid or missing");
        }
        return reasons.Count == 0
            ? "Provider did not meet the long-scan gate."
            : string.Join("; ", reasons) + ".";
    }
}
