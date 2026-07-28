using System.Text.Json.Nodes;

namespace PogoInventory.Streaming.Semantics.Ollama.Evidence;

public static class EvidenceSchema
{
    public static JsonObject BuildSchema() => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = new JsonArray("layoutSupported", "screenState", "species", "cp", "attackIv", "defenseIv", "hpIv", "diagnostics"),
        ["properties"] = new JsonObject
        {
            ["layoutSupported"] = new JsonObject { ["type"] = "boolean" },
            ["screenState"] = FieldSchema(), ["species"] = FieldSchema(), ["cp"] = FieldSchema(), ["attackIv"] = FieldSchema(), ["defenseIv"] = FieldSchema(), ["hpIv"] = FieldSchema(),
            ["diagnostics"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } }
        }
    };

    private static JsonObject FieldSchema() => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = new JsonArray("status", "value", "confidence", "visibleText"),
        ["properties"] = new JsonObject
        {
            ["status"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Candidate", "Unknown", "Conflicting", "Occluded", "Unreadable", "NotVisible", "Unsupported") },
            ["value"] = new JsonObject { ["type"] = new JsonArray("string", "number", "null") },
            ["confidence"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
            ["visibleText"] = new JsonObject { ["type"] = new JsonArray("string", "null") }
        }
    };
}
