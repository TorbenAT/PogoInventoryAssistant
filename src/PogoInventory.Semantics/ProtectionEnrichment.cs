using PogoInventory.Core.Models;
using PogoInventory.Core.Reference;

namespace PogoInventory.Semantics;

/// <summary>
/// A decoded evidence frame supplied by the host.  Semantic enrichment has no
/// image-decoder dependency and will only use a frame when its ID and SHA-256
/// are also present in the canonical item evidence set.
/// </summary>
public sealed record ProtectionVisualFrame(
    long FrameId,
    string EvidenceHash,
    int Width,
    int Height,
    byte[] Rgba);

/// <summary>
/// Fail-closed enrichment of canonical protection fields.  The current
/// deterministic visual authority is intentionally limited to the visible
/// favourite control; unproven UI indicators remain Unknown.
/// </summary>
public static class ProtectionEnrichment
{
    public static PokemonProtection Analyze(
        PokemonItemEvidenceSet evidence,
        IReadOnlyList<ProtectionVisualFrame> frames,
        SemanticFieldResult<string> species,
        SpeciesReferenceData speciesReference)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(speciesReference);

        return new PokemonProtection
        {
            Favorite = AnalyzeFavorite(evidence, frames),
            Legendary = DeriveRarity(species, speciesReference,
                SpeciesClassification.Legendary, "LEGENDARY"),
            Mythical = DeriveRarity(species, speciesReference,
                SpeciesClassification.Mythical, "MYTHICAL"),
            UltraBeast = DeriveRarity(species, speciesReference,
                SpeciesClassification.UltraBeast, "ULTRA_BEAST")
        };
    }

    private static ProtectionField<bool> AnalyzeFavorite(
        PokemonItemEvidenceSet evidence,
        IReadOnlyList<ProtectionVisualFrame> frames)
    {
        var knownFrames = evidence.HeaderFrames
            .Concat(evidence.AppraisalFrames)
            .Select(frame => (frame.FrameId, frame.EvidenceHash))
            .ToHashSet();
        var readings = frames
            .Where(frame => knownFrames.Contains((frame.FrameId, frame.EvidenceHash)))
            .Select(ReadFavorite)
            .Where(reading => reading is not null)
            .Cast<FavoriteReading>()
            .ToArray();

        if (readings.Length < 2)
        {
            return ProtectionField<bool>.Unknown("FAVORITE_VISUAL_EVIDENCE_INSUFFICIENT");
        }

        var values = readings.Select(reading => reading.Value).Distinct().ToArray();
        var protectionEvidence = readings.Select(reading => new ProtectionEvidence
        {
            Source = ProtectionEvidenceSource.Visual,
            FrameId = reading.Frame.FrameId,
            EvidenceHash = reading.Frame.EvidenceHash,
            RawEvidence = $"goldPixels={reading.GoldPixels}; grayPixels={reading.GrayPixels}",
            Detail = "APPRAISAL_FAVORITE_STAR_ROI"
        }).ToArray();
        if (values.Length != 1)
        {
            return new ProtectionField<bool>
            {
                State = ProtectionProofState.Conflicting,
                HasValue = false,
                Confidence = 0,
                Evidence = protectionEvidence,
                Reasons = ["FAVORITE_VISUAL_EVIDENCE_CONFLICT"]
            };
        }

        return new ProtectionField<bool>
        {
            Value = values[0],
            HasValue = true,
            State = ProtectionProofState.Known,
            Confidence = Math.Min(1d, readings.Length / 3d),
            Evidence = protectionEvidence,
            Reasons = [values[0]
                ? "FAVORITE_GOLD_STAR_MULTI_FRAME"
                : "FAVORITE_OUTLINE_STAR_MULTI_FRAME"]
        };
    }

    private static FavoriteReading? ReadFavorite(ProtectionVisualFrame frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0 ||
            frame.Rgba.Length != checked(frame.Width * frame.Height * 4))
        {
            return null;
        }

        // Normalised from the OnePlus 6T appraisal screen. This bounded corner
        // region excludes the dynamic Pokémon model and all text/OCR regions.
        var left = (int)Math.Floor(frame.Width * .855d);
        var right = (int)Math.Ceiling(frame.Width * .945d);
        var top = (int)Math.Floor(frame.Height * .090d);
        var bottom = (int)Math.Ceiling(frame.Height * .132d);
        if (left >= right || top >= bottom)
        {
            return null;
        }

        var gold = 0;
        var gray = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var index = (y * frame.Width + x) * 4;
                var r = frame.Rgba[index];
                var g = frame.Rgba[index + 1];
                var b = frame.Rgba[index + 2];
                if (r >= 185 && g is >= 110 and <= 240 && b <= 105 && r >= g + 25)
                {
                    gold++;
                }
                else if (r is >= 105 and <= 235 &&
                         Math.Abs(r - g) <= 25 && Math.Abs(g - b) <= 25)
                {
                    gray++;
                }
            }
        }

        var area = (right - left) * (bottom - top);
        var goldMinimum = Math.Max(160, (int)Math.Ceiling(area * .030d));
        const int grayMinimum = 80;
        if (gold >= goldMinimum)
        {
            return new FavoriteReading(frame, true, gold, gray);
        }
        if (gold <= 2 && gray >= grayMinimum)
        {
            return new FavoriteReading(frame, false, gold, gray);
        }
        return null;
    }

    private static ProtectionField<bool> DeriveRarity(
        SemanticFieldResult<string> species,
        SpeciesReferenceData reference,
        SpeciesClassification target,
        string label)
    {
        if (species.Status != SemanticFieldStatus.Known ||
            string.IsNullOrWhiteSpace(species.Value) ||
            reference.Classification(species.Value) is not { } classification)
        {
            return ProtectionField<bool>.Unknown($"{label}_SPECIES_NOT_KNOWN");
        }

        var evidence = species.FrameIds.Zip(species.EvidenceHashes)
            .Select(pair => new ProtectionEvidence
            {
                Source = ProtectionEvidenceSource.ReferenceDerived,
                FrameId = pair.First,
                EvidenceHash = pair.Second,
                RawEvidence = species.Value,
                Detail = $"species-reference:{reference.Version}; classification={classification}"
            })
            .ToArray();
        if (evidence.Length == 0)
        {
            return ProtectionField<bool>.Unknown($"{label}_SPECIES_EVIDENCE_MISSING");
        }

        return new ProtectionField<bool>
        {
            Value = classification == target,
            HasValue = true,
            State = ProtectionProofState.Known,
            Confidence = species.Confidence,
            Evidence = evidence,
            Reasons = [$"{label}_REFERENCE_DERIVED"]
        };
    }

    private sealed record FavoriteReading(
        ProtectionVisualFrame Frame,
        bool Value,
        int GoldPixels,
        int GrayPixels);
}
