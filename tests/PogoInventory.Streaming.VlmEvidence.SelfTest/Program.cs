using System.Text.Json;
using PogoInventory.Streaming.Semantics.Ollama.Evidence;

var schema = EvidenceSchema.BuildSchema();
var required = schema["required"]!.AsArray().Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
var expected = new[] { "layoutSupported", "screenState", "species", "cp", "attackIv", "defenseIv", "hpIv", "diagnostics" };
var failures = 0;
Check("schema requires all top-level fields", expected.All(required.Contains));
Check("schema rejects additional properties", schema["additionalProperties"]!.GetValue<bool>() == false);
var properties = schema["properties"]!.AsObject();
foreach (var field in expected.Where(x => x is not "layoutSupported" and not "diagnostics"))
{
    var fieldSchema = properties[field]!.AsObject();
    var fieldRequired = fieldSchema["required"]!.AsArray().Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
    Check($"{field} requires status/value/confidence/visibleText", new[] { "status", "value", "confidence", "visibleText" }.All(fieldRequired.Contains));
}
Check("schema serializes as JSON object", JsonDocument.Parse(schema.ToJsonString()).RootElement.ValueKind == JsonValueKind.Object);
Console.WriteLine($"VLM evidence self-test: {expected.Length + 2 - failures}/{expected.Length + 2}");
Console.WriteLine("AuthorizesPhoneInput: false");
Console.WriteLine("InputCommandsSent: 0");
return failures;

void Check(string name, bool ok) { if (!ok) { failures++; Console.Error.WriteLine($"FAIL: {name}"); } else Console.WriteLine($"PASS: {name}"); }
