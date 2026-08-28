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
    /// Ensures a pre-opened session replays startup identity repairs through the exact-path publication marker.
    /// </summary>
    [Fact]
    public void StartupIdentityRepair_IsVisibleToPreopenedWriterWithoutFullRefresh() {
        string firstPath = CreateExternalAsset("Models/StartupOwner.fbx");
        AssetIdentityMetadataService metadata = new AssetIdentityMetadataService(ProjectRootPath);
        const string duplicateId = "00112233445566778899aabbccddeeff";
        metadata.Save(firstPath, new AssetIdentityMetadataDocument { AssetId = duplicateId });
        using EditorAssetHashCache preopenedCache = new EditorAssetHashCache(ProjectRootPath);
        using EditorAssetIdentityIndex preopenedIndex = new EditorAssetIdentityIndex(ProjectRootPath, null, null, preopenedCache);
        preopenedIndex.Initialize();
        using EditorNativeAssetWriteService preopenedWriter = new EditorNativeAssetWriteService(ProjectRootPath, preopenedIndex, preopenedCache);

        string duplicatePath = CreateExternalAsset("Models/StartupZCopy.fbx");
        File.Copy(firstPath + ".hmeta", duplicatePath + ".hmeta", true);

        using EditorAssetHashCache repairingCache = new EditorAssetHashCache(ProjectRootPath);
        using EditorAssetIdentityIndex repairingIndex = new EditorAssetIdentityIndex(ProjectRootPath, null, null, repairingCache);
        repairingIndex.Initialize();
        Assert.NotNull(repairingIndex.FindByPath("Models/StartupZCopy.fbx"));
        Assert.Contains(EditorProjectWriteGeneration.ReadAfter(ProjectRootPath, 0), change => change.RelativePath == "Models/StartupZCopy.fbx");

        EditorAssetIdentityEntry repaired = preopenedWriter.ExecuteSynchronizedRead(
            () => preopenedIndex.FindByPath("Models/StartupZCopy.fbx"));

        Assert.NotNull(repaired);
        Assert.NotEqual(duplicateId, repaired.AssetId);
        Assert.Contains(duplicateId, repaired.FormerAssetIds);
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
        string path = Path.Combine(ProjectRootPath, "assets", "models", "TestModel.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, AssetSerializer.SerializeToBytes(CreateModel(
            authoringAssetId: currentAssetId,
            formerAuthoringAssetIds: new[] { formerAssetId })));
        using IEditorProjectAuthoringSession session = CreateSession();

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
    /// Ensures invalid embedded identity aliases are rejected before an existing destination is replaced.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenExistingDestinationHasDuplicateFormerIdentity_RejectsWithoutReplacement() {
        using IEditorProjectAuthoringSession session = CreateSession();
        string path = Path.Combine(ProjectRootPath, "assets", "models", "DuplicateFormer.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        byte[] existingBytes = AssetSerializer.SerializeToBytes(CreateModel(
            authoringAssetId: "00112233445566778899aabbccddeeff",
            formerAuthoringAssetIds: new[] {
                "ffeeddccbbaa99887766554433221100",
                "ffeeddccbbaa99887766554433221100"
            }));
        File.WriteAllBytes(path, existingBytes);

        Assert.Throws<InvalidOperationException>(() => session.WriteAsset("models/DuplicateFormer.hasset", CreateModel()));

        Assert.Equal(existingBytes, File.ReadAllBytes(path));
    }

    /// <summary>
    /// Ensures the writer rejects destinations whose extension cannot contain the supplied native asset kind.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenExtensionDoesNotMatchAssetKind_RejectsWithoutMutation() {
        using IEditorProjectAuthoringSession session = CreateSession();
        string wrongPath = Path.Combine(ProjectRootPath, "assets", "scenes", "WrongExtension.hasset");

        Assert.Throws<InvalidOperationException>(() => session.WriteAsset(
            "scenes/WrongExtension.hasset",
            CreateScene()));

        Assert.False(File.Exists(wrongPath));
        Assert.False(File.Exists(wrongPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures external and metadata extensions are not accepted as native destinations.
    /// </summary>
    [Theory]
    [InlineData("models/External.png")]
    [InlineData("models/Hidden.hmeta")]
    [InlineData("models/Unknown.txt")]
    public void WriteAsset_WhenExtensionIsNotCurrentNative_RejectsWithoutMutation(string relativePath) {
        using IEditorProjectAuthoringSession session = CreateSession();

        Assert.Throws<InvalidOperationException>(() => session.WriteAsset(relativePath, CreateModel()));

        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    /// <summary>
    /// Ensures a directory link cannot redirect a native write outside the project assets root.
    /// </summary>
    [DirectoryLinkFact]
    public void WriteAsset_WhenDirectoryLinkEscapesAssetsRoot_RejectsWithoutMutation() {
        string outsideRoot = Path.Combine(Path.GetTempPath(), "helengine-native-write-outside-" + Guid.NewGuid().ToString("N"));
        string linkPath = Path.Combine(ProjectRootPath, "assets", "linked");
        Directory.CreateDirectory(outsideRoot);
        try {
            Directory.CreateSymbolicLink(linkPath, outsideRoot);

            using IEditorProjectAuthoringSession session = CreateSession();
            Assert.Throws<InvalidOperationException>(() => session.WriteAsset("linked/Escaped.hasset", CreateModel()));

            Assert.False(File.Exists(Path.Combine(outsideRoot, "Escaped.hasset")));
            Assert.False(File.Exists(Path.Combine(outsideRoot, "Escaped.hasset.hmeta")));
        } finally {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
            if (Directory.Exists(outsideRoot)) {
                Directory.Delete(outsideRoot, true);
            }
        }
    }

    /// <summary>
    /// Ensures a sibling that differs only by case is not treated as the assets root on case-sensitive filesystems.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenCaseSensitiveSiblingIsOutsideAssetsRoot_RejectsWithoutMutation() {
        if (OperatingSystem.IsWindows()) {
            return;
        }

        string siblingRoot = Path.Combine(ProjectRootPath, "ASSETS");
        Directory.CreateDirectory(siblingRoot);
        using IEditorProjectAuthoringSession session = CreateSession();

        Assert.Throws<InvalidOperationException>(() => session.WriteAsset("../ASSETS/Sibling.hasset", CreateModel()));

        Assert.False(File.Exists(Path.Combine(siblingRoot, "Sibling.hasset")));
    }

    /// <summary>
    /// Ensures caller-supplied former identities are not published for a new destination.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenNewDestinationContainsFormerAliases_ClearsAliasesBeforePublication() {
        using IEditorProjectAuthoringSession session = CreateSession();

        EditorAssetWriteResult result = session.WriteAsset(
            "models/New.hasset",
            CreateModel(
                authoringAssetId: "00112233445566778899aabbccddeeff",
                formerAuthoringAssetIds: new[] { "ffeeddccbbaa99887766554433221100", "ffeeddccbbaa99887766554433221100" }));

        Assert.Empty(ReadModel(result.FullPath).FormerAuthoringAssetIds);
    }

    /// <summary>
    /// Ensures the native writer remains an internal composition detail of the project session.
    /// </summary>
    [Fact]
    public void EditorNativeAssetWriteService_IsNotPublic() {
        Assert.False(typeof(EditorNativeAssetWriteService).IsPublic);
    }

    /// <summary>
    /// Ensures concurrent sessions cannot publish one caller identity to two new destinations.
    /// </summary>
    [Fact]
    public async Task WriteAsset_WhenSessionsRaceForOneCallerIdentity_PublishesAtMostOneDestination() {
        const string callerAssetId = "00112233445566778899aabbccddeeff";
        using IEditorProjectAuthoringSession firstSession = CreateSession();
        using IEditorProjectAuthoringSession secondSession = CreateSession();
        using Barrier startBarrier = new Barrier(2);

        Task<EditorAssetWriteResult> firstWrite = Task.Run(() => {
            startBarrier.SignalAndWait();
            return firstSession.WriteAsset("models/RaceFirst.hasset", CreateModel(authoringAssetId: callerAssetId));
        });
        Task<EditorAssetWriteResult> secondWrite = Task.Run(() => {
            startBarrier.SignalAndWait();
            return secondSession.WriteAsset("models/RaceSecond.hasset", CreateModel(authoringAssetId: callerAssetId));
        });
        Task<EditorAssetWriteResult>[] writes = new[] { firstWrite, secondWrite };

        int successfulWrites = 0;
        for (int index = 0; index < writes.Length; index++) {
            try {
                await writes[index];
                successfulWrites++;
            } catch (InvalidOperationException) {
            }
        }

        Assert.Equal(1, successfulWrites);
        Assert.Single(Directory.GetFiles(Path.Combine(ProjectRootPath, "assets", "models"), "Race*.hasset"));
    }

    /// <summary>
    /// Ensures concurrent first writes to one destination converge on one identity.
    /// </summary>
    [Fact]
    public async Task WriteAsset_WhenSessionsRaceForOneDestination_PreservesOnePublishedIdentity() {
        const string firstAssetId = "00112233445566778899aabbccddeeff";
        const string secondAssetId = "ffeeddccbbaa99887766554433221100";
        using IEditorProjectAuthoringSession firstSession = CreateSession();
        using IEditorProjectAuthoringSession secondSession = CreateSession();
        using Barrier startBarrier = new Barrier(2);

        Task<EditorAssetWriteResult> firstWrite = Task.Run(() => {
            startBarrier.SignalAndWait();
            return firstSession.WriteAsset("models/RaceSame.hasset", CreateModel(authoringAssetId: firstAssetId));
        });
        Task<EditorAssetWriteResult> secondWrite = Task.Run(() => {
            startBarrier.SignalAndWait();
            return secondSession.WriteAsset("models/RaceSame.hasset", CreateModel(authoringAssetId: secondAssetId));
        });
        EditorAssetWriteResult[] writes = await Task.WhenAll(firstWrite, secondWrite);

        ModelAsset saved = ReadModel(Path.Combine(ProjectRootPath, "assets", "models", "RaceSame.hasset"));
        Assert.Contains(saved.AuthoringAssetId, new[] { firstAssetId, secondAssetId });
        Assert.All(writes, write => Assert.Equal(saved.AuthoringAssetId, write.AssetId));
    }

    /// <summary>
    /// Ensures a pre-opened writer replays every intervening path record without a full enumeration.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenSessionMissesMultiplePublications_ReplaysExactPathsWithoutEnumeration() {
        CountingAssetFileCatalog catalog = new CountingAssetFileCatalog();
        using EditorAssetHashCache firstCache = new EditorAssetHashCache(ProjectRootPath);
        using EditorAssetIdentityIndex firstIndex = new EditorAssetIdentityIndex(ProjectRootPath, null, null, firstCache, catalog);
        firstIndex.Initialize();
        using EditorNativeAssetWriteService firstWriter = new EditorNativeAssetWriteService(ProjectRootPath, firstIndex, firstCache);
        using EditorAssetHashCache secondCache = new EditorAssetHashCache(ProjectRootPath);
        using EditorAssetIdentityIndex secondIndex = new EditorAssetIdentityIndex(ProjectRootPath, null, null, secondCache);
        secondIndex.Initialize();
        using EditorNativeAssetWriteService secondWriter = new EditorNativeAssetWriteService(ProjectRootPath, secondIndex, secondCache);

        firstWriter.WriteAsset("models/Initial.hasset", CreateModel());
        string initialPath = Path.Combine(ProjectRootPath, "assets", "models", "Initial.hasset");
        string oldHash = firstCache.GetContentHash(initialPath);
        secondWriter.WriteAsset("models/InterveningA.hasset", CreateModel(new float3(1f, 0f, 0f)));
        secondWriter.WriteAsset("models/InterveningB.hasset", CreateModel(new float3(2f, 0f, 0f)));
        secondWriter.WriteAsset("models/Initial.hasset", CreateModel(new float3(3f, 0f, 0f)));

        firstWriter.WriteAsset("models/Final.hasset", CreateModel(new float3(4f, 0f, 0f)));
        string newHash = firstCache.GetContentHash(initialPath);

        Assert.NotEqual(oldHash, newHash);
        Assert.Equal(1, catalog.EnumerationCount);
        Assert.NotNull(firstIndex.FindByPath("models/InterveningA.hasset"));
        Assert.NotNull(firstIndex.FindByPath("models/InterveningB.hasset"));
    }

    /// <summary>
    /// Ensures a pre-opened session invalidates a cached fingerprint after another session rewrites a file.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenOtherSessionRewritesWithRestoredTimestamp_RecomputesHash() {
        using EditorProjectAuthoringSession firstSession = (EditorProjectAuthoringSession)CreateSession();
        using EditorProjectAuthoringSession secondSession = (EditorProjectAuthoringSession)CreateSession();
        EditorAssetWriteResult first = firstSession.WriteAsset("models/Shared.hasset", CreateModel());
        DateTime timestamp = File.GetLastWriteTimeUtc(first.FullPath);

        secondSession.WriteAsset("models/Shared.hasset", CreateModel(new float3(5f, 0f, 0f)));
        File.SetLastWriteTimeUtc(first.FullPath, timestamp);
        firstSession.WriteAsset("models/Observer.hasset", CreateModel(new float3(6f, 0f, 0f)));

        string observedHash = firstSession.HashCacheValue.GetContentHash(first.FullPath);
        Assert.NotEqual(first.ContentHash, observedHash);
    }

    /// <summary>
    /// Ensures direct use of the session resolver observes another session's publication before hashing.
    /// </summary>
    [Fact]
    public void DirectResolver_WhenOtherSessionRewritesWithRestoredTimestamp_RecomputesHash() {
        using EditorProjectAuthoringSession firstSession = (EditorProjectAuthoringSession)CreateSession();
        using EditorProjectAuthoringSession secondSession = (EditorProjectAuthoringSession)CreateSession();
        EditorAssetWriteResult first = firstSession.WriteAsset("models/DirectResolver.hasset", CreateModel());
        DateTime timestamp = File.GetLastWriteTimeUtc(first.FullPath);
        SceneAssetReference initial = firstSession.ReferenceResolverValue.CreateFileReference(first.FullPath, AssetEntryKind.Model);

        secondSession.WriteAsset("models/DirectResolver.hasset", CreateModel(new float3(7f, 0f, 0f)));
        File.SetLastWriteTimeUtc(first.FullPath, timestamp);

        SceneAssetReference observed = firstSession.ReferenceResolverValue.CreateFileReference(first.FullPath, AssetEntryKind.Model);

        Assert.NotEqual(initial.ContentHash, observed.ContentHash);
    }

    /// <summary>
    /// Ensures a browser manager borrowing a session graph observes exact-path publications before hashing.
    /// </summary>
    [Fact]
    public void BorrowedBrowser_WhenOtherSessionRewritesWithRestoredTimestamp_RecomputesHash() {
        using EditorProjectAuthoringSession firstSession = (EditorProjectAuthoringSession)CreateSession();
        using EditorProjectAuthoringSession secondSession = (EditorProjectAuthoringSession)CreateSession();
        EditorAssetWriteResult first = firstSession.WriteAsset("models/BorrowedBrowser.hasset", CreateModel());
        DateTime timestamp = File.GetLastWriteTimeUtc(first.FullPath);
        using EditorAssetManager browser = new EditorAssetManager(ProjectRootPath, firstSession.ReferenceResolverValue);
        List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();

        Assert.True(browser.TryNavigateTo("models"));
        browser.LoadEntries(entries);
        string initialHash = Assert.Single(entries, entry => entry.RelativePath == "models/BorrowedBrowser.hasset").ContentHash;
        secondSession.WriteAsset("models/BorrowedBrowser.hasset", CreateModel(new float3(8f, 0f, 0f)));
        File.SetLastWriteTimeUtc(first.FullPath, timestamp);

        browser.LoadEntries(entries);

        string observedHash = Assert.Single(entries, entry => entry.RelativePath == "models/BorrowedBrowser.hasset").ContentHash;
        Assert.NotEqual(initialHash, observedHash);
        Assert.NotNull(firstSession.ReferenceResolverValue.CreateFileReference(first.FullPath, AssetEntryKind.Model));
    }

    /// <summary>
    /// Ensures writer construction replays a publication made after the index scan but before writer creation.
    /// </summary>
    [Fact]
    public void WriterConstruction_ReplaysPublishedPathAfterIndexInitialization() {
        string relativePath = "models/ConstructionRace.hasset";
        string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        using EditorAssetHashCache cache = new EditorAssetHashCache(ProjectRootPath);
        using EditorAssetIdentityIndex index = new EditorAssetIdentityIndex(ProjectRootPath, null, null, cache);
        index.Initialize();
        Assert.Null(index.FindByPath(relativePath));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, AssetSerializer.SerializeToBytes(CreateModel(authoringAssetId: "00112233445566778899aabbccddeeff")));
        EditorProjectWriteGeneration.PublishChange(ProjectRootPath, relativePath);

        using EditorNativeAssetWriteService writer = new EditorNativeAssetWriteService(ProjectRootPath, index, cache);

        Assert.NotNull(index.FindByPath(relativePath));
        Assert.Equal(relativePath, index.FindByPath(relativePath).RelativePath);
    }

    /// <summary>
    /// Ensures a direct session reference read replays a same-length external publication before hashing.
    /// </summary>
    [Fact]
    public void CreateReference_WhenOtherSessionRewritesWithRestoredTimestamp_ReplaysBeforeRead() {
        using EditorProjectAuthoringSession firstSession = (EditorProjectAuthoringSession)CreateSession();
        using EditorProjectAuthoringSession secondSession = (EditorProjectAuthoringSession)CreateSession();
        EditorAssetWriteResult first = firstSession.WriteAsset("models/ReadBoundary.hasset", CreateModel());
        DateTime timestamp = File.GetLastWriteTimeUtc(first.FullPath);
        SceneAssetReference initialReference = firstSession.CreateReference("models/ReadBoundary.hasset", AssetEntryKind.Model);

        secondSession.WriteAsset("models/ReadBoundary.hasset", CreateModel(new float3(7f, 0f, 0f)));
        File.SetLastWriteTimeUtc(first.FullPath, timestamp);

        SceneAssetReference observedReference = firstSession.CreateReference("models/ReadBoundary.hasset", AssetEntryKind.Model);

        Assert.NotEqual(initialReference.ContentHash, observedReference.ContentHash);
    }

    /// <summary>
    /// Ensures a post-replacement bookkeeping failure is healed by a newly opened session.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenHashBookkeepingFailsAfterReplacement_NewSessionReplaysAndRehashes() {
        const string relativePath = "models/PostReplacementFailure.hasset";
        using (EditorProjectAuthoringSession seedSession = (EditorProjectAuthoringSession)CreateSession()) {
            seedSession.WriteAsset(relativePath, CreateModel());
        }

        using EditorAssetHashCache failingCache = new EditorAssetHashCache(ProjectRootPath);
        using EditorAssetIdentityIndex failingIndex = new EditorAssetIdentityIndex(ProjectRootPath, null, null, failingCache);
        failingIndex.Initialize();
        using EditorNativeAssetWriteService failingWriter = new EditorNativeAssetWriteService(
            ProjectRootPath,
            failingIndex,
            failingCache,
            new FixedBaselineWriteChangeLog(ProjectRootPath));
        string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        string oldHash = failingCache.GetContentHash(fullPath);
        failingCache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => failingWriter.WriteAsset(relativePath, CreateModel(new float3(8f, 0f, 0f))));

        using EditorProjectAuthoringSession healedSession = (EditorProjectAuthoringSession)CreateSession();
        SceneAssetReference healedReference = healedSession.CreateReference(relativePath, AssetEntryKind.Model);

        Assert.NotEqual(oldHash, healedReference.ContentHash);
    }

    /// <summary>
    /// Ensures a failed exact-path publication leaves the destination untouched.
    /// </summary>
    [Fact]
    public void WriteAsset_WhenChangePublicationFails_DoesNotReplaceDestination() {
        ThrowingWriteChangeLog changeLog = new ThrowingWriteChangeLog();
        using EditorAssetHashCache cache = new EditorAssetHashCache(ProjectRootPath);
        using EditorAssetIdentityIndex index = new EditorAssetIdentityIndex(ProjectRootPath, null, null, cache);
        index.Initialize();
        using EditorNativeAssetWriteService writer = new EditorNativeAssetWriteService(ProjectRootPath, index, cache, changeLog);
        string destinationPath = Path.Combine(ProjectRootPath, "assets", "models", "PublicationFailure.hasset");

        Assert.Throws<IOException>(() => writer.WriteAsset("models/PublicationFailure.hasset", CreateModel()));

        Assert.False(File.Exists(destinationPath));
        Assert.Equal(1, changeLog.PublishCount);
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
    /// Writes one external authored source without creating an identity document.
    /// </summary>
    string CreateExternalAsset(string relativePath) {
        string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, new byte[] { 1, 2, 3 });
        return fullPath;
    }

    /// <summary>
    /// Creates a minimal scene payload for extension validation.
    /// </summary>
    /// <returns>Current scene asset.</returns>
    static SceneAsset CreateScene() {
        return new SceneAsset {
            Id = "Scenes/TestScene",
            RootEntities = Array.Empty<SceneEntityAsset>(),
            AssetReferences = Array.Empty<SceneAssetReference>(),
            SceneSettings = new SceneSettingsAsset()
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

    /// <summary>
    /// Counts explicit full authored-file enumerations while delegating to the real catalog.
    /// </summary>
    sealed class CountingAssetFileCatalog : IEditorAssetFileCatalog {
        public int EnumerationCount { get; private set; }

        public IEnumerable<string> EnumerateFiles(string assetsRootPath) {
            EnumerationCount++;
            return Directory.EnumerateFiles(assetsRootPath, "*", SearchOption.AllDirectories);
        }
    }

    /// <summary>
    /// Fails publication before a destination replacement is allowed.
    /// </summary>
    sealed class ThrowingWriteChangeLog : IEditorProjectWriteChangeLog {
        public int PublishCount { get; private set; }

        public long CurrentGeneration => 0;

        public IReadOnlyList<EditorProjectWriteChange> ReadAfter(long generation) {
            return Array.Empty<EditorProjectWriteChange>();
        }

        public long PublishChange(string relativePath) {
            PublishCount++;
            throw new IOException("Test publication failure.");
        }

        public long BeginRepairBatch(IReadOnlyList<string> relativePaths) {
            PublishCount++;
            throw new IOException("Test publication failure.");
        }

        public void CommitRepairBatch(long batchId) {
            throw new IOException("Test publication failure.");
        }

        public void CancelRepairBatch(long batchId) {
        }
    }

    /// <summary>
    /// Keeps startup replay at a known baseline while publishing through the real locked snapshot.
    /// </summary>
    sealed class FixedBaselineWriteChangeLog : IEditorProjectWriteChangeLog {
        readonly string ProjectRootPath;

        public FixedBaselineWriteChangeLog(string projectRootPath) {
            ProjectRootPath = projectRootPath;
        }

        public long CurrentGeneration => 0;

        public IReadOnlyList<EditorProjectWriteChange> ReadAfter(long generation) {
            return Array.Empty<EditorProjectWriteChange>();
        }

        public long PublishChange(string relativePath) {
            return EditorProjectWriteGeneration.PublishChangeUnderLock(ProjectRootPath, relativePath);
        }

        public long BeginRepairBatch(IReadOnlyList<string> relativePaths) {
            return EditorProjectWriteGeneration.BeginRepairBatchUnderLock(ProjectRootPath, relativePaths);
        }

        public void CommitRepairBatch(long batchId) {
            EditorProjectWriteGeneration.CommitRepairBatchUnderLock(ProjectRootPath, batchId);
        }

        public void CancelRepairBatch(long batchId) {
            EditorProjectWriteGeneration.CancelRepairBatchUnderLock(ProjectRootPath, batchId);
        }
    }
}
