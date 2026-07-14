using System.Text.Json;
using PogoInventory.Verification.Models;

namespace PogoInventory.Verification.Services;

public static class CalcyProviderSelectionService
{
    public static async Task<CalcyProviderSelection> SelectAsync(
        string reportPath,
        CalcyProviderMechanism mechanism,
        string providerVersion,
        string outputPath,
        string? parserProfilePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var report = JsonSerializer.Deserialize<CalcyVerificationReport>(
            await File.ReadAllTextAsync(reportPath, cancellationToken),
            VerificationJson.CreateOptions()) ?? throw new InvalidOperationException(
                "Verification report contained no data.");
        report.Validate();

        if (!report.RecommendedForLongScan || !report.ZeroFalseComplete)
        {
            throw new InvalidOperationException(
                $"Provider selection refused: {report.GateDetail}");
        }

        if (mechanism == CalcyProviderMechanism.Unknown || mechanism != report.Mechanism)
        {
            throw new InvalidOperationException(
                "Selected mechanism must match the verified report mechanism.");
        }

        if (!string.IsNullOrWhiteSpace(report.ProviderVersion) &&
            !string.Equals(
                report.ProviderVersion,
                providerVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Selected provider version must match the verified report version.");
        }

        string? parserHash = null;
        string? fullParserPath = null;
        if (!string.IsNullOrWhiteSpace(parserProfilePath))
        {
            fullParserPath = Path.GetFullPath(parserProfilePath);
            if (!File.Exists(fullParserPath))
            {
                throw new FileNotFoundException("Parser profile was not found.", fullParserPath);
            }
            parserHash = await VerificationHash.Sha256Async(fullParserPath, cancellationToken);
        }

        var fullReportPath = Path.GetFullPath(reportPath);
        var selection = new CalcyProviderSelection
        {
            Mechanism = mechanism,
            ProviderVersion = providerVersion,
            SelectedAtUtc = DateTimeOffset.UtcNow,
            VerificationReportPath = fullReportPath,
            VerificationReportSha256 = await VerificationHash.Sha256Async(
                fullReportPath,
                cancellationToken),
            ParserProfilePath = fullParserPath,
            ParserProfileSha256 = parserHash,
            VerifiedCaseCount = report.CaseCount,
            ExactCompleteCount = report.ExactCompleteCount,
            ExactCompleteRate = report.ExactCompleteRate,
            WrongCompleteCount = report.WrongCompleteCount
        };
        selection.Validate();

        var fullOutput = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutput);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(
            fullOutput,
            JsonSerializer.Serialize(
                selection,
                VerificationJson.CreateOptions(writeIndented: true)),
            cancellationToken);
        return selection;
    }
}
