using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the public asset-authoring capability supplied to project-authored editor commands.
/// </summary>
public sealed class EditorProjectAssetAuthoringServiceTests {
    /// <summary>
    /// Ensures a command context preserves the host-owned asset-authoring capability for project code.
    /// </summary>
    [Fact]
    public void EditorCommandContext_WhenCapabilityIsInjected_ExposesTheSameCapability() {
        string projectRootPath = CreateTemporaryProjectRoot();
        IEditorProjectAssetAuthoringService capability = new EditorProjectAssetAuthoringServiceFactory(Array.Empty<IAssetImporterRegistration>()).Create(projectRootPath);
        EditorCommandContext context = new EditorCommandContext(projectRootPath, new ScriptTypeResolver(), capability);

        Assert.Same(capability, context.AssetAuthoring);
    }

    /// <summary>
    /// Ensures typed texture settings written through the public capability are current and byte-stable on repeat.
    /// </summary>
    [Fact]
    public void SaveTextureImportSettings_WhenRepeated_ProducesCurrentByteStableSettings() {
        string projectRootPath = CreateTemporaryProjectRoot();
        IEditorProjectAssetAuthoringService capability = new EditorProjectAssetAuthoringServiceFactory(Array.Empty<IAssetImporterRegistration>()).Create(projectRootPath);
        string sourcePath = Path.Combine(projectRootPath, "assets", "textures", "sample.png");
        TextureAssetImportSettings settings = new TextureAssetImportSettings();
        settings.Importer.ImporterId = "fixture-texture";
        settings.Importer.AssetId = "sample-texture";

        capability.SaveTextureImportSettings(sourcePath, settings);
        byte[] firstBytes = File.ReadAllBytes(sourcePath + ".hasset");
        capability.SaveTextureImportSettings(sourcePath, settings);
        byte[] secondBytes = File.ReadAllBytes(sourcePath + ".hasset");

        Assert.Equal(firstBytes, secondBytes);
        using MemoryStream stream = new MemoryStream(secondBytes);
        TextureAssetImportSettings restored = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);
        Assert.Equal(TextureAssetImportSettingsBinarySerializer.CurrentVersion, stream.ToArray()[5]);
        Assert.Equal("sample-texture", restored.Importer.AssetId);
    }

    /// <summary>
    /// Ensures creating the host capability does not eagerly import every source asset in the project.
    /// </summary>
    [Fact]
    public void Create_WhenTextureSourceExists_WritesSettingsWithoutEagerCacheImport() {
        string projectRootPath = CreateTemporaryProjectRoot();
        string sourcePath = Path.Combine(projectRootPath, "assets", "textures", "sample.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
        TextureImporterRegistration registration = new TextureImporterRegistration(
            "fixture-texture",
            new ConstantTextureImporter(),
            new[] { ".png" });

        _ = new EditorProjectAssetAuthoringServiceFactory(new[] { registration }).Create(projectRootPath);

        Assert.True(File.Exists(sourcePath + ".hasset"));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(projectRootPath, "cache"), "*", SearchOption.AllDirectories));
    }

    /// <summary>
    /// Ensures project code can write a native asset and create its canonical reference without
    /// constructing an editor writer, resolver, or project-path singleton.
    /// </summary>
    [Fact]
    public void NativeAssetAuthoring_WritesAndReferencesThroughOnePublicCapability() {
        string projectRootPath = CreateTemporaryProjectRoot();
        IEditorProjectAssetAuthoringService capability = new EditorProjectAssetAuthoringServiceFactory(Array.Empty<IAssetImporterRegistration>()).Create(projectRootPath);
        ModelAsset model = new ModelAsset {
            Id = "Models/PublicApiModel",
            Positions = Array.Empty<float3>(),
            Normals = Array.Empty<float3>(),
            TexCoords = Array.Empty<float2>(),
            Indices16 = Array.Empty<ushort>(),
            Indices32 = Array.Empty<uint>(),
            Submeshes = Array.Empty<ModelSubmeshAsset>()
        };

        capability.WriteNativeAsset("models/PublicApiModel.hasset", model);
        SceneAssetReference reference = capability.CreateFileReference("models/PublicApiModel.hasset", AssetEntryKind.Model);

        Assert.Equal(model.AuthoringAssetId, reference.AssetId);
        Assert.Equal("models/PublicApiModel.hasset", reference.RelativePath);
        Assert.StartsWith("sha256:", reference.ContentHash, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(projectRootPath, "assets", "models", "PublicApiModel.hasset.hmeta")));
    }

    /// <summary>
    /// Creates an isolated project root for a capability test.
    /// </summary>
    /// <returns>New temporary project root path.</returns>
    static string CreateTemporaryProjectRoot() {
        string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-authoring-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRootPath);
        return projectRootPath;
    }
}
