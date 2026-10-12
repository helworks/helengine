namespace helengine.editor.tests.managers.project;

/// <summary>Resolves effective packaged sprite references from the final cooked texture set without accepting source or inactive override aliases.</summary>
internal sealed class TextureDependencyAssetValueReader : IScenePersistenceAssetValueReader {
    /// <summary>Runtime textures keyed by their exact cooked content path.</summary>
    readonly IReadOnlyDictionary<string, RuntimeTexture> Textures;

    /// <summary>Records exact paths encountered while decoding effective sprite payloads, preserving component order.</summary>
    public List<string> ReadPaths { get; } = new();

    /// <summary>Initializes the reader with textures produced by the actual PS1 work-item executor.</summary>
    public TextureDependencyAssetValueReader(IReadOnlyDictionary<string, RuntimeTexture> textures) {
        Textures = textures;
    }

    /// <summary>Reads a texture reference through the runtime binary codec and rejects any path outside the declared final outputs.</summary>
    public bool TryReadAssetValue(EngineBinaryReader reader, Type valueType, out object value) {
        value = null;
        if (valueType != typeof(RuntimeTexture)) { return false; }
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
        Assert.NotNull(reference);
        Assert.True(Textures.TryGetValue(reference.RelativePath, out RuntimeTexture texture), reference.RelativePath);
        ReadPaths.Add(reference.RelativePath);
        value = texture;
        return true;
    }
}
