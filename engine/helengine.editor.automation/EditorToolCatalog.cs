using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace helengine.editor.automation;

/// <summary>
/// Loads the immutable editor tool contract and applies its JSON Schema rules to runtime arguments.
/// </summary>
public static class EditorToolCatalog
{
    /// <summary>Stores every compiled input schema by the exact public tool name.</summary>
    static readonly IReadOnlyDictionary<string, JsonSchema> InputSchemas;

    /// <summary>Stores the declarative catalog in its original stable ordering.</summary>
    static readonly IReadOnlyList<EditorToolDefinition> Tools;

    /// <summary>Loads the embedded catalog once and compiles its input schemas with JsonSchema.Net.</summary>
    static EditorToolCatalog()
    {
        Assembly Assembly = typeof(EditorToolCatalog).Assembly;
        string ResourceName = Assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(".editor-tools.json", StringComparison.Ordinal));

        using Stream Resource = Assembly.GetManifestResourceStream(ResourceName);
        using JsonDocument Catalog = JsonDocument.Parse(Resource);
        List<EditorToolDefinition> Definitions = new();
        Dictionary<string, JsonSchema> Schemas = new(StringComparer.Ordinal);

        foreach (JsonElement Element in Catalog.RootElement.GetProperty("tools").EnumerateArray())
        {
            EditorToolDefinition Definition = JsonSerializer.Deserialize<EditorToolDefinition>(Element.GetRawText());
            Definition.InputSchema = Definition.InputSchema.Clone();
            Definition.OutputSchema = Definition.OutputSchema.Clone();
            Definitions.Add(Definition);
            Schemas.Add(Definition.Name, JsonSchema.Build(Definition.InputSchema));
            JsonSchema.Build(Definition.OutputSchema);
        }

        Tools = new ReadOnlyCollection<EditorToolDefinition>(Definitions);
        InputSchemas = new ReadOnlyDictionary<string, JsonSchema>(Schemas);
    }

    /// <summary>Returns detached definitions for all 107 tools in the embedded contract's order.</summary>
    /// <returns>A read-only snapshot whose mutable definitions cannot alter the shared catalog.</returns>
    public static IReadOnlyList<EditorToolDefinition> GetTools()
    {
        List<EditorToolDefinition> Snapshot = new(Tools.Count);
        foreach (EditorToolDefinition Definition in Tools)
        {
            Snapshot.Add(new EditorToolDefinition
            {
                Name = Definition.Name,
                InputSchema = Definition.InputSchema.Clone(),
                OutputSchema = Definition.OutputSchema.Clone(),
                Mutates = Definition.Mutates,
                Undoable = Definition.Undoable,
                Persistence = Definition.Persistence,
                OwnerTask = Definition.OwnerTask,
                FunctionalSource = Definition.FunctionalSource,
                BehaviorTest = Definition.BehaviorTest
            });
        }

        return new ReadOnlyCollection<EditorToolDefinition>(Snapshot);
    }

    /// <summary>Evaluates one tool argument object against its compiled JSON Schema.</summary>
    /// <param name="ToolName">The case-sensitive tool name registered in the catalog.</param>
    /// <param name="Arguments">The candidate JSON value supplied by a client.</param>
    /// <param name="Errors">A read-only collection explaining schema errors or an unknown tool.</param>
    /// <returns>True only when the tool exists and JsonSchema.Net accepts the complete input.</returns>
    public static bool TryValidateInputArguments(string ToolName, JsonElement Arguments, out IReadOnlyList<string> Errors)
    {
        if (!InputSchemas.TryGetValue(ToolName, out JsonSchema Schema))
        {
            Errors = Array.AsReadOnly(new[] { $"Unknown editor tool '{ToolName}'." });
            return false;
        }

        return TryValidate(Schema, Arguments, out Errors);
    }

    /// <summary>Validates a value against a caller-supplied JSON Schema, including dynamic document patch schemas.</summary>
    /// <param name="SchemaDocument">A complete JSON Schema document returned by an editor schema query.</param>
    /// <param name="Candidate">The JSON value to check against the supplied schema.</param>
    /// <param name="Errors">A read-only collection describing schema rejection, or an empty collection on success.</param>
    /// <returns>True only when JsonSchema.Net accepts the candidate instance.</returns>
    public static bool TryValidateAgainstSchema(JsonElement SchemaDocument, JsonElement Candidate, out IReadOnlyList<string> Errors)
    {
        JsonSchema Schema = JsonSchema.Build(SchemaDocument);
        return TryValidate(Schema, Candidate, out Errors);
    }

    /// <summary>Evaluates a compiled JSON Schema and returns a stable non-empty rejection explanation.</summary>
    /// <param name="Schema">The compiled schema used for validation.</param>
    /// <param name="Candidate">The JSON value being evaluated.</param>
    /// <param name="Errors">A read-only collection with a generic schema failure explanation.</param>
    /// <returns>True when the compiled schema accepts the candidate instance.</returns>
    static bool TryValidate(JsonSchema Schema, JsonElement Candidate, out IReadOnlyList<string> Errors)
    {
        EvaluationResults Results = Schema.Evaluate(Candidate, new EvaluationOptions());
        if (Results.IsValid)
        {
            Errors = Array.Empty<string>();
            return true;
        }

        Errors = Array.AsReadOnly(new[] { "The JSON value does not satisfy its JSON Schema." });
        return false;
    }
}
