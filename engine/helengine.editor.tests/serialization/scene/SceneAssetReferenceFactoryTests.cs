using System.Reflection;
using Xunit;
using helengine.editor;

namespace helengine.editor.tests.serialization.scene;

/// <summary>
/// Verifies constrained scene asset reference construction.
/// </summary>
public sealed class SceneAssetReferenceFactoryTests {
    /// <summary>Ensures current editor component fields reject packaged path-only filesystem references.</summary>
    [Fact]
    public void ReadOptionalReference_WhenCurrentFieldIsPathOnly_Throws() {
        using MemoryStream stream = new MemoryStream();
        using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian, true)) {
            SceneComponentBinaryFieldEncoding.WriteOptionalReference(
                writer,
                global::helengine.SceneAssetReferenceFactory.CreateFileSystemTexture("textures/test.png"));
        }
        stream.Position = 0;
        using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);

        Assert.Throws<ArgumentException>(() => SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader));
    }

    /// <summary>Ensures packaged component fields retain their explicit path-only contract.</summary>
    [Fact]
    public void ReadOptionalReference_WhenPackagedFieldIsPathOnly_ReturnsReference() {
        using MemoryStream stream = new MemoryStream();
        using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian, true)) {
            writer.WriteByte(1);
            writer.WriteInt32((int)SceneAssetReferenceSourceKind.FileSystem);
            writer.WriteString("textures/test.png");
            writer.WriteString(string.Empty);
            writer.WriteString(string.Empty);
            writer.WriteString(string.Empty);
        }
        stream.Position = 0;
        using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);

        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);

        Assert.Equal("textures/test.png", reference.RelativePath);
        Assert.Empty(reference.AssetId);
        Assert.Empty(reference.ContentHash);
    }
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
        Assert.False(typeof(SceneAssetReference).GetProperty(nameof(SceneAssetReference.ContentHash))?.CanWrite ?? true);
        Assert.Null(typeof(global::helengine.SceneAssetReferenceFactory).GetMethod("Rehydrate", BindingFlags.Public | BindingFlags.Static));
    }

    /// <summary>
    /// Ensures a file-backed reference preserves its stable identity and content hash alongside its path.
    /// </summary>
    [Fact]
    public void CreateFileSystemReference_ReturnsStableIdentityPathAndHash() {
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Textures/Shared.png",
            "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

        Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, reference.SourceKind);
        Assert.Equal("00112233445566778899aabbccddeeff", reference.AssetId);
        Assert.Equal("Textures/Shared.png", reference.RelativePath);
        Assert.Equal("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", reference.ContentHash);
        Assert.Equal(string.Empty, reference.ProviderId);
    }

    /// <summary>
    /// Ensures invalid stable identity and hash values are rejected before a reference can be persisted.
    /// </summary>
    [Fact]
    public void CreateFileSystemReference_RejectsInvalidIdentityAndHash() {
        Assert.Throws<ArgumentException>(() => global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "not-a-guid",
            "Textures/Shared.png",
            "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"));
        Assert.Throws<ArgumentException>(() => global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Textures/Shared.png",
            "sha256:bad"));
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
        Assert.Equal(string.Empty, reference.ContentHash);
    }

    /// <summary>
    /// Ensures the only nested component reference encoding carries the canonical content hash.
    /// </summary>
    [Fact]
    public void SceneComponentReferenceEncoding_RoundTripsContentHash() {
        SceneAssetReference original = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Textures/Shared.png",
            "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        using MemoryStream stream = new MemoryStream();
        using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian, true)) {
            SceneComponentBinaryFieldEncoding.WriteOptionalReference(writer, original);
        }

        stream.Position = 0;
        using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian, true);
        SceneAssetReference roundTrip = SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader);

        Assert.Equal(original.AssetId, roundTrip.AssetId);
        Assert.Equal(original.RelativePath, roundTrip.RelativePath);
        Assert.Equal(original.ContentHash, roundTrip.ContentHash);
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
