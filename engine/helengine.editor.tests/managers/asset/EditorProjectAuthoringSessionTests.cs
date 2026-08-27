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
        EditorProjectAuthoringSession authoring = TrackSession(new EditorProjectAuthoringSession(CreateAssetImportManager(projectRootPath)));

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
    /// Ensures the session does not claim stable native-write behavior before the dedicated write service task exists.
    /// </summary>
    [Fact]
    public void WriteAsset_BeforeStableWriteService_ThrowsTaskBoundaryException() {
        string projectRootPath = CreateTemporaryProjectRoot();
        EditorProjectAuthoringSession session = TrackSession(new EditorProjectAuthoringSession(CreateAssetImportManager(projectRootPath)));

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
        EditorProjectAuthoringSession session = TrackSession(new EditorProjectAuthoringSession(CreateAssetImportManager(projectRootPath)));

        Assert.Throws<NotSupportedException>(() => session.BeginTransaction());
    }

    /// <summary>
    /// Ensures session-owned disposable state is released exactly once despite repeated host disposal calls.
    /// </summary>
    [Fact]
    public void Dispose_ReleasesOwnedLifetimeExactlyOnce() {
        string projectRootPath = CreateTemporaryProjectRoot();
        CountingSessionLifetime lifetime = new CountingSessionLifetime();
        EditorProjectAuthoringSession session = TrackSession(new EditorProjectAuthoringSession(
            CreateAssetImportManager(projectRootPath),
            new EditorAssetHashCache(projectRootPath),
            lifetime));

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
