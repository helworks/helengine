using System.Reflection;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies project-scoped authoring sessions expose one stable public composition boundary.
/// </summary>
public sealed class EditorProjectAuthoringSessionTests : IDisposable {
    /// <summary>
    /// Temporary roots created by this test fixture.
    /// </summary>
    readonly List<string> TemporaryProjectRoots = new List<string>();

    /// <summary>
    /// Real sessions created by this test fixture.
    /// </summary>
    readonly List<IDisposable> Sessions = new List<IDisposable>();

    [Fact]
    public void ExplicitComposition_RequiresTheSessionOwnedWriter() {
        ConstructorInfo constructor = typeof(EditorProjectAuthoringSession)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(item => item.GetParameters().Length > 0 && item.GetParameters()[0].ParameterType == typeof(AssetImportManager));

        Assert.Equal(typeof(EditorNativeAssetWriteService), constructor.GetParameters().Last().ParameterType);
    }

    /// <summary>
    /// Ensures an injected new-only session is returned unchanged by the command context.
    /// </summary>
    [Fact]
    public void Authoring_WhenSessionIsInjected_ReturnsTheHostOwnedInstance() {
        string projectRootPath = CreateTemporaryProjectRoot();
        FakeEditorProjectAuthoringSession authoring = new FakeEditorProjectAuthoringSession();
        EditorCommandContext context = new EditorCommandContext(projectRootPath, new ScriptTypeResolver(), authoring);

        Assert.Same(authoring, context.Authoring);
    }

    /// <summary>
    /// Ensures a concrete session can be passed to the command context without overload ambiguity or casts.
    /// </summary>
    [Fact]
    public void Authoring_WhenConcreteSessionIsPassed_IsUnambiguous() {
        string projectRootPath = CreateTemporaryProjectRoot();
        EditorProjectAuthoringSession authoring = CreateSession(projectRootPath);

        EditorCommandContext context = new EditorCommandContext(projectRootPath, new ScriptTypeResolver(), authoring);

        Assert.Same(authoring, context.Authoring);
    }

