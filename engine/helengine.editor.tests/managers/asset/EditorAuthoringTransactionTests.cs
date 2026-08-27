namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies recoverable, project-scoped multi-file authoring transactions.
/// </summary>
public sealed class EditorAuthoringTransactionTests : IDisposable {
    readonly string ProjectRootPath;

    public EditorAuthoringTransactionTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-authoring-transactions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void WriteAsset_DoesNotTouchAssetsUntilCommit() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();

        EditorAssetWriteResult result = transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));

        Assert.Equal(EditorAssetWriteDisposition.Created, result.Disposition);
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
        transaction.Commit();
        Assert.True(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
        transaction.Commit();
        Assert.False(Directory.Exists(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions", transaction.TransactionId)));
    }

    [Fact]
    public void Commit_PublishesIndexAndReferenceVisibilityOnlyAfterCommit() {
        using EditorProjectAuthoringSession observer = CreateSession(ProjectRootPath);
        using EditorProjectAuthoringSession author = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = author.BeginTransaction();
        EditorAssetWriteResult staged = transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));

        Assert.ThrowsAny<Exception>(() => observer.CreateReference("models/ship.hasset", AssetEntryKind.Model));
        transaction.Commit();

        SceneAssetReference reference = observer.CreateReference("models/ship.hasset", AssetEntryKind.Model);
        Assert.Equal(staged.AssetId, reference.AssetId);
        Assert.True(File.Exists(Path.Combine(ProjectRootPath, "cache", "editor", "asset-identity-index.json")));
    }

    [Fact]
    public void WriteAsset_PreservesExistingNativeIdentityWithoutSidecar() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAssetWriteResult original = session.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        using EditorAuthoringTransaction transaction = session.BeginTransaction();

        EditorAssetWriteResult staged = transaction.WriteAsset("models/ship.hasset", CreateModel("Changed"));
        transaction.Commit();

        Assert.Equal(original.AssetId, staged.AssetId);
        Assert.False(File.Exists(original.FullPath + ".hmeta"));
    }

    [Fact]
    public void BeginTransaction_AllowsOnlyOneActiveTransaction() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();

        Assert.Throws<InvalidOperationException>(() => session.BeginTransaction());
    }

    [Fact]
    public void Commit_PublishesAllStagedAssets() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        transaction.WriteAsset("models/cargo.hasset", CreateModel("Cargo"));

        transaction.Commit();

        Assert.True(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
        Assert.True(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "cargo.hasset")));
    }

    [Fact]
    public void Commit_UnchangedStagedAssetPreservesTimestamp() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAssetWriteResult original = session.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        DateTime timestamp = File.GetLastWriteTimeUtc(original.FullPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        EditorAssetWriteResult staged = transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));

        Assert.Equal(EditorAssetWriteDisposition.Unchanged, staged.Disposition);
        transaction.Commit();

        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(original.FullPath));
    }

    [Fact]
    public void Commit_WhenDestinationChangesAfterStaging_PublishesNothing() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAssetWriteResult original = session.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        byte[] originalBytes = File.ReadAllBytes(original.FullPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        transaction.WriteAsset("models/ship.hasset", CreateModel("Changed"));
        File.WriteAllBytes(original.FullPath, originalBytes.Concat(new byte[] { 0x42 }).ToArray());

        Assert.Throws<IOException>(() => transaction.Commit());
        Assert.Equal(originalBytes.Concat(new byte[] { 0x42 }), File.ReadAllBytes(original.FullPath));
    }

    [Fact]
    public void Commit_WhenSecondReplacementFails_RestoresEarlierDestinations() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAssetWriteResult first = session.WriteAsset("models/first.hasset", CreateModel("First"));
        EditorAssetWriteResult second = session.WriteAsset("models/second.hasset", CreateModel("Second"));
        byte[] firstBytes = File.ReadAllBytes(first.FullPath);
        byte[] secondBytes = File.ReadAllBytes(second.FullPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction(new EditorAuthoringTransactionHooks {
            BeforeReplacement = (index, _) => {
                if (index == 1) {
                    throw new IOException("injected replacement failure");
                }
            }
        });
        transaction.WriteAsset("models/first.hasset", CreateModel("FirstChanged"));
        transaction.WriteAsset("models/second.hasset", CreateModel("SecondChanged"));

        Assert.Throws<IOException>(() => transaction.Commit());
        Assert.Equal(firstBytes, File.ReadAllBytes(first.FullPath));
        Assert.Equal(secondBytes, File.ReadAllBytes(second.FullPath));
    }

    [Fact]
    public void Commit_WhenNewReplacementFails_DeletesNewDestinations() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction(new EditorAuthoringTransactionHooks {
            BeforeReplacement = (index, _) => {
                if (index == 1) {
                    throw new IOException("injected replacement failure");
                }
            }
        });
        transaction.WriteAsset("models/first.hasset", CreateModel("First"));
        transaction.WriteAsset("models/second.hasset", CreateModel("Second"));

        Assert.Throws<IOException>(() => transaction.Commit());
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "first.hasset")));
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "second.hasset")));
    }

    [Fact]
    public void Dispose_UncommittedTransactionRemovesOnlyItsStagingDirectory() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAuthoringTransaction transaction = session.BeginTransaction();
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));

        transaction.Dispose();
        transaction.Dispose();

        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
        string transactionRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions");
        Assert.True(!Directory.Exists(transactionRoot) || !Directory.EnumerateDirectories(transactionRoot).Any());
    }

    [Fact]
    public void StartupRecovery_CommittingJournalRestoresBackedUpDestination() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAssetWriteResult original = session.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        byte[] originalBytes = File.ReadAllBytes(original.FullPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        transaction.WriteAsset("models/ship.hasset", CreateModel("Changed"));
        string transactionDirectory = Path.Combine(
            ProjectRootPath,
            "cache",
            "editor",
            "authoring-transactions",
            transaction.TransactionId);
        string manifestPath = Path.Combine(transactionDirectory, "transaction.json");
        EditorAuthoringTransactionDocument document = System.Text.Json.JsonSerializer.Deserialize<EditorAuthoringTransactionDocument>(
            File.ReadAllText(manifestPath),
            EditorAuthoringTransactionDocument.JsonOptions);
        EditorAuthoringTransactionEntry entry = Assert.Single(document.Entries);
        string backupPath = Path.Combine(transactionDirectory, entry.BackupRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
        File.WriteAllBytes(backupPath, originalBytes);
        entry.PriorContentHash = original.ContentHash;
        entry.PriorSerializedHash = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(originalBytes)).ToLowerInvariant();
        entry.Changed = true;
        File.Copy(Path.Combine(transactionDirectory, entry.StagedRelativePath.Replace('/', Path.DirectorySeparatorChar)), original.FullPath, true);
        document.State = EditorAuthoringTransactionState.Committing;
        entry.State = document.State;
        File.WriteAllText(manifestPath, System.Text.Json.JsonSerializer.Serialize(document, EditorAuthoringTransactionDocument.JsonOptions));

        using EditorProjectAuthoringSession recovered = CreateSession(ProjectRootPath);

        Assert.Equal(originalBytes, File.ReadAllBytes(original.FullPath));
        Assert.False(Directory.Exists(transactionDirectory));
    }

    [Fact]
    public void StartupRecovery_RejectsTraversalJournalBeforeTouchingOutsidePath() {
        string transactionRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions");
        string transactionDirectory = Path.Combine(transactionRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(transactionDirectory, "staged"));
        File.WriteAllBytes(Path.Combine(transactionDirectory, "staged", "payload"), new byte[] { 1 });
        EditorAuthoringTransactionDocument document = new EditorAuthoringTransactionDocument {
            TransactionId = Path.GetFileName(transactionDirectory),
            State = EditorAuthoringTransactionState.Staging,
            Entries = new List<EditorAuthoringTransactionEntry> {
                new EditorAuthoringTransactionEntry {
                    DestinationRelativePath = "../outside.hasset",
                    StagedRelativePath = "staged/payload",
                    State = EditorAuthoringTransactionState.Staging
                }
            }
        };
        File.WriteAllText(
            Path.Combine(transactionDirectory, "transaction.json"),
            System.Text.Json.JsonSerializer.Serialize(document, EditorAuthoringTransactionDocument.JsonOptions));

        Assert.Throws<InvalidDataException>(() => CreateSession(ProjectRootPath));
        Assert.True(Directory.Exists(transactionDirectory));
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "outside.hasset")));
    }

    [Fact]
    public void StartupRecovery_RemovesStagingAndCommittedTransactions() {
        string transactionRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions");
        string stagingDirectory = Path.Combine(transactionRoot, Guid.NewGuid().ToString("N"));
        string committedDirectory = Path.Combine(transactionRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        Directory.CreateDirectory(committedDirectory);
        WriteEmptyDocument(stagingDirectory, EditorAuthoringTransactionState.Staging);
        WriteEmptyDocument(committedDirectory, EditorAuthoringTransactionState.Committed);

        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);

        Assert.False(Directory.Exists(stagingDirectory));
        Assert.False(Directory.Exists(committedDirectory));
    }

    static void WriteEmptyDocument(string transactionDirectory, EditorAuthoringTransactionState state) {
        EditorAuthoringTransactionDocument document = new EditorAuthoringTransactionDocument {
            TransactionId = Path.GetFileName(transactionDirectory),
            State = state,
            Entries = new List<EditorAuthoringTransactionEntry>()
        };
        File.WriteAllText(
            Path.Combine(transactionDirectory, "transaction.json"),
            System.Text.Json.JsonSerializer.Serialize(document, EditorAuthoringTransactionDocument.JsonOptions));
    }

    static EditorProjectAuthoringSession CreateSession(string projectRootPath) {
        return new EditorProjectAuthoringSession(
            projectRootPath,
            Array.Empty<IAssetImporterRegistration>(),
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets"))));
    }

    static ModelAsset CreateModel(string id) {
        return new ModelAsset {
            Id = id,
            Positions = Array.Empty<float3>(),
            Normals = Array.Empty<float3>(),
            TexCoords = Array.Empty<float2>(),
            Indices16 = Array.Empty<ushort>(),
            Indices32 = Array.Empty<uint>(),
            Submeshes = Array.Empty<ModelSubmeshAsset>()
        };
    }
}
