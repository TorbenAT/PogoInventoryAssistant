using PogoInventory.Automation.Models;
using PogoInventory.Exploration.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

internal static class TagSelectorTests
{
    public static async Task RunProfileSafetyAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "pogo-tag-profile-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "selector.png");
            await File.WriteAllBytesAsync(source, PngEncoder.Encode(
                new PixelImage(108, 234, Enumerable.Repeat(
                    (byte)255, 108 * 234 * 4).ToArray())));
            var profile = Profile(source, maximumScrolls: 3);
            profile.Validate(Path.Combine(root, "profile.json"));

            AssertThrows(() => Profile(source, maximumScrolls: 6)
                .Validate(Path.Combine(root, "profile.json")));
            AssertThrows(() => (profile with { Templates = Array.Empty<TagNameTemplate>() })
                .Validate(Path.Combine(root, "profile.json")));
            AssertThrows(() => (profile with
            {
                Templates = new[] { profile.Templates[0], profile.Templates[0] }
            }).Validate(Path.Combine(root, "profile.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public static async Task RunWorkflowContractAsync()
    {
        var source = await File.ReadAllTextAsync(RepositoryFile(
            "src", "PogoInventory.Cli", "Program.cs"));
        AssertContains(source, "initialSelected != selectedState",
            "Idempotence must suppress an already-satisfied row mutation.");
        AssertContains(source, "rowMutationActions = 0",
            "The no-mutation count must be explicit.");
        AssertContains(source, "TAG_NOT_FOUND_NO_MUTATION",
            "A missing confident name match must fail without a row tap.");
        AssertContains(source, "scrolls <= profile.MaximumScrolls",
            "Tag search must be bounded by its profile.");
        AssertContains(source, "updated.Row.IsSelected == selectedState",
            "Selected and unselected row state must be verified.");
        AssertContains(source, "detailsPill != selectedState",
            "The committed Details state must be verified.");
        AssertContains(source, "rowIndexUsed = false",
            "The workflow must record that no row ordinal was used.");
        AssertContains(source, "fixedRowCoordinateUsed = false",
            "The workflow must record that no fixed row coordinate was used.");
        AssertTrue(!source.Contains("rowIndexUsed = true", StringComparison.Ordinal),
            "No production tag path may opt into a fixed row index.");
    }

    private static TagSelectorProfile Profile(string source, int maximumScrolls) => new()
    {
        DeviceSerial = "synthetic:5555",
        ScreenWidth = 108,
        ScreenHeight = 234,
        MaximumScrolls = maximumScrolls,
        OpenTagMenu = new NormalizedPoint { X = 0.7, Y = 0.56 },
        Done = new NormalizedPoint { X = 0.5, Y = 0.79 },
        ScrollStart = new NormalizedPoint { X = 0.5, Y = 0.65 },
        ScrollEnd = new NormalizedPoint { X = 0.5, Y = 0.35 },
        Templates = new[]
        {
            new TagNameTemplate
            {
                Name = "Trade",
                SourceImage = source,
                Region = new NormalizedRegion { X = 0.1, Y = 0.1, Width = 0.5, Height = 0.1 }
            }
        }
    };

    private static string RepositoryFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "PogoInventoryAssistant.sln")))
        {
            current = current.Parent;
        }
        return Path.Combine(current?.FullName ?? throw new InvalidOperationException(
            "Repository root was not found."), Path.Combine(segments));
    }

    private static void AssertContains(string source, string expected, string message) =>
        AssertTrue(source.Contains(expected, StringComparison.Ordinal), message);

    private static void AssertThrows(Action action)
    {
        try
        {
            action();
            throw new InvalidOperationException("Expected validation failure was not observed.");
        }
        catch (InvalidDataException)
        {
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
