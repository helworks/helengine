using System.Reflection;
using Xunit;

namespace helengine.editor.tests.serialization.scene;

/// <summary>
/// Verifies constrained scene asset reference construction.
/// </summary>
public sealed class SceneAssetReferenceFactoryTests {
    /// <summary>
    /// Ensures the scene asset reference no longer exposes a public parameterless constructor or writable properties.
    /// </summary>
    [Fact]
    public void SceneAssetReference_IsNotFreelyMutable() {
        Assert.Null(typeof(SceneAssetReference).GetConstructor(Type.EmptyTypes));
        Assert.False(typeof(SceneAssetReference).GetProperty(nameof(SceneAssetReference.SourceKind))?.CanWrite ?? true);
        Assert.False(typeof(SceneAssetReference).GetProperty(nameof(SceneAssetReference.RelativePath))?.CanWrite ?? true);
        Assert.False(typeof(SceneAssetReference).GetProperty(nameof(SceneAssetReference.ProviderId))?.CanWrite ?? true);
        Assert.False(typeof(SceneAssetReference).GetProperty(nameof(SceneAssetReference.AssetId))?.CanWrite ?? true);
        Assert.Null(typeof(global::helengine.SceneAssetReferenceFactory).GetMethod("Rehydrate", BindingFlags.Public | BindingFlags.Static));
    }

    /// <summary>
    /// Ensures file-backed references come from the sanctioned file-system factory shape.
    /// </summary>
    [Fact]
    public void CreateFileSystemFont_ReturnsFileBackedReference() {
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemFont("Fonts/DemoDiscBody.ttf");

        Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, reference.SourceKind);
        Assert.Equal("Fonts/DemoDiscBody.ttf", reference.RelativePath);
        Assert.Equal(string.Empty, reference.ProviderId);
        Assert.Equal(string.Empty, reference.AssetId);
    }

    /// <summary>
    /// Ensures generated engine references come from the sanctioned engine-generated factory shape.
    /// </summary>
    [Fact]
    public void CreateCubeModel_ReturnsEngineGeneratedReference() {
        SceneAssetReference reference = global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel();

        Assert.Equal(SceneAssetReferenceSourceKind.Generated, reference.SourceKind);
        Assert.Equal(EngineGeneratedAssetProvider.ProviderIdValue, reference.ProviderId);
        Assert.Equal(EngineGeneratedModelCache.CubeAssetId, reference.AssetId);
        Assert.Equal(EngineGeneratedAssetProvider.CubeRelativePath, reference.RelativePath);
    }
}
