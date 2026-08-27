using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the public asset-authoring capability supplied to project-authored editor commands.
/// </summary>
public sealed class EditorProjectAssetAuthoringServiceTests {
    /// <summary>
    /// Ensures project bootstrap exposes only the disposable session factory and cannot create an
    /// unowned project capability with a deferred identity cache.
    /// </summary>
    [Fact]
    public void AuthoringFactory_ExposesOnlyDisposableSessionCreation() {
        Type factoryType = typeof(EditorProjectAssetAuthoringServiceFactory);

        Assert.Contains(typeof(IEditorProjectAuthoringSessionFactory), factoryType.GetInterfaces());
        Assert.DoesNotContain(
            factoryType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic),
            method => method.Name == "Create" && typeof(IEditorProjectAssetAuthoringService).IsAssignableFrom(method.ReturnType));
        Assert.NotNull(factoryType.GetMethod(nameof(EditorProjectAssetAuthoringServiceFactory.CreateSession)));
    }

    /// <summary>
    /// Ensures a command context preserves the host-owned asset-authoring capability for project code.
    /// </summary>
    [Fact]
    public void EditorCommandContext_WhenCapabilityIsInjected_ExposesTheSameCapability() {
        string projectRootPath = CreateTemporaryProjectRoot();
        IEditorProjectAuthoringSession session = new EditorProjectAssetAuthoringServiceFactory(Array.Empty<IAssetImporterRegistration>()).CreateSession(projectRootPath);
        EditorCommandContext context = new EditorCommandContext(projectRootPath, new ScriptTypeResolver(), session);

        Assert.Same(session, context.Authoring);
        session.Dispose();
    }

    /// <summary>
    /// Ensures the explicit session factory path returns the disposable current project session.
    /// </summary>
    [Fact]
    public void Factory_CreateSession_ReturnsDisposableProjectSession() {
        string projectRootPath = CreateTemporaryProjectRoot();
        try {
            IEditorProjectAuthoringSession session = new EditorProjectAssetAuthoringServiceFactory(Array.Empty<IAssetImporterRegistration>()).CreateSession(projectRootPath);

            Assert.IsType<EditorProjectAuthoringSession>(session);
            Assert.IsAssignableFrom<IDisposable>(session);
            session.Dispose();
        } finally {
            DeleteTemporaryProjectRoot(projectRootPath);
        }
    }

    /// <summary>
    /// Ensures typed texture settings written through the public capability are current and byte-stable on repeat.
    /// </summary>
    [Fact]
    public void SaveTextureImportSettings_WhenRepeated_ProducesCurrentByteStableSettings() {
        string projectRootPath = CreateTemporaryProjectRoot();
        IEditorProjectAssetAuthoringService capability = CreateCapability(projectRootPath);
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

        _ = CreateCapability(projectRootPath, new[] { registration });

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
        IEditorProjectAssetAuthoringService capability = CreateCapability(projectRootPath);
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
    /// Ensures project-authored native output is byte-stable when the caller supplies its stable identity explicitly.
    /// </summary>
    [Fact]
    public void NativeAssetAuthoring_WithExplicitIdentity_IsByteStableAcrossFreshProjectRoots() {
        const string relativePath = "models/PublicApiStableModel.hasset";
        const string authoringAssetId = "00112233445566778899aabbccddeeff";
        string firstProjectRootPath = CreateTemporaryProjectRoot();
        string secondProjectRootPath = CreateTemporaryProjectRoot();
        IEditorProjectAssetAuthoringService firstCapability = CreateCapability(firstProjectRootPath);
        IEditorProjectAssetAuthoringService secondCapability = CreateCapability(secondProjectRootPath);

        firstCapability.WriteNativeAsset(relativePath, CreatePublicApiModel(), authoringAssetId);
        secondCapability.WriteNativeAsset(relativePath, CreatePublicApiModel(), authoringAssetId);

        byte[] firstBytes = File.ReadAllBytes(Path.Combine(firstProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar)));
        byte[] secondBytes = File.ReadAllBytes(Path.Combine(secondProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Equal(firstBytes, secondBytes);
        using MemoryStream stream = new MemoryStream(firstBytes);
        ModelAsset restored = Assert.IsType<ModelAsset>(AssetSerializer.Deserialize(stream));
        Assert.Equal(authoringAssetId, restored.AuthoringAssetId);
        Assert.False(File.Exists(Path.Combine(firstProjectRootPath, "assets", relativePath + ".hmeta")));
        Assert.False(File.Exists(Path.Combine(secondProjectRootPath, "assets", relativePath + ".hmeta")));
    }

    /// <summary>
    /// Ensures live scene definitions are persisted through the same public native authoring boundary as detached assets.
    /// </summary>
    [Fact]
    public void NativeSceneAuthoring_WritesCurrentSceneWithExplicitIdentity() {
        string projectRootPath = CreateTemporaryProjectRoot();
        IEditorProjectAssetAuthoringService capability = CreateCapability(projectRootPath);
        SceneAsset scene = new SceneAsset {
            Id = "scenes/PublicApiScene.helen",
            RootEntities = Array.Empty<SceneEntityAsset>(),
            AssetReferences = Array.Empty<SceneAssetReference>()
        };
        ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();

        capability.WriteNativeScene("scenes/PublicApiScene.helen", new SceneSettingsAsset(), Array.Empty<Entity>(), registry, "00112233445566778899aabbccddeeff");
        SceneAsset restored = capability.LoadNativeAsset<SceneAsset>("scenes/PublicApiScene.helen");

        Assert.Equal("00112233445566778899aabbccddeeff", restored.AuthoringAssetId);
        Assert.Equal("scenes/PublicApiScene.helen", restored.Id);
    }

    /// <summary>
    /// Ensures blueprint authoring is exposed by the same public capability instead of requiring
    /// project code to construct the editor blueprint save pipeline.
    /// </summary>
    [Fact]
    public void NativeBlueprintAuthoring_IsExposedByThePublicCapability() {
        Assert.Contains(
            typeof(IEditorProjectAssetAuthoringService).GetMethods(),
            method => method.Name == nameof(IEditorProjectAssetAuthoringService.WriteNativeBlueprint));
    }

    /// <summary>
    /// Ensures generated runtime cache output is also available through the public capability.
    /// </summary>
    [Fact]
    public void GeneratedCacheAuthoring_IsExposedByThePublicCapability() {
        Assert.NotNull(typeof(IEditorProjectAssetAuthoringService).GetMethod(nameof(IEditorProjectAssetAuthoringService.WriteGeneratedCacheAsset)));
    }

    /// <summary>
    /// Ensures project tools can canonicalize component references without constructing editor services.
    /// </summary>
    [Fact]
    public void ReferenceCanonicalization_IsExposedByThePublicCapability() {
        Assert.NotNull(typeof(IEditorProjectAssetAuthoringService).GetMethod(nameof(IEditorProjectAssetAuthoringService.CanonicalizeAssetReferences)));
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

    /// <summary>
    /// Creates the lower-level authoring capability directly for tests that target that surface.
    /// </summary>
    /// <param name="projectRootPath">Project root used by the capability.</param>
    /// <param name="importers">Optional importer registrations for the test host.</param>
    /// <returns>Directly composed authoring capability.</returns>
    static EditorProjectAssetAuthoringService CreateCapability(
        string projectRootPath,
        IReadOnlyList<IAssetImporterRegistration> importers = null) {
        string fullProjectRootPath = Path.GetFullPath(projectRootPath);
        string assetsRootPath = Path.Combine(fullProjectRootPath, "assets");
        Directory.CreateDirectory(assetsRootPath);
        AssetImportManager assetImportManager = new AssetImportManager(
            fullProjectRootPath,
            new ContentManager(new HostFileSystemContentStreamSource(assetsRootPath)));
        IReadOnlyList<IAssetImporterRegistration> registrations = importers ?? Array.Empty<IAssetImporterRegistration>();
        for (int index = 0; index < registrations.Count; index++) {
            registrations[index].Register(assetImportManager);
        }

        assetImportManager.GenerateMissingImportSettings();
        return new EditorProjectAssetAuthoringService(assetImportManager);
    }

    /// <summary>
    /// Deletes one temporary project root created by a factory test.
    /// </summary>
    /// <param name="projectRootPath">Temporary project root path.</param>
    static void DeleteTemporaryProjectRoot(string projectRootPath) {
        if (Directory.Exists(projectRootPath)) {
            Directory.Delete(projectRootPath, true);
        }
    }

    /// <summary>
    /// Creates the empty deterministic model payload used by native authoring tests.
    /// </summary>
    /// <returns>Model asset with no geometry and deterministic metadata.</returns>
    static ModelAsset CreatePublicApiModel() {
        return new ModelAsset {
            Id = "Models/PublicApiStableModel",
            Positions = Array.Empty<float3>(),
            Normals = Array.Empty<float3>(),
            TexCoords = Array.Empty<float2>(),
            Indices16 = Array.Empty<ushort>(),
            Indices32 = Array.Empty<uint>(),
            Submeshes = Array.Empty<ModelSubmeshAsset>()
        };
    }
}
