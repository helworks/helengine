using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor.automation;

/// <summary>
/// Carries one validated editor-tool invocation across the shared automation boundary.
/// </summary>
public sealed class EditorOperationRequest
{
    /// <summary>Gets or sets the exact catalog name of the requested operation.</summary>
    [JsonPropertyName("tool_name")]
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Gets or sets the explicit editor binding that owns this request.</summary>
    [JsonPropertyName("binding_id")]
    public string BindingId { get; set; } = string.Empty;

    /// <summary>Gets or sets the live scene identity when the operation addresses a scene.</summary>
    [JsonPropertyName("scene_handle")]
    public string SceneHandle { get; set; } = string.Empty;

    /// <summary>Gets or sets the revision that a mutation must match before it can execute.</summary>
    [JsonPropertyName("expected_revision")]
    public string ExpectedRevision { get; set; } = string.Empty;

    /// <summary>Gets or sets the caller-generated idempotency identity for a mutation.</summary>
    [JsonPropertyName("operation_id")]
    public string OperationId { get; set; } = string.Empty;

    /// <summary>Gets or sets the detached JSON object containing tool-specific arguments.</summary>
    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; }
}
