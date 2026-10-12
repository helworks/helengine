namespace helengine.editor.tests.managers.project;

/// <summary>Reads a packaged animation reference through the runtime reference codec while capturing its final dependency path.</summary>
internal sealed class AnimationDependencyAssetValueReader : IScenePersistenceAssetValueReader {
    /// <summary>Clip returned for the single animation member in the production AnimationPlayer schema.</summary>
    readonly AnimationClipAsset Clip;

    /// <summary>Expected final runtime path shared by the scene asset table and component payload.</summary>
    readonly string ExpectedPath;

    /// <summary>Initializes the reader with the final table path and clip returned to the decoded component.</summary>
    public AnimationDependencyAssetValueReader(string expectedPath, AnimationClipAsset clip) {
        ExpectedPath = expectedPath;
        Clip = clip;
    }

    /// <summary>Consumes only animation asset values using the actual packaged reference decoder; the shared walker reads ordinary members.</summary>
    public bool TryReadAssetValue(EngineBinaryReader reader, Type valueType, out object value) {
        value = null;
        if (valueType != typeof(AnimationClipAsset)) {
            return false;
        }
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
        Assert.NotNull(reference);
        Assert.Equal(ExpectedPath, reference.RelativePath);
        value = Clip;
        return true;
    }
}