    /// <summary>
    /// Ensures a session normalizes its project and assets roots before resolving references and rejects use after disposal.
    /// </summary>
    [Fact]
    public void Session_CanonicalizesRoots_AndDisposeIsIdempotent() {
        string projectRootPath = CreateTemporaryProjectRoot();
        string sourcePath = Path.Combine(projectRootPath, "assets", "models", "ship.obj");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        File.WriteAllText(sourcePath, "o ship");
        File.WriteAllText(sourcePath + ".hmeta", "{\"version\":1,\"assetId\":\"00112233445566778899aabbccddeeff\",\"formerAssetIds\":[]}");
        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets")));

        EditorProjectAuthoringSession session = TrackSession(new EditorProjectAuthoringSession(
            Path.Combine(projectRootPath, "."),
            Array.Empty<IAssetImporterRegistration>(),
            contentManager));

        SceneAssetReference reference = session.CreateReference("models/../models/ship.obj", AssetEntryKind.Model);

        Assert.Equal("models/ship.obj", reference.RelativePath);
        session.Dispose();
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.RefreshExternalChanges());
    }

    /// <summary>
    /// Ensures the session creates its repair report before the initial identity index reconciliation.
    /// </summary>
    [Fact]
    public void Session_InitialIdentityRepairs_AreAvailableThroughOneSharedReport() {
        string projectRootPath = CreateTemporaryProjectRoot();
        string sourcePath = Path.Combine(projectRootPath, "assets", "models", "reported.obj");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        File.WriteAllText(sourcePath, "o reported");
        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets")));
        EditorProjectAuthoringSession session = TrackSession(new EditorProjectAuthoringSession(
            projectRootPath,
            Array.Empty<IAssetImporterRegistration>(),
            contentManager));

        EditorAssetRepairRecord repair = Assert.Single(session.RepairReport.Records);

        Assert.Equal(EditorAssetRepairKind.MissingExternalMetadataCreation, repair.Kind);
        Assert.Equal("models/reported.obj", repair.RelativePath);
    }

    /// <summary>
    /// Ensures saved-identity adoption is replayed to a session that was already open.
    /// </summary>
    [Fact]
    public void SavedIdAdoption_IsVisibleToPreopenedSessionWithoutFullRefresh() {
        string projectRootPath = CreateTemporaryProjectRoot();
        EditorProjectAuthoringSession observer = CreateSession(projectRootPath);
        string sourcePath = Path.Combine(projectRootPath, "assets", "models", "adopted.obj");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        File.WriteAllText(sourcePath, "o adopted");

        EditorProjectAuthoringSession adopter = CreateSession(projectRootPath);
        Assert.True(adopter.IdentityIndexValue.WasMetadataMissing(sourcePath));
        const string adoptedAssetId = "00112233445566778899aabbccddeeff";
        SceneAssetReference savedReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            adoptedAssetId,
            "models/adopted.obj",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution resolution = adopter.ResolveReference(savedReference, AssetEntryKind.Model);
        SceneAssetReference observedReference = observer.CreateReference("models/adopted.obj", AssetEntryKind.Model);

        Assert.Equal(adoptedAssetId, resolution.CanonicalReference.AssetId);
        Assert.Equal(adoptedAssetId, observedReference.AssetId);
    }

    /// <summary>
    /// Ensures the session routes native writes through its stable writer.
    /// </summary>
    [Fact]
    public void WriteAsset_UsesStableNativeWriter() {
        string projectRootPath = CreateTemporaryProjectRoot();
        EditorProjectAuthoringSession session = CreateSession(projectRootPath);

        EditorAssetWriteResult result = session.WriteAsset("models/test.hasset", new ModelAsset {
            Id = "Models/Test",
            Positions = Array.Empty<float3>(),
            Normals = Array.Empty<float3>(),
            TexCoords = Array.Empty<float2>(),
            Indices16 = Array.Empty<ushort>(),
            Indices32 = Array.Empty<uint>(),
            Submeshes = Array.Empty<ModelSubmeshAsset>()
        });

        Assert.Equal(EditorAssetWriteDisposition.Created, result.Disposition);
        Assert.Equal("models/test.hasset", result.RelativePath);
    }

    /// <summary>
    /// Ensures transaction creation is owned by the project session.
    /// </summary>
    [Fact]
    public void BeginTransaction_CreatesSessionOwnedTransaction() {
        string projectRootPath = CreateTemporaryProjectRoot();
        EditorProjectAuthoringSession session = CreateSession(projectRootPath);

        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        Assert.NotNull(transaction);
    }

    /// <summary>
    /// Ensures a successful native write is visible through the session index without a full rescan.
    /// </summary>
    [Fact]
    public void WriteNativeAsset_RegistersDestinationForImmediateReferenceResolution() {
        string projectRootPath = CreateTemporaryProjectRoot();
        CountingAssetFileCatalog catalog = new CountingAssetFileCatalog();
        EditorAssetHashCache cache = new EditorAssetHashCache(projectRootPath);
        AssetImportManager manager = CreateAssetImportManager(projectRootPath);
        EditorAssetIdentityIndex identityIndex = new EditorAssetIdentityIndex(projectRootPath, null, null, cache, catalog);
        identityIndex.Initialize();
        EditorAssetReferenceResolver referenceResolver = new EditorAssetReferenceResolver(projectRootPath, identityIndex, cache);
        EditorNativeAssetWriteService nativeAssetWriteService = new EditorNativeAssetWriteService(projectRootPath, identityIndex, cache);
        EditorProjectAuthoringSessionResources resources = new EditorProjectAuthoringSessionResources(referenceResolver, identityIndex, cache, nativeAssetWriteService);
        EditorProjectAuthoringSession session = TrackSession(new EditorProjectAuthoringSession(
            manager,
            cache,
            identityIndex,
            referenceResolver,
            new EditorAuthoringSessionLifetime(resources),
            nativeAssetWriteService));
        const string assetId = "00112233445566778899aabbccddeeff";

        session.WriteNativeAsset("Models/Written.hasset", CreateModelAsset(), assetId);

        Assert.Equal(1, catalog.EnumerationCount);
        SceneAssetReference persistedReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            assetId,
            "Models/Written.hasset",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
        AssetReferenceResolution resolution = session.ResolveReference(persistedReference, AssetEntryKind.Model);

        Assert.Equal(assetId, resolution.CanonicalReference.AssetId);
        Assert.Equal("Models/Written.hasset", resolution.CanonicalReference.RelativePath);
        Assert.Equal(1, catalog.EnumerationCount);
    }

    /// <summary>
    /// Ensures session-owned disposable state is released exactly once despite repeated host disposal calls.
    /// </summary>
    [Fact]
    public void Dispose_ReleasesOwnedLifetimeExactlyOnce() {
        string projectRootPath = CreateTemporaryProjectRoot();
        EditorAssetHashCache cache = new EditorAssetHashCache(projectRootPath);
        EditorAssetIdentityIndex identityIndex = new EditorAssetIdentityIndex(projectRootPath, null, null, cache);
        identityIndex.Initialize();
        EditorAssetReferenceResolver referenceResolver = new EditorAssetReferenceResolver(projectRootPath, identityIndex, cache);
        EditorNativeAssetWriteService nativeAssetWriteService = new EditorNativeAssetWriteService(projectRootPath, identityIndex, cache);
        CountingSessionLifetime lifetime = new CountingSessionLifetime(new EditorProjectAuthoringSessionResources(referenceResolver, identityIndex, cache, nativeAssetWriteService));
        EditorProjectAuthoringSession session = TrackSession(new EditorProjectAuthoringSession(
            CreateAssetImportManager(projectRootPath),
            cache,
            identityIndex,
            referenceResolver,
            lifetime,
            nativeAssetWriteService));

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

        Assert.Null(constructor);
    }

    /// <summary>
    /// Disposes every real session and removes every temporary root created by this fixture.
    /// </summary>
    public void Dispose() {
        for (int index = 0; index < Sessions.Count; index++) {
            Sessions[index].Dispose();
        }

        for (int index = 0; index < TemporaryProjectRoots.Count; index++) {
            string projectRootPath = TemporaryProjectRoots[index];
            if (Directory.Exists(projectRootPath)) {
                Directory.Delete(projectRootPath, true);
            }
        }
    }

    /// <summary>
    /// Creates and tracks one isolated project root for the session contract tests.
    /// </summary>
    /// <returns>New temporary project root path.</returns>
    string CreateTemporaryProjectRoot() {
        string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-authoring-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRootPath);
        TemporaryProjectRoots.Add(projectRootPath);
        return projectRootPath;
    }

    /// <summary>
    /// Tracks one real session for fixture cleanup.
    /// </summary>
    /// <param name="session">Session to track.</param>
    /// <returns>The same session for inline construction.</returns>
    EditorProjectAuthoringSession TrackSession(EditorProjectAuthoringSession session) {
        Sessions.Add(session);
        return session;
    }

    /// <summary>
    /// Creates one host import manager for a session test.
    /// </summary>
    /// <param name="projectRootPath">Project root path.</param>
    /// <returns>Host import manager.</returns>
    static AssetImportManager CreateAssetImportManager(string projectRootPath) {
        return new AssetImportManager(
            projectRootPath,
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets"))));
    }

    /// <summary>
    /// Composes explicitly owned project services for one session test.
    /// </summary>
    /// <param name="projectRootPath">Temporary project root.</param>
    /// <returns>Tracked session with an owned resource lifetime.</returns>
    EditorProjectAuthoringSession CreateSession(string projectRootPath) {
        EditorAssetHashCache cache = new EditorAssetHashCache(projectRootPath);
        EditorAssetIdentityIndex identityIndex = new EditorAssetIdentityIndex(projectRootPath, null, null, cache);
        identityIndex.Initialize();
        EditorAssetReferenceResolver referenceResolver = new EditorAssetReferenceResolver(projectRootPath, identityIndex, cache);
        EditorNativeAssetWriteService nativeAssetWriteService = new EditorNativeAssetWriteService(projectRootPath, identityIndex, cache);
        EditorProjectAuthoringSessionResources resources = new EditorProjectAuthoringSessionResources(referenceResolver, identityIndex, cache, nativeAssetWriteService);
        return TrackSession(new EditorProjectAuthoringSession(
            CreateAssetImportManager(projectRootPath),
            cache,
            identityIndex,
            referenceResolver,
            new EditorAuthoringSessionLifetime(resources),
            nativeAssetWriteService));
    }

    /// <summary>
    /// Creates a minimal current native model payload used by write-path tests.
    /// </summary>
    static ModelAsset CreateModelAsset() {
        return new ModelAsset {
            Id = "Models/Written",
            Positions = Array.Empty<float3>(),
            Normals = Array.Empty<float3>(),
            TexCoords = Array.Empty<float2>(),
            Indices16 = Array.Empty<ushort>(),
            Indices32 = Array.Empty<uint>(),
            Submeshes = Array.Empty<ModelSubmeshAsset>()
        };
    }

    /// <summary>
    /// Counts full authored-file enumerations while delegating to the filesystem.
    /// </summary>
    sealed class CountingAssetFileCatalog : IEditorAssetFileCatalog {
        /// <summary>
        /// Gets the number of full authored-file enumerations.
        /// </summary>
        public int EnumerationCount { get; private set; }

        /// <summary>
        /// Enumerates authored files beneath one assets root.
        /// </summary>
        public IEnumerable<string> EnumerateFiles(string assetsRootPath) {
            EnumerationCount++;
            return Directory.EnumerateFiles(assetsRootPath, "*", SearchOption.AllDirectories);
        }
    }

    /// <summary>
    /// Supplies only the current public session contract to test command-context overload resolution.
    /// </summary>
    sealed class FakeEditorProjectAuthoringSession : IEditorProjectAuthoringSession {
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
        /// Releases no resources because this fake only tests object identity.
        /// </summary>
        public void Dispose() {
        }
    }

    /// <summary>
    /// Counts disposal calls made through the session's internal owned-resource seam.
    /// </summary>
    sealed class CountingSessionLifetime : IEditorAuthoringSessionLifetime {
        /// <summary>
        /// Resource graph released at the lifetime boundary.
        /// </summary>
        readonly IDisposable OwnedService;
        /// <summary>
        /// Gets the number of times the lifetime was released.
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <summary>
        /// Initializes a counting lifetime over explicitly owned resources.
        /// </summary>
        /// <param name="ownedService">Resource graph to release.</param>
        public CountingSessionLifetime(IDisposable ownedService) {
            OwnedService = ownedService ?? throw new ArgumentNullException(nameof(ownedService));
        }

        /// <summary>
        /// Records one lifetime release.
        /// </summary>
        public void Dispose() {
            if (DisposeCount != 0) {
                return;
            }

            DisposeCount++;
            OwnedService.Dispose();
        }
    }
}
