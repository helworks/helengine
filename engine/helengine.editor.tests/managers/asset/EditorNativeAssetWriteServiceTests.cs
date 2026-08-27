namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies stable, idempotent native asset writes through the project authoring session.
/// </summary>
public sealed class EditorNativeAssetWriteServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used by this test fixture.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one isolated current-format project.
    /// </summary>
    public EditorNativeAssetWriteServiceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-native-write-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    /// <summary>
    /// Removes the isolated project after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures the first native write assigns an embedded identity and creates no identity sidecar.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenDestinationIsNew_CreatesEmbeddedIdentityWithoutSidecar() {
        using IEditorProjectAuthoringSession session = CreateSession();

        EditorAssetWriteResult result = session.WriteAsset("models/TestModel.hasset", CreateModel());

        Assert.Equal(EditorAssetWriteDisposition.Created, result.Disposition);
        Assert.Equal("models/TestModel.hasset", result.RelativePath);
        Assert.Equal(Path.Combine(ProjectRootPath, "assets", "models", "TestModel.hasset"), result.FullPath);
        Assert.Matches("^[0-9a-f]{32}$", result.AssetId);
        Assert.Matches("^sha256:[0-9a-f]{64}$", result.ContentHash);
        Assert.False(result.PreservedExistingIdentity);
        Assert.True(File.Exists(result.FullPath));
        Assert.False(File.Exists(result.FullPath + ".hmeta"));
        Assert.Equal(result.AssetId, ReadModel(result.FullPath).AuthoringAssetId);
    }

    /// <summary>
    /// Ensures an equivalent fresh object preserves the destination identity and timestamp without replacement.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenEquivalentDestinationExists_IsUnchangedAndPreservesTimestamp() {
        using IEditorProjectAuthoringSession session = CreateSession();

        EditorAssetWriteResult first = session.WriteAsset("models/TestModel.hasset", CreateModel());
        DateTime timestamp = File.GetLastWriteTimeUtc(first.FullPath);

        EditorAssetWriteResult second = session.WriteAsset("models/TestModel.hasset", CreateModel());

        Assert.Equal(first.AssetId, second.AssetId);
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Equal(EditorAssetWriteDisposition.Unchanged, second.Disposition);
        Assert.True(second.PreservedExistingIdentity);
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(second.FullPath));
    }

    /// <summary>
    /// Ensures changed native content preserves the current destination identity and refreshes its recovery hash.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenContentChanges_PreservesIdentityAndReportsChanged() {
        using IEditorProjectAuthoringSession session = CreateSession();

        EditorAssetWriteResult first = session.WriteAsset("models/TestModel.hasset", CreateModel());
        EditorAssetWriteResult second = session.WriteAsset("models/TestModel.hasset", CreateModel(new float3(1f, 2f, 3f)));

        Assert.Equal(first.AssetId, second.AssetId);
        Assert.NotEqual(first.ContentHash, second.ContentHash);
        Assert.Equal(EditorAssetWriteDisposition.Changed, second.Disposition);
        Assert.True(second.PreservedExistingIdentity);
        Assert.Equal(first.AssetId, ReadModel(second.FullPath).AuthoringAssetId);
    }

    /// <summary>
    /// Ensures a valid caller identity is accepted only when it is unowned by another current destination.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenNewDestinationRequestsOwnedIdentity_RejectsDuplicate() {
        const string callerAssetId = "00112233445566778899aabbccddeeff";
        using IEditorProjectAuthoringSession session = CreateSession();

        session.WriteAsset("models/First.hasset", CreateModel(authoringAssetId: callerAssetId));

        Assert.Throws<InvalidOperationException>(() => session.WriteAsset(
            "models/Second.hasset",
            CreateModel(authoringAssetId: callerAssetId)));
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "Second.hasset")));
    }

    /// <summary>
    /// Ensures an invalid caller identity is never persisted and receives a fresh current identity.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenCallerIdentityIsInvalid_MintsFreshIdentity() {
        using IEditorProjectAuthoringSession session = CreateSession();

        EditorAssetWriteResult result = session.WriteAsset(
            "models/InvalidIdentity.hasset",
            CreateModel(authoringAssetId: "not-an-asset-id"));

        Assert.Matches("^[0-9a-f]{32}$", result.AssetId);
        Assert.Equal(result.AssetId, ReadModel(result.FullPath).AuthoringAssetId);
    }

    /// <summary>
    /// Ensures overwriting a current native destination copies its current and former identities before serialization.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenDestinationExists_PreservesCurrentAndFormerIdentities() {
        const string currentAssetId = "00112233445566778899aabbccddeeff";
        const string formerAssetId = "ffeeddccbbaa99887766554433221100";
        using IEditorProjectAuthoringSession session = CreateSession();

        session.WriteAsset("models/TestModel.hasset", CreateModel(
            authoringAssetId: currentAssetId,
            formerAuthoringAssetIds: new[] { formerAssetId }));
        EditorAssetWriteResult result = session.WriteAsset("models/TestModel.hasset", CreateModel(
            authoringAssetId: "abcdefabcdefabcdefabcdefabcdefab"));

        Assert.Equal(currentAssetId, result.AssetId);
        Assert.True(result.PreservedExistingIdentity);
        ModelAsset saved = ReadModel(result.FullPath);
        Assert.Equal(currentAssetId, saved.AuthoringAssetId);
        Assert.Equal(new[] { formerAssetId }, saved.FormerAuthoringAssetIds);
    }

    /// <summary>
    /// Ensures path validation rejects an outside target before creating files or identity metadata.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenTargetEscapesAssetsRoot_RejectsWithoutMutation() {
        using IEditorProjectAuthoringSession session = CreateSession();
        string outsidePath = Path.Combine(ProjectRootPath, "outside.hasset");

        Assert.Throws<InvalidOperationException>(() => session.WriteAsset("../outside.hasset", CreateModel()));

        Assert.False(File.Exists(outsidePath));
        Assert.False(File.Exists(outsidePath + ".hmeta"));
    }

    /// <summary>
    /// Ensures an existing destination without current embedded identity is rejected without replacement.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenExistingDestinationHasNoCurrentIdentity_RejectsWithoutReplacement() {
        using IEditorProjectAuthoringSession session = CreateSession();
        string path = Path.Combine(ProjectRootPath, "assets", "models", "NotCurrent.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        byte[] existingBytes = new byte[] { 1, 2, 3, 4 };
        File.WriteAllBytes(path, existingBytes);

        Assert.Throws<InvalidOperationException>(() => session.WriteAsset("models/NotCurrent.hasset", CreateModel()));

        Assert.Equal(existingBytes, File.ReadAllBytes(path));
    }

    /// <summary>
    /// Creates one session through the public host factory.
    /// </summary>
    /// <returns>Disposable project authoring session.</returns>
    IEditorProjectAuthoringSession CreateSession() {
        return new EditorProjectAssetAuthoringServiceFactory(Array.Empty<IAssetImporterRegistration>()).CreateSession(ProjectRootPath);
    }

    /// <summary>
    /// Creates one deterministic model payload.
    /// </summary>
    /// <param name="position">Optional position used to create changed content.</param>
    /// <param name="authoringAssetId">Optional caller identity.</param>
    /// <param name="formerAuthoringAssetIds">Optional former identity aliases.</param>
    /// <returns>Model asset payload.</returns>
    static ModelAsset CreateModel(
        float3? position = null,
        string authoringAssetId = "",
        string[] formerAuthoringAssetIds = null) {
        return new ModelAsset {
            Id = "Models/TestModel",
            AuthoringAssetId = authoringAssetId,
            FormerAuthoringAssetIds = formerAuthoringAssetIds ?? Array.Empty<string>(),
            Positions = position.HasValue ? new[] { position.Value } : Array.Empty<float3>(),
            Normals = Array.Empty<float3>(),
            TexCoords = Array.Empty<float2>(),
            Indices16 = Array.Empty<ushort>(),
            Indices32 = Array.Empty<uint>(),
            Submeshes = Array.Empty<ModelSubmeshAsset>()
        };
    }

    /// <summary>
    /// Loads one model payload from a current native destination.
    /// </summary>
    /// <param name="path">Absolute model path.</param>
    /// <returns>Decoded model asset.</returns>
    static ModelAsset ReadModel(string path) {
        using FileStream stream = File.OpenRead(path);
        return Assert.IsType<ModelAsset>(AssetSerializer.Deserialize(stream));
    }
}
