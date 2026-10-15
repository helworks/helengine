using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor.automation;

/// <summary>
/// Publishes the schema and conservative behavioral metadata for one editor tool.
/// </summary>
public sealed class EditorToolDefinition
{
    /// <summary>Gets or sets the unique MCP-facing operation name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON Schema used to validate tool arguments before dispatch.</summary>
    [JsonPropertyName("input_schema")]
    public JsonElement InputSchema { get; set; }

    /// <summary>Gets or sets the explicit success-or-error result schema for the operation.</summary>
    [JsonPropertyName("output_schema")]
    public JsonElement OutputSchema { get; set; }

    /// <summary>Gets or sets whether this operation may change editor or project state.</summary>
    [JsonPropertyName("mutates")]
    public bool Mutates { get; set; }

    /// <summary>Gets or sets whether undo is guaranteed for every successful mutation of this tool.</summary>
    [JsonPropertyName("undoable")]
    public bool Undoable { get; set; }

    /// <summary>Gets or sets the declared persistence behavior for the operation's effects.</summary>
    [JsonPropertyName("persistence")]
    public string Persistence { get; set; } = string.Empty;

    /// <summary>Gets or sets the task number that owns implementation of the operation.</summary>
    [JsonPropertyName("owner_task")]
    public int OwnerTask { get; set; }

    /// <summary>Gets or sets the audited editor source that provides this operation's behavior.</summary>
    [JsonPropertyName("functional_source")]
    public string FunctionalSource { get; set; } = string.Empty;

    /// <summary>Gets or sets the behavior-test identity required before this tool is implemented.</summary>
    [JsonPropertyName("behavior_test")]
    public string BehaviorTest { get; set; } = string.Empty;
}
