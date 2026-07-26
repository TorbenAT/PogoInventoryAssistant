using PogoInventory.Application;
using PogoInventory.Core.Models;

namespace PogoInventory.SelfTest;

internal static class GroundTruthMeasurementTests
{
    public static void CsvParsingPreservesStatusesAndQuotedSources()
    {
        var csv = "RunId,Ordinal,ObservationId,GroundTruthEntityId,Species,Cp,AttackIv,DefenseIv,HpIv,Nickname,GroundTruthStatus,GroundTruthSource,ReviewerNote\n" +
            "run-1,1,id-1,entity-1,Pikachu,402,10,11,12,\"Pika, one\",Verified,\"frame,0011.png\",clear\n" +
            "run-1,2,id-2,,,,,,,,Unverifiable,frame-0012.png,unclear\n";
        var rows = GroundTruthMeasurementService.ParseCsv(csv);
        AssertEqual(2, rows.Count, "CSV parser row count");
        AssertEqual(GroundTruthStatus.Verified, rows[0].GroundTruthStatus, "verified status parsed");
        AssertEqual("Pika, one", rows[0].Nickname, "quoted comma preserved");
        AssertEqual(GroundTruthStatus.Unverifiable, rows[1].GroundTruthStatus, "unverifiable status parsed");
    }

    public static void FieldMetricSeparatesCorrectIncorrectUnknownAndUnverifiable()
    {
        var entries = new[]
        {
            (Label("1", GroundTruthStatus.Verified, "402"), Observation(402)),
            (Label("2", GroundTruthStatus.Verified, "403"), Observation(402)),
            (Label("3", GroundTruthStatus.Verified, "404"), Observation(null)),
            (Label("4", GroundTruthStatus.Unverifiable, null), Observation(999))
        };
        var metric = GroundTruthMeasurementService.MeasureFieldForTests("Cp", entries);
        AssertEqual(3, metric.VerifiedRows, "verified rows");
        AssertEqual(1, metric.Correct, "correct rows");
        AssertEqual(1, metric.Incorrect, "incorrect rows");
        AssertEqual(1, metric.Unknown, "unknown scanner rows");
        AssertEqual(1, metric.Unverifiable, "unverifiable rows");
        AssertEqual(50.0, metric.AccuracyAmongVerifiablePercent!.Value, "accuracy excludes unknown");
        AssertEqual(66.66666666666667, metric.CompletenessAmongVerifiablePercent!.Value, "completeness among verified rows");
    }

    public static void ReviewCauseClassificationIsFailClosed()
    {
        var result = GroundTruthMeasurementService.ClassifyCauseForTests(
            Observation(null), Observation(402),
            Label("1", GroundTruthStatus.Unverifiable, null),
            Label("2", GroundTruthStatus.Unverifiable, "402"));
        AssertEqual("CpNotExtracted", result.Cause, "missing CP is classified without guessing");
        AssertEqual("CP", result.Feature, "missing CP feature");
    }

    private static GroundTruthLabelRow Label(string ordinal, GroundTruthStatus status, string? cp) => new()
    {
        RunId = "run-1", Ordinal = int.Parse(ordinal), ObservationId = "id-" + ordinal,
        Cp = cp, GroundTruthStatus = status, GroundTruthSource = "frame.png"
    };

    private static PokemonObservation Observation(int? cp) => new()
    {
        ExternalKey = "test", SequenceNumber = 1, Species = "Pikachu", Cp = cp
    };

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
    }
}
