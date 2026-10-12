namespace helengine.editor.tests.managers.project;

/// <summary>Exposes two direct model references that must receive CPU companions independently of scene-global output deduplication.</summary>
public sealed class SceneDependencyCpuModelComponent : Component {
    /// <summary>Generated primitive used by both fixture scenes through the CPU reference contract.</summary>
    [CpuReadableModelReference]
    public SceneAssetReference GeneratedModel { get; set; }

    /// <summary>Imported source model shared by both fixture scenes without applying GPU vertex limits.</summary>
    [CpuReadableModelReference]
    public SceneAssetReference ImportedModel { get; set; }
}
