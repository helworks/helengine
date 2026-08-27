using System.Reflection;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies project-scoped authoring sessions expose one stable public composition boundary.
/// </summary>
public sealed class EditorProjectAuthoringSessionTests {
    /// <summary>
    /// Ensures an injected host-owned session is returned unchanged by every command-context authoring property.
    /// </summary>
    [Fact]
    public void Authoring_WhenSessionIsInjected_ReturnsTheHostOwnedInstance() {
        string projectRootPath = CreateTemporaryProjectRoot();
        FakeEditorProjectAuthoringSession authoring = new FakeEditorProjectAuthoringSession();
        EditorCommandContext context = new EditorCommandContext(projectRootPath, new ScriptTypeResolver(), (IEditorProjectAuthoringSession)authoring);

        Assert.Same(authoring, context.Authoring);
        Assert.Same(authoring, context.AssetAuthoring);
    }

    /// <summary>
    /// Ensures a session normalizes its project and assets roots before resolving references and rejects use after idempotent disposal.
    /// </summary>
    [Fact]
    public void Session_CanonicalizesRoots_AndDisposeIsIdempotent() {
        string projectRootPath = CreateTemporaryProjectRoot();
        string sourcePath = Path.Combine(projectRootPath, "assets", "models", "ship.obj");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        File.WriteAllText(sourcePath, "o ship");
        File.WriteAllText(sourcePath + ".hmeta", "{\"version\":1,\"assetId\":\"00112233445566778899aabbccddeeff\",\"formerAssetIds\":[]}");
        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets")));

        EditorProjectAuthoringSession session = new EditorProjectAuthoringSession(
            Path.Combine(projectRootPath, "."),
            Array.Empty<IAssetImporterRegistration>(),
            contentManager);

        SceneAssetReference reference = session.CreateReference("models/../models/ship.obj", AssetEntryKind.Model);

        Assert.Equal("models/ship.obj", reference.RelativePath);
        session.Dispose();
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.RefreshExternalChanges());
    }

    /// <summary>
    /// Ensures the session does not claim stable native-write behavior before the dedicated write service task exists.
    /// </summary>
    [Fact]
    public void WriteAsset_BeforeStableWriteService_ThrowsTaskBoundaryException() {
        string projectRootPath = CreateTemporaryProjectRoot();
        AssetImportManager assetImportManager = new AssetImportManager(
            projectRootPath,
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets"))));
        EditorProjectAuthoringSession session = new EditorProjectAuthoringSession(assetImportManager);

        Assert.Throws<NotSupportedException>(() => session.WriteAsset("models/test.hasset", new ModelAsset {
            Id = "Models/Test",
            Positions = Array.Empty<float3>(),
            Normals = Array.Empty<float3>(),
            TexCoords = Array.Empty<float2>(),
            Indices16 = Array.Empty<ushort>(),
            Indices32 = Array.Empty<uint>(),
            Submeshes = Array.Empty<ModelSubmeshAsset>()
        }));
    }

    /// <summary>
    /// Ensures transaction creation remains an explicit task boundary until recoverable publication is implemented.
    /// </summary>
    [Fact]
    public void BeginTransaction_BeforeRecoverableTransactionService_ThrowsTaskBoundaryException() {
        string projectRootPath = CreateTemporaryProjectRoot();
        AssetImportManager assetImportManager = new AssetImportManager(
            projectRootPath,
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets"))));
        EditorProjectAuthoringSession session = new EditorProjectAuthoringSession(assetImportManager);

        Assert.Throws<NotSupportedException>(() => session.BeginTransaction());
    }

    /// <summary>
    /// Ensures session-owned disposable state is released exactly once despite repeated host disposal calls.
    /// </summary>
    [Fact]
    public void Dispose_ReleasesOwnedLifetimeExactlyOnce() {
        string projectRootPath = CreateTemporaryProjectRoot();
        AssetImportManager assetImportManager = new AssetImportManager(
            projectRootPath,
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets"))));
        CountingSessionLifetime lifetime = new CountingSessionLifetime();
        EditorProjectAuthoringSession session = new EditorProjectAuthoringSession(
            assetImportManager,
            new EditorAssetHashCache(projectRootPath),
            lifetime);

        session.Dispose();
        session.Dispose();

        Assert.Equal(1, lifetime.DisposeCount);
    }

    /// <summary>
    /// Ensures the host-facing session does not expose the borrowed import-manager constructor publicly.
    /// </summary>
    [Fact]
    public void AssetImportManagerConstructor_IsInternalToTheEditorHost() {
        ConstructorInfo constructor = typeof(EditorProjectAuthoringSession).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            new[] { typeof(AssetImportManager) },
            null);

        Assert.NotNull(constructor);
        Assert.False(constructor.IsPublic);
    }

    /// <summary>
    /// Creates one isolated project root for the session contract tests.
    /// </summary>
    /// <returns>Absolute temporary project root path.</returns>
    static string CreateTemporaryProjectRoot() {
        string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-authoring-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRootPath);
        return projectRootPath;
    }

    /// <summary>
    /// Supplies the complete public session contract without constructing any editor internals.
    /// </summary>
    sealed class FakeEditorProjectAuthoringSession : IEditorProjectAuthoringSession, IEditorProjectAssetAuthoringService {
        /// <summary>
        /// Creates an empty fake session for command-context identity assertions.
        /// </summary>
        public FakeEditorProjectAuthoringSession() {
            RepairReport = new EditorAssetRepairReport();
        }

        /// <summary>
        /// Gets the empty repair report surfaced by the fake.
        /// </summary>
        public EditorAssetRepairReport RepairReport { get; }

        /// <summary>
        /// Creates no reference because this fake only tests object identity.
        /// </summary>
        public SceneAssetReference CreateReference(string relativePath, AssetEntryKind expectedKind) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Resolves no reference because this fake only tests object identity.
        /// </summary>
        public AssetReferenceResolution ResolveReference(SceneAssetReference reference, AssetEntryKind expectedKind) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Loads no imported model because this fake only tests object identity.
        /// </summary>
        public RuntimeModel LoadImportedRuntimeModel(string relativePath) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes no asset because this fake only tests object identity.
        /// </summary>
        public EditorAssetWriteResult WriteAsset(string relativePath, Asset asset) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Begins no transaction because this fake only tests object identity.
        /// </summary>
        public EditorAuthoringTransaction BeginTransaction() {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Performs no refresh because this fake only tests object identity.
        /// </summary>
        public void RefreshExternalChanges() {
        }

        /// <summary>
        /// Releases no owned resources because this fake only tests object identity.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Loads no typed texture settings because this fake only implements the transitional service surface.
        /// </summary>
        public TextureAssetImportSettings LoadOrCreateTextureImportSettings(string sourcePath) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Saves no typed texture settings because this fake only implements the transitional service surface.
        /// </summary>
        public void SaveTextureImportSettings(string sourcePath, TextureAssetImportSettings settings) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Loads no typed model settings because this fake only implements the transitional service surface.
        /// </summary>
        public ModelAssetImportSettings LoadOrCreateModelImportSettings(string sourcePath) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Loads no typed audio settings because this fake only implements the transitional service surface.
        /// </summary>
        public AudioAssetImportSettings LoadOrCreateAudioImportSettings(string sourcePath) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Loads no sectioned settings because this fake only implements the transitional service surface.
        /// </summary>
        public AssetImportSettings LoadOrCreateSectionedImportSettings(string sourcePath) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Saves no typed model settings because this fake only implements the transitional service surface.
        /// </summary>
        public void SaveModelImportSettings(string sourcePath, ModelAssetImportSettings settings) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Saves no typed audio settings because this fake only implements the transitional service surface.
        /// </summary>
        public void SaveAudioImportSettings(string sourcePath, AudioAssetImportSettings settings) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Saves no sectioned settings because this fake only implements the transitional service surface.
        /// </summary>
        public void SaveSectionedImportSettings(string sourcePath, AssetImportSettings settings) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Resolves no runtime model because this fake only implements the transitional service surface.
        /// </summary>
        public RuntimeModel ResolveRuntimeModel(string sourcePath) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Resolves no font because this fake only implements the transitional service surface.
        /// </summary>
        public FontAsset ResolveFontAsset(string sourcePath) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Resolves no texture because this fake only implements the transitional service surface.
        /// </summary>
        public TextureAsset ResolveTextureAsset(string sourcePath) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Creates no scene resolver because this fake only implements the transitional service surface.
        /// </summary>
        public ISceneAssetReferenceResolver CreateSceneAssetReferenceResolver() {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes no native asset because this fake only implements the transitional service surface.
        /// </summary>
        public void WriteNativeAsset(string relativePath, Asset asset) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes no explicitly identified native asset because this fake only implements the transitional service surface.
        /// </summary>
        public void WriteNativeAsset(string relativePath, Asset asset, string authoringAssetId) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes no native scene because this fake only implements the transitional service surface.
        /// </summary>
        public void WriteNativeScene(string relativePath, SceneSettingsAsset sceneSettings, Entity[] roots, ComponentPersistenceRegistry persistenceRegistry, string authoringAssetId) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Canonicalizes no component state because this fake only implements the transitional service surface.
        /// </summary>
        public bool CanonicalizeAssetReferences(Component component, EntityComponentSaveState saveState) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes no native blueprint because this fake only implements the transitional service surface.
        /// </summary>
        public void WriteNativeBlueprint(string relativePath, ComponentPersistenceRegistry persistenceRegistry) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes no explicitly identified native blueprint because this fake only implements the transitional service surface.
        /// </summary>
        public void WriteNativeBlueprint(string relativePath, ComponentPersistenceRegistry persistenceRegistry, string authoringAssetId) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes no generated cache asset because this fake only implements the transitional service surface.
        /// </summary>
        public void WriteGeneratedCacheAsset(string relativePath, Asset asset) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes no native material because this fake only implements the transitional service surface.
        /// </summary>
        public void WriteNativeMaterial(string relativePath, GeneratedMaterialAssetDefinition definition) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes no explicitly identified native material because this fake only implements the transitional service surface.
        /// </summary>
        public void WriteNativeMaterial(string relativePath, GeneratedMaterialAssetDefinition definition, string authoringAssetId) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Creates no file reference because this fake only implements the transitional service surface.
        /// </summary>
        public SceneAssetReference CreateFileReference(string relativePath, AssetEntryKind expectedKind) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Loads no native asset because this fake only implements the transitional service surface.
        /// </summary>
        public TAsset LoadNativeAsset<TAsset>(string relativePath) where TAsset : Asset {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Loads no imported texture because this fake only implements the transitional service surface.
        /// </summary>
        public bool TryLoadImportedTextureAsset(string assetId, out TextureAsset textureAsset) {
            textureAsset = null;
            return false;
        }

        /// <summary>
        /// Returns no project platform identifiers because this fake only implements the transitional service surface.
        /// </summary>
        public IReadOnlyList<string> GetSupportedPlatformIds() {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Counts disposal calls made through the session's internal owned-resource seam.
    /// </summary>
    sealed class CountingSessionLifetime : IEditorAuthoringSessionLifetime {
        /// <summary>
        /// Gets the number of times the lifetime was released.
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <summary>
        /// Records one lifetime release.
        /// </summary>
        public void Dispose() {
            DisposeCount++;
        }
    }
}
