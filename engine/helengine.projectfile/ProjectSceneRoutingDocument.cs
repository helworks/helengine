namespace helengine.projectfile;

/// <summary>Project-owned scene routing configuration keyed by platform identifier.</summary>
public sealed class ProjectSceneRoutingDocument {
    /// <summary>Gets or sets explicit routing records keyed by platform identifier.</summary>
    public Dictionary<string, ProjectPlatformSceneRoutingDocument> Platforms { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Explicit boot-scene and alias routing for one project platform.</summary>
public sealed class ProjectPlatformSceneRoutingDocument {
    /// <summary>Gets or sets the scene selected when the generated boot scene starts.</summary>
    public string BootSceneId { get; set; }

    /// <summary>Gets or sets logical-to-platform scene aliases.</summary>
    public Dictionary<string, string> SceneAliases { get; set; } = new(StringComparer.Ordinal);
}