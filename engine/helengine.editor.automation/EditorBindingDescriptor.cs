using System.Text.Json.Serialization;

namespace helengine.editor.automation;

/// <summary>
/// Represents a client's explicit association with one verified editor instance.
/// </summary>
public sealed class EditorBindingDescriptor
{
    /// <summary>Gets or sets the random binding identity, independent of the endpoint port.</summary>
    [JsonPropertyName("binding_id")]
    public string BindingId { get; set; } = string.Empty;

    /// <summary>Gets or sets the full identity snapshot of the bound editor instance.</summary>
    [JsonPropertyName("instance")]
    public EditorInstanceDescriptor Instance { get; set; }

    /// <summary>Gets or sets the binding lifecycle state reported to the client.</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}
