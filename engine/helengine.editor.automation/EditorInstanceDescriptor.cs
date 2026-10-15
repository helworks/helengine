using System.Text.Json.Serialization;

namespace helengine.editor.automation;

/// <summary>
/// Identifies a verified running editor instance without exposing transport credentials.
/// </summary>
public sealed class EditorInstanceDescriptor
{
    /// <summary>Gets or sets the stable random identity of the editor process instance.</summary>
    [JsonPropertyName("instance_id")]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the displayed project name.</summary>
    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Gets or sets the canonical root path of the opened project.</summary>
    [JsonPropertyName("project_root_path")]
    public string ProjectRootPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the identity of the current project generation.</summary>
    [JsonPropertyName("project_generation")]
    public string ProjectGeneration { get; set; } = string.Empty;

    /// <summary>Gets or sets the local MCP endpoint currently published by the editor.</summary>
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the version of the Helengine automation contract.</summary>
    [JsonPropertyName("contract_version")]
    public int ContractVersion { get; set; } = 1;

    /// <summary>Gets or sets the displayed editor version.</summary>
    [JsonPropertyName("editor_version")]
    public string EditorVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the operating-system process id as an integer.</summary>
    [JsonPropertyName("process_id")]
    public int ProcessId { get; set; }

    /// <summary>Gets or sets the UTC-aware time at which the editor instance started.</summary>
    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; set; }
}
