using PogoInventory.Semantics;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Semantics;

namespace PogoInventory.SelfTest;

internal static class CanonicalSemanticCoreTests
{
    public static Task PixelBridgePreservesChannelsAndStrideAsync()
    {
        var source = new byte[] { 10, 20, 30, 40, 1, 2, 3, 4, 99, 99, 99, 99 };
        var rgba = BgraPixelBridge.ToTightlyPackedRgba32(source, 2, 1, 12);
        Assert(rgba.SequenceEqual(new byte[] { 30, 20, 10, 40, 3, 2, 1, 4 }), "BGRA-to-RGBA conversion or stride handling failed.");
        return Task.CompletedTask;
    }

    public static Task FreshnessBarrierRejectsStaleAndWrongStateAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var barrier = new FrameBarrier(10, now.AddSeconds(-2), TimeSpan.FromSeconds(3), "AppraisalBars");
        var fresh = Metadata(11, now.AddSeconds(-1), "AppraisalBars");
        var stale = Metadata(10, now.AddSeconds(-1), "AppraisalBars");
        var wrong = Metadata(12, now.AddSeconds(-1), "PokemonDetails");
        Assert(barrier.Accepts(fresh, now), "Fresh frame should pass barrier.");
        Assert(!barrier.Accepts(stale, now), "Stale frame passed barrier.");
        Assert(!barrier.Accepts(wrong, now), "Wrong-state frame passed barrier.");
        return Task.CompletedTask;
    }

    public static Task CanonicalConsensusRequiresDistinctEvidenceAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var evidence = new PokemonItemEvidenceSet("item-1", [Frame(1, "a"), Frame(2, "b")], [Frame(3, "c"), Frame(4, "d")]);
        var result = analyzer.Analyze(evidence,
            [new("Pikachu", .9, 1, "a"), new("Pikachu", .8, 2, "b")],
            [new(402, 1, 1, "a"), new(402, .25, 2, "b")],
            [new((15, 14, 13), .9, 3, "c"), new((15, 14, 13), .8, 4, "d")]);
        Assert(result.Species.Status == SemanticFieldStatus.Known && result.Cp.Status == SemanticFieldStatus.Known, "Two-frame agreement was not accepted.");
        Assert(result.AttackIv.Value == 15 && result.DefenseIv.Value == 14 && result.HpIv.Value == 13 && result.IsComplete, "IV consensus was not accepted.");
        return Task.CompletedTask;
    }

    public static Task AppraisalBarsHeaderSuppliesSpeciesAndCpAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var evidence = new PokemonItemEvidenceSet("item-2", [Frame(1, "a"), Frame(2, "b"), Frame(3, "c")], [Frame(1, "a"), Frame(2, "b"), Frame(3, "c")]);
        var result = analyzer.Analyze(evidence,
            [new("Mankey", .9, 1, "a"), new("Mankey", .8, 2, "b")],
            [new(476, 1, 1, "a"), new(476, .25, 2, "b")],
            []);
        Assert(result.Species.Status == SemanticFieldStatus.Known && result.Cp.Status == SemanticFieldStatus.Known && result.Cp.Value == 476, "AppraisalBars header did not resolve species and CP.");
        return Task.CompletedTask;
    }

    public static Task SameFrameCannotSatisfyConsensusAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var evidence = new PokemonItemEvidenceSet("item-3", [Frame(1, "a")], [Frame(1, "a")]);
        var result = analyzer.Analyze(evidence,
            [new("Mankey", .9, 1, "a"), new("Mankey", .8, 1, "a")],
            [new(476, 1, 1, "a"), new(476, .25, 1, "a")],
            []);
        Assert(result.Species.Status == SemanticFieldStatus.Unknown && result.Cp.Status == SemanticFieldStatus.Unknown, "Repeated observations from one frame satisfied consensus.");
        return Task.CompletedTask;
    }

    public static Task CompetingThirdReadingFailsClosedAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var evidence = new PokemonItemEvidenceSet(
            "item-4",
            [Frame(1, "a"), Frame(2, "b"), Frame(3, "c")],
            [Frame(1, "a"), Frame(2, "b"), Frame(3, "c")]);
        var result = analyzer.Analyze(
            evidence,
            [new("Quaxly", .9, 1, "a"), new("Quaxly", .9, 2, "b"), new("Quaxly", .9, 3, "c")],
            [new(74, 1, 1, "a"), new(74, 1, 2, "b"), new(574, 1, 3, "c")],
            [new((10, 11, 12), .9, 1, "a"), new((10, 11, 12), .9, 2, "b"), new((10, 11, 13), .9, 3, "c")]);
        Assert(
            result.Cp.Status == SemanticFieldStatus.Conflicting && result.Cp.Value is null,
            "A competing CP reading was hidden by 2-of-3 majority.");
        Assert(
            result.AttackIv.Status == SemanticFieldStatus.Conflicting &&
            result.DefenseIv.Status == SemanticFieldStatus.Conflicting &&
            result.HpIv.Status == SemanticFieldStatus.Conflicting,
            "A competing IV triple was hidden by 2-of-3 majority.");
        return Task.CompletedTask;
    }

    public static Task CpRequiresAnchoredObservationAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var evidence = new PokemonItemEvidenceSet(
            "item-5",
            [Frame(1, "a"), Frame(2, "b"), Frame(3, "c")],
            [Frame(1, "a"), Frame(2, "b"), Frame(3, "c")]);
        var unanchored = analyzer.Analyze(
            evidence,
            [],
            [new(4012, .25, 1, "a"), new(4012, .25, 2, "b"), new(4012, .25, 3, "c")],
            []);
        Assert(
            unanchored.Cp.Status == SemanticFieldStatus.Unknown,
            "Three unanchored digit readings became Known CP.");

        var anchored = analyzer.Analyze(
            evidence,
            [],
            [new(401, 1, 1, "a"), new(401, .25, 2, "b"), new(401, .25, 3, "c")],
            []);
        Assert(
            anchored.Cp is { Status: SemanticFieldStatus.Known, Value: 401 },
            "One exact CP anchor plus independent agreement did not resolve.");
        return Task.CompletedTask;
    }

    public static Task CpEditChainResolvesAnimatedOcclusionAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var evidence = new PokemonItemEvidenceSet(
            "item-edit-chain",
            [Frame(1, "a"), Frame(2, "b"), Frame(3, "c")],
            [Frame(1, "a"), Frame(2, "b"), Frame(3, "c")]);
        var result = analyzer.Analyze(
            evidence,
            [],
            [
                new(31, 1, 1, "a"),
                new(4531, .25, 2, "b"),
                new(531, 1, 3, "c")
            ],
            []);
        Assert(
            result.Cp is
            {
                Status: SemanticFieldStatus.Known,
                Value: 531
            } &&
            result.Cp.Reasons.Contains(
                "CP_THREE_FRAME_EDIT_CHAIN_AGREEMENT",
                StringComparer.Ordinal),
            "A unique anchored CP edit chain did not resolve its middle value.");
        return Task.CompletedTask;
    }

    public static Task CpFiveFrameMajorityResolvesSingleDigitOcclusionAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var evidenceFrames = Enumerable.Range(1, 5)
            .Select(index => Frame(index, index.ToString()))
            .ToArray();
        var result = analyzer.Analyze(
            new PokemonItemEvidenceSet(
                "item-five-frame", evidenceFrames, evidenceFrames),
            [],
            [
                new(53, 1, 1, "1"),
                new(531, 1, 2, "2"),
                new(531, 1, 3, "3"),
                new(531, 1, 4, "4"),
                new(31, .25, 5, "5")
            ],
            []);
        Assert(
            result.Cp is
            {
                Status: SemanticFieldStatus.Known,
                Value: 531
            } &&
            result.Cp.Reasons.Contains(
                "CP_MULTI_FRAME_OCCLUSION_AGREEMENT",
                StringComparer.Ordinal),
            "Five-frame CP majority did not resolve bounded digit occlusion.");
        return Task.CompletedTask;
    }

    public static Task CpShortMajorityDoesNotBeatLongerReadingsAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var evidenceFrames = Enumerable.Range(1, 5)
            .Select(index => Frame(index, index.ToString()))
            .ToArray();
        var result = analyzer.Analyze(
            new PokemonItemEvidenceSet(
                "item-short-majority", evidenceFrames, evidenceFrames),
            [],
            [
                new(53, 1, 1, "1"),
                new(53, 1, 2, "2"),
                new(53, 1, 3, "3"),
                new(530, 1, 4, "4"),
                new(531, .75, 5, "5")
            ],
            []);
        Assert(
            result.Cp.Status == SemanticFieldStatus.Conflicting &&
            result.Cp.Value is null,
            "A shorter CP majority incorrectly beat longer readings.");
        return Task.CompletedTask;
    }

    public static Task CpPreprocessingVariantsResolveOnlyStrongEvidenceAsync()
    {
        static SemanticFieldResult<int?> Field(
            int? value,
            SemanticFieldStatus status,
            params long[] frames) =>
            new(
                value,
                status,
                status == SemanticFieldStatus.Known ? 1 : 0,
                frames,
                frames.Select(x => x.ToString()).ToArray(),
                []);

        var raw = Field(
            470, SemanticFieldStatus.Known, 1, 2, 3, 4, 5);
        var noisy = Field(
            null, SemanticFieldStatus.Conflicting, 1, 2, 3, 4, 5);
        var resolved =
            PokemonItemSemanticAnalyzer.ResolveCpPreprocessingVariants(
                raw, noisy);
        Assert(
            resolved is
            {
                Status: SemanticFieldStatus.Known,
                Value: 470
            },
            "Unanimous five-frame preprocessing did not override a noisy variant.");

        var disagreement =
            PokemonItemSemanticAnalyzer.ResolveCpPreprocessingVariants(
                raw,
                Field(704, SemanticFieldStatus.Known, 1, 2, 3, 4, 5));
        Assert(
            disagreement.Status == SemanticFieldStatus.Conflicting &&
            disagreement.Value is null,
            "Two different Known preprocessing results did not fail closed.");

        var calibratedSecond =
            PokemonItemSemanticAnalyzer.ResolveCpPreprocessingVariants(
                raw,
                Field(704, SemanticFieldStatus.Known, 1, 2, 3, 4, 5),
                preferSecondWhenFullySupported: true);
        Assert(
            calibratedSecond is
            {
                Status: SemanticFieldStatus.Known,
                Value: 704
            } &&
            calibratedSecond.Reasons.Contains(
                "CP_CALIBRATED_PREPROCESSING_SELECTED"),
            "The explicitly calibrated five-frame preprocessing was not selected.");

        var strongerWindow =
            PokemonItemSemanticAnalyzer.ResolveCpPreprocessingVariants(
                raw,
                Field(47, SemanticFieldStatus.Known, 1, 2, 3));
        Assert(
            strongerWindow is
            {
                Status: SemanticFieldStatus.Known,
                Value: 470
            } &&
            strongerWindow.Reasons.Contains(
                "CP_PREPROCESSING_FULL_WINDOW_SUPPORT_SELECTED"),
            "A complete five-frame CP window did not beat partial support.");

        var prefixResolved =
            PokemonItemSemanticAnalyzer.ResolveCpPreprocessingVariants(
                Field(147, SemanticFieldStatus.Known, 1, 2, 3, 4, 5),
                Field(14, SemanticFieldStatus.Known, 1, 2, 3, 4, 5));
        Assert(
            prefixResolved.Status == SemanticFieldStatus.Unknown &&
            prefixResolved.Value is null &&
            prefixResolved.Reasons.Contains(
                "CP_PREPROCESSING_PREFIX_AMBIGUOUS"),
            "A strict-prefix CP disagreement was not kept fail-closed.");

        var weakAgainstConflict =
            PokemonItemSemanticAnalyzer.ResolveCpPreprocessingVariants(
                Field(22, SemanticFieldStatus.Known, 1, 2, 3),
                noisy);
        Assert(
            weakAgainstConflict.Status == SemanticFieldStatus.Unknown &&
            weakAgainstConflict.Value is null,
            "Three-frame CP support incorrectly overrode conflicting preprocessing.");

        var weakAgainstUnknown =
            PokemonItemSemanticAnalyzer.ResolveCpPreprocessingVariants(
                Field(74, SemanticFieldStatus.Known, 1, 2),
                Field(null, SemanticFieldStatus.Unknown, 1, 2, 3, 4, 5));
        Assert(
            weakAgainstUnknown.Status == SemanticFieldStatus.Unknown &&
            weakAgainstUnknown.Value is null,
            "Two-frame CP support incorrectly overrode unknown preprocessing.");

        var noTrustedCandidate =
            PokemonItemSemanticAnalyzer.ResolveCpPreprocessingVariants(
                noisy,
                Field(
                    null, SemanticFieldStatus.Unknown, 1, 2, 3, 4, 5));
        Assert(
            noTrustedCandidate.Status == SemanticFieldStatus.Unknown &&
            noTrustedCandidate.Value is null,
            "Untrusted preprocessing noise was mislabeled as a semantic conflict.");
        return Task.CompletedTask;
    }

    public static Task CpUniqueDeletionReconstructionIsFailClosedAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var frames = Enumerable.Range(1, 5)
            .Select(index => Frame(index, index.ToString()))
            .ToArray();
        var reconstructed = analyzer.Analyze(
            new PokemonItemEvidenceSet(
                "item-reconstructed", frames, frames),
            [],
            [
                new(43, .75, 1, "1"),
                new(4, 1, 2, "2"),
                new(49, .75, 3, "3"),
                new(43, .75, 4, "4"),
                new(39, 1, 5, "5")
            ],
            []);
        Assert(
            reconstructed.Cp is
            {
                Status: SemanticFieldStatus.Known,
                Value: 439
            },
            "Unique four-of-five digit-deletion evidence did not reconstruct CP439.");

        var ambiguous = analyzer.Analyze(
            new PokemonItemEvidenceSet(
                "item-ambiguous", frames, frames),
            [],
            [
                new(12, 1, 1, "1"),
                new(12, 1, 2, "2"),
                new(13, 1, 3, "3"),
                new(13, 1, 4, "4"),
                new(1, .75, 5, "5")
            ],
            []);
        Assert(
            ambiguous.Cp.Status == SemanticFieldStatus.Conflicting &&
            ambiguous.Cp.Value is null,
            "Ambiguous digit-deletion evidence did not remain fail-closed.");
        return Task.CompletedTask;
    }

    public static Task SemanticProgressionRequiresKnownDifferenceAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var previousEvidence = new PokemonItemEvidenceSet(
            "previous", [Frame(1, "a"), Frame(2, "b")],
            [Frame(1, "a"), Frame(2, "b")]);
        var candidateEvidence = new PokemonItemEvidenceSet(
            "candidate", [Frame(3, "c"), Frame(4, "d")],
            [Frame(3, "c"), Frame(4, "d")]);
        var previous = analyzer.Analyze(
            previousEvidence,
            [new("Pikachu", 1, 1, "a"), new("Pikachu", 1, 2, "b")],
            [new(88, 1, 1, "a"), new(88, 1, 2, "b")],
            []);
        var changed = analyzer.Analyze(
            candidateEvidence,
            [new("Pikachu", 1, 3, "c"), new("Pikachu", 1, 4, "d")],
            [new(129, 1, 3, "c"), new(129, 1, 4, "d")],
            []);
        var unknown = analyzer.Analyze(candidateEvidence, [], [], []);

        Assert(
            PokemonItemProgressionEvidence.ProveDifferent(previous, changed)
                .SequenceEqual(["CP_CHANGED"]),
            "Known CP 88 to CP 129 did not prove semantic progression.");
        Assert(
            PokemonItemProgressionEvidence.ProveDifferent(previous, unknown)
                .Count == 0,
            "Unknown candidate data was treated as proof of progression.");

        var previousIv = analyzer.Analyze(
            previousEvidence,
            [new("Pikachu", 1, 1, "a"), new("Pikachu", 1, 2, "b")],
            [new(129, 1, 1, "a"), new(129, 1, 2, "b")],
            [new((9, 4, 15), 1, 1, "a"), new((9, 4, 15), 1, 2, "b")]);
        var changedIv = analyzer.Analyze(
            candidateEvidence,
            [new("Pikachu", 1, 3, "c"), new("Pikachu", 1, 4, "d")],
            [new(129, 1, 3, "c"), new(129, 1, 4, "d")],
            [new((9, 4, 5), 1, 3, "c"), new((9, 4, 5), 1, 4, "d")]);
        Assert(
            PokemonItemProgressionEvidence.ProveDifferent(previousIv, changedIv)
                .SequenceEqual(["HP_IV_CHANGED"]),
            "Same species and CP with a Known IV-only difference did not prove progression.");
        return Task.CompletedTask;
    }

    private static PokemonEvidenceFrame Frame(long id, string hash) => new(id, DateTimeOffset.UtcNow, hash, "AppraisalBars", "test");
    private static FrameMetadata Metadata(long id, DateTimeOffset captured, string state) => new(new FrameId(id), new FrameTimestamp(id, captured, TimeSpan.Zero), new FrameDescriptor(2, 1, 8, FramePixelFormat.Bgra32), FrameQuality.Unknown, new FrameStability(0, 3, TimeSpan.FromMilliseconds(100), true), "test", new Dictionary<string, string> { ["screen"] = state });
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
