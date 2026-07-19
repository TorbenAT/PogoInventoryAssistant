using System.Xml.Linq;
using PogoInventory.Vision.Models;

namespace PogoInventory.Vision.Profiles;

public sealed class KnownModalClassifier
{
    public KnownPokemonGoModal? Classify(
        string hierarchyXml,
        string screenshotSha256,
        ScreenState expectedPostState = ScreenState.ExternalOverlay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hierarchyXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotSha256);

        var text = string.Join(" ", XDocument.Parse(hierarchyXml)
            .Descendants("node")
            .SelectMany(node => new[]
            {
                (string?)node.Attribute("text"),
                (string?)node.Attribute("content-desc")
            })
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!text.Contains("Adventure Sync", StringComparison.OrdinalIgnoreCase) ||
            !text.Contains("walked", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new KnownPokemonGoModal
        {
            Id = KnownPokemonGoModalId.AdventureSyncProgress,
            Confidence = 0.99,
            BeforeScreenshotSha256 = screenshotSha256,
            StateBefore = ScreenState.KnownInformationalPopup,
            StateAfter = expectedPostState,
            DismissalAllowed = true,
            Detail = "Adventure Sync progress information banner."
        };
    }
}
