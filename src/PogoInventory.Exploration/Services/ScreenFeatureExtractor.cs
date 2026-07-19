using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using PogoInventory.Exploration.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Exploration.Services;

public sealed class ScreenFeatureExtractor
{
    public ScreenObservation Extract(
        string sessionId,
        string observationId,
        string screenshotPath,
        byte[] screenshotPng,
        string? hierarchyXml,
        ScreenDetectionResult detection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(observationId);
        ArgumentNullException.ThrowIfNull(screenshotPng);
        ArgumentNullException.ThrowIfNull(detection);

        var image = PngDecoder.Decode(screenshotPng);
        var screenshotHash = Convert.ToHexString(SHA256.HashData(screenshotPng)).ToLowerInvariant();
        var hierarchyPath = hierarchyXml is null ? null : screenshotPath + ".uiautomator.xml";
        if (hierarchyPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(hierarchyPath)!);
            File.WriteAllText(hierarchyPath, hierarchyXml, Encoding.UTF8);
        }

        var nodes = ParseNodeSignatures(hierarchyXml);
        return new ScreenObservation
        {
            SessionId = sessionId,
            ObservationId = observationId,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            ScreenshotPath = screenshotPath,
            ScreenshotSha256 = screenshotHash,
            ScreenWidth = image.Width,
            ScreenHeight = image.Height,
            DetectedState = detection.State,
            StateConfidence = detection.Confidence,
            AlternativeStates = detection.States
                .Where(x => x.State != detection.State)
                .OrderByDescending(x => x.Score)
                .Take(3)
                .Select(x => x.State)
                .ToArray(),
            UiHierarchyPath = hierarchyPath,
            VisibleControls = nodes,
            ModalClassification = detection.State is ScreenState.ExternalOverlay or ScreenState.KnownInformationalPopup
                ? detection.State.ToString()
                : null,
            NoveltyScore = 1
        };
    }

    private static IReadOnlyList<string> ParseNodeSignatures(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Array.Empty<string>();
        }

        try
        {
            return XDocument.Parse(xml)
                .Descendants("node")
                .Select(node => string.Join('|',
                    (string?)node.Attribute("class") ?? "",
                    (string?)node.Attribute("resource-id") ?? "",
                    (string?)node.Attribute("text") ?? "",
                    (string?)node.Attribute("content-desc") ?? ""))
                .Where(x => x != "|||")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception) when (xml.Length > 0)
        {
            return Array.Empty<string>();
        }
    }
}
