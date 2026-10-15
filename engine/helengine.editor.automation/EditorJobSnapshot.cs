using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor.automation;

/// <summary>
/// Captures an observable asynchronous editor job state and its detached result payload.
/// </summary>
public sealed class EditorJobSnapshot
{
    /// <summary>Gets or sets the stable job identity.</summary>
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation family or job kind.</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets a state from queued, running, cancel_requested, cancelled, succeeded, or failed.</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>Gets or sets a concise status message suitable for a client.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets whether a cancellation request is currently valid.</summary>
    [JsonPropertyName("can_cancel")]
    public bool CanCancel { get; set; }

    /// <summary>Gets or sets structured progress values detached from editor objects.</summary>
    [JsonPropertyName("progress")]
    public JsonElement Progress { get; set; }

    /// <summary>Gets or sets the final detached result, if the job has completed.</summary>
    [JsonPropertyName("result")]
    public JsonElement Result { get; set; }
}
