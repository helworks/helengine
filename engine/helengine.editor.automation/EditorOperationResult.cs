using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor.automation;

/// <summary>
/// Describes the confirmed outcome or explicit failure of one editor operation.
/// </summary>
public sealed class EditorOperationResult
{
    /// <summary>Gets or sets whether the editor completed the requested operation.</summary>
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }

    /// <summary>Gets or sets the stable machine-readable error code, or an empty string on success.</summary>
    [JsonPropertyName("error_code")]
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>Gets or sets a human-readable explanation of the outcome.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the identity of the editor instance that handled the request.</summary>
    [JsonPropertyName("instance_id")]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the project generation observed while handling the request.</summary>
    [JsonPropertyName("project_generation")]
    public string ProjectGeneration { get; set; } = string.Empty;

    /// <summary>Gets or sets the resulting project revision when one is known.</summary>
    [JsonPropertyName("revision")]
    public string Revision { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation identity echoed by the editor.</summary>
    [JsonPropertyName("operation_id")]
    public string OperationId { get; set; } = string.Empty;

    /// <summary>Gets or sets detached JSON data that identifies returned values and applied effects.</summary>
    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}
