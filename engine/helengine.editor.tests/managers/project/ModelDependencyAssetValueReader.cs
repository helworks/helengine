namespace helengine.editor.tests.managers.project;

/// <summary>Reads the runtime mesh reference and requires the processed model path rather than its superseded authored source.</summary>
internal sealed class ModelDependencyAssetValueReader : IScenePersistenceAssetValueReader {
    /// <summary>Expected path of the tessellated output selected for scene preload.</summary>
    readonly string ExpectedPath;

    /// <summary>Counts model references consumed through the real packaged reference codec.</summary>
    public int ReadCount { get; private set; }

    /// <summary>Initializes the reader with the exact model path produced by the mesh modifier.</summary>
    public ModelDependencyAssetValueReader(string expectedPath) { ExpectedPath = expectedPath; }

    /// <summary>Consumes model references and leaves ordinary members to the production scene value walker.</summary>
    public bool TryReadAssetValue(EngineBinaryReader reader, Type valueType, out object value) {
        value = null;
        if (valueType != typeof(RuntimeModel)) { return false; }
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
        Assert.NotNull(reference);
        Assert.Equal(ExpectedPath, reference.RelativePath);
        ReadCount++;
        value = new helengine.editor.tests.testing.TestRuntimeModel();
        return true;
    }
}
