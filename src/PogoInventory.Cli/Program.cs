using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.Core.Reporting;

return await MainAsync(args);

static async Task<int> MainAsync(string[] args)
{
    try
    {
        if (args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        if (!args[0].Equals("analyze", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown command: {args[0]}");
            PrintHelp();
            return 2;
        }

        var options = ParseOptions(args.Skip(1).ToArray());
        var inventoryPath = Require(options, "inventory");
        var policyPath = Require(options, "policy");
        var outputDirectory = Require(options, "out");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var observations = JsonSerializer.Deserialize<List<PokemonObservation>>(
            await File.ReadAllTextAsync(inventoryPath),
            jsonOptions) ?? throw new InvalidOperationException("Inventory JSON contained no data.");

        var policy = JsonSerializer.Deserialize<RulePolicy>(
            await File.ReadAllTextAsync(policyPath),
            jsonOptions) ?? throw new InvalidOperationException("Policy JSON contained no data.");

        var analyzer = new InventoryAnalyzer();
        var result = analyzer.Analyze(observations, policy);

        var writer = new DecisionReportWriter();
        await writer.WriteAsync(result, outputDirectory);

        Console.WriteLine($"Analysed {result.Decisions.Count} Pokémon.");
        Console.WriteLine($"KEEP: {result.KeepCount}");
        Console.WriteLine($"REVIEW: {result.ReviewCount}");
        Console.WriteLine($"DELETE: {result.DeleteCount}");
        Console.WriteLine($"Reports written to: {Path.GetFullPath(outputDirectory)}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var index = 0; index < args.Length; index++)
    {
        var token = args[index];
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unexpected argument: {token}");
        }

        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {token}");
        }

        options[token[2..]] = args[++index];
    }

    return options;
}

static string Require(IReadOnlyDictionary<string, string> options, string key) =>
    options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing required option --{key}");

static void PrintHelp()
{
    Console.WriteLine("Pogo Inventory Assistant");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  analyze --inventory <file> --policy <file> --out <directory>");
}
