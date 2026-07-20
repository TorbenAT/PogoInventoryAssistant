using System.Text.Json;
using PogoInventory.Automation.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Exploration.Models;

public sealed record TagNameTemplate
{
    public required string Name { get; init; }
    public required string SourceImage { get; init; }
    public required NormalizedRegion Region { get; init; }
}

public sealed record TagSelectorProfile
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string DeviceSerial { get; init; }
    public required int ScreenWidth { get; init; }
    public required int ScreenHeight { get; init; }
    public double MinimumMatchConfidence { get; init; } = 0.88;
    public double MinimumMatchMargin { get; init; } = 0.10;
    public int MaximumScrolls { get; init; } = 3;
    public required NormalizedPoint OpenTagMenu { get; init; }
    public required NormalizedPoint Done { get; init; }
    public required NormalizedPoint ScrollStart { get; init; }
    public required NormalizedPoint ScrollEnd { get; init; }
    public required IReadOnlyList<TagNameTemplate> Templates { get; init; }

    public void Validate(string profilePath)
    {
        if (SchemaVersion != "1.0" || string.IsNullOrWhiteSpace(DeviceSerial) ||
            ScreenWidth <= 0 || ScreenHeight <= 0 ||
            MinimumMatchConfidence is < 0.50 or > 1 ||
            MinimumMatchMargin is < 0.02 or > 0.50 ||
            MaximumScrolls is < 0 or > 5 || Templates is null || Templates.Count == 0)
        {
            throw new InvalidDataException("Tag selector profile is incomplete or unsafe.");
        }
        OpenTagMenu.Validate(nameof(OpenTagMenu));
        Done.Validate(nameof(Done));
        ScrollStart.Validate(nameof(ScrollStart));
        ScrollEnd.Validate(nameof(ScrollEnd));
        if (Templates.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            Templates.Count)
        {
            throw new InvalidDataException("Tag names must be unique in the selector profile.");
        }
        foreach (var template in Templates)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(template.Name);
            template.Region.Validate();
            var source = ResolveSourcePath(profilePath, template.SourceImage);
            if (!File.Exists(source))
            {
                throw new FileNotFoundException(
                    $"Tag template source image was not found: {source}", source);
            }
        }
    }

    public static string ResolveSourcePath(string profilePath, string sourceImage) =>
        Path.GetFullPath(Path.IsPathRooted(sourceImage)
            ? sourceImage
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(profilePath))!, sourceImage));
}

public static class TagSelectorProfileLoader
{
    public static async Task<TagSelectorProfile> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var stream = File.OpenRead(Path.GetFullPath(path));
        var profile = await JsonSerializer.DeserializeAsync<TagSelectorProfile>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken) ?? throw new InvalidDataException("Tag selector profile was empty.");
        profile.Validate(path);
        return profile;
    }
}
