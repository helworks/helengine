namespace helengine.editor.tests.managers.asset;

using helengine.editor.tests.testing;

/// <summary>
/// Verifies recoverable, project-scoped multi-file authoring transactions.
/// </summary>
public sealed class EditorAuthoringTransactionTests : IDisposable {
    readonly string ProjectRootPath;
    readonly List<TestGeneratedAssetGraph> GeneratedGraphs = new List<TestGeneratedAssetGraph>();

    public EditorAuthoringTransactionTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-authoring-transactions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    public void Dispose() {
        for (int index = 0; index < GeneratedGraphs.Count; index++) {
            GeneratedGraphs[index].Dispose();
        }
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
    public void WriteAsset_WhenDestinationIsAnExistingDirectory_RejectsWithoutRemovingTheDirectory() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        string destinationPath = Path.Combine(ProjectRootPath, "assets", "models", "directory-collision.hasset");
        Directory.CreateDirectory(destinationPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();

        transaction.WriteAsset("models/directory-collision.hasset", CreateModel("Collision"));

        Assert.Throws<IOException>(() => transaction.Commit());
        Assert.True(Directory.Exists(destinationPath));
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
        Assert.NotNull(observer.IdentityIndexValue.FindByPath("models/ship.hasset"));
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
    public void WriteMaterial_StagesAllDocumentsAndPreservesExistingIdentity() {
        using EditorProjectAuthoringSession session = CreateSession(
            ProjectRootPath,
            new IAssetImporterRegistration[] {
                new TextureImporterRegistration("repaired", new TestTextureImporter(), new[] { ".png" })
            });
        GeneratedMaterialAssetDefinition firstDefinition = CreateGeneratedMaterial("Materials/TestMaterial");
        using (EditorAuthoringTransaction firstTransaction = session.BeginTransaction()) {
            EditorAssetWriteResult firstResult = firstTransaction.WriteMaterial("materials/TestMaterial.hasset", firstDefinition);

            string materialPath = Path.Combine(ProjectRootPath, "assets", "materials", "TestMaterial.hasset");
            Assert.Equal(EditorAssetWriteDisposition.Created, firstResult.Disposition);
            Assert.False(File.Exists(materialPath));
            firstTransaction.Commit();
            Assert.True(File.Exists(materialPath));
        }

        string committedPath = Path.Combine(ProjectRootPath, "assets", "materials", "TestMaterial.hasset");
        string firstIdentity;
        using (FileStream stream = File.OpenRead(committedPath)) {
            firstIdentity = MaterialAssetCommonSettingsDocumentBinarySerializer.Deserialize(stream).AuthoringAssetId;
        }

        GeneratedMaterialAssetDefinition secondDefinition = CreateGeneratedMaterial("Materials/TestMaterial");
        secondDefinition.MaterialAsset.AuthoringAssetId = "abcdefabcdefabcdefabcdefabcdefab";
        using EditorAuthoringTransaction secondTransaction = session.BeginTransaction();
        EditorAssetWriteResult secondResult = secondTransaction.WriteMaterial("materials/TestMaterial.hasset", secondDefinition);

        Assert.Equal(firstIdentity, secondResult.AssetId);
        secondTransaction.Commit();
        using FileStream updatedStream = File.OpenRead(committedPath);
        Assert.Equal(firstIdentity, MaterialAssetCommonSettingsDocumentBinarySerializer.Deserialize(updatedStream).AuthoringAssetId);
    }

    [Fact]
    public void WriteMaterial_UsesCanonicalCommonHashAndIdentityFreePlatformOverrides() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        transaction.WriteMaterial("materials/CanonicalMaterial.hasset", CreateGeneratedMaterial("Materials/CanonicalMaterial"));

        string manifestPath = Path.Combine(
            ProjectRootPath,
            "cache",
            "editor",
            "authoring-transactions",
            transaction.TransactionId,
            "transaction.json");
        EditorAuthoringTransactionDocument document = System.Text.Json.JsonSerializer.Deserialize<EditorAuthoringTransactionDocument>(
            File.ReadAllText(manifestPath),
            EditorAuthoringTransactionDocument.JsonOptions);
        EditorAuthoringTransactionEntry commonEntry = Assert.Single(document.Entries.Where(entry =>
            entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialCommonSettings));
        EditorAuthoringTransactionEntry[] overrideEntries = document.Entries.Where(entry =>
            entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialPlatformOverride).ToArray();
        Assert.Equal(2, overrideEntries.Length);
        Assert.All(overrideEntries, entry => Assert.Equal(string.Empty, entry.ExpectedAssetId));
        Assert.NotEqual(commonEntry.StagedSerializedHash, commonEntry.StagedContentHash);
        string commonStagedPath = Path.Combine(
            Path.GetDirectoryName(manifestPath),
            commonEntry.StagedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal(
            commonEntry.StagedContentHash,
            EditorNativeAssetWriteService.ComputeCanonicalMaterialSettingsHash(File.ReadAllBytes(commonStagedPath)));
        transaction.Commit();
    }

    [Fact]
    public void WriteGeneratedCacheAsset_IsStagedAndPublishedThroughTheSameTransaction() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        session.WriteGeneratedCacheAsset("editor/models/cache.hasset", CreateModel("Cache"), transaction);

        string cachePath = Path.Combine(ProjectRootPath, "cache", "editor", "models", "cache.hasset");
        Assert.False(File.Exists(cachePath));
        transaction.Commit();
        Assert.True(File.Exists(cachePath));
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-write.generation")));
    }

    [Fact]
    public void WriteGeneratedFile_PublishesOnlyOnCommitAndSupportsIdempotentPriorHash() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        byte[] sourceBytes = new byte[] { 0x10, 0x20, 0x30 };
        string relativePath = "assets/generated/source.bin";
        using (EditorAuthoringTransaction transaction = session.BeginTransaction()) {
            EditorAssetWriteResult result = transaction.WriteGeneratedFile(
                relativePath,
                sourceBytes,
                null,
                EditorGeneratedFileKind.Source);

            Assert.Equal(EditorAssetWriteDisposition.Created, result.Disposition);
            Assert.Equal(sourceBytes, transaction.ReadStagedFile(relativePath));
            Assert.False(File.Exists(Path.Combine(ProjectRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar))));
            transaction.Commit();
        }

        string publishedPath = Path.Combine(ProjectRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal(sourceBytes, File.ReadAllBytes(publishedPath));
        string priorHash;
        using (EditorAuthoringTransaction hashTransaction = session.BeginTransaction()) {
            priorHash = hashTransaction.GetCurrentFileHash(relativePath);
        }
        using EditorAuthoringTransaction second = session.BeginTransaction();
        EditorAssetWriteResult unchanged = second.WriteGeneratedFile(
            relativePath,
            sourceBytes,
            priorHash,
            EditorGeneratedFileKind.Source);
        Assert.Equal(EditorAssetWriteDisposition.Unchanged, unchanged.Disposition);
        second.Commit();
    }

    [Fact]
    public void StartupRecovery_CommittingGeneratedFileRestoresProjectRootDestinationWithoutAssetGeneration() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        string relativePath = "cache/editor/recovery.bin";
        string destinationPath = Path.Combine(ProjectRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
        byte[] originalBytes = new byte[] { 0x01, 0x02 };
        File.WriteAllBytes(destinationPath, originalBytes);

        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        byte[] replacementBytes = new byte[] { 0x03, 0x04, 0x05 };
        transaction.WriteGeneratedFile(
            relativePath,
            replacementBytes,
            EditorNativeAssetWriteService.ComputeRawBytesHash(originalBytes),
            EditorGeneratedFileKind.Cache);

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
        string stagedPath = Path.Combine(transactionDirectory, entry.StagedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string backupPath = Path.Combine(transactionDirectory, entry.BackupRelativePath.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllBytes(backupPath, originalBytes);
        entry.BackupContentHash = EditorNativeAssetWriteService.ComputeRawBytesHash(originalBytes);
        entry.BackupSerializedHash = entry.BackupContentHash;
        entry.Progress = EditorAuthoringTransactionEntryProgress.Applying;
        entry.State = EditorAuthoringTransactionState.Committing;
        document.State = EditorAuthoringTransactionState.Committing;
        File.Copy(stagedPath, destinationPath, true);
        File.WriteAllText(manifestPath, System.Text.Json.JsonSerializer.Serialize(document, EditorAuthoringTransactionDocument.JsonOptions));
        using (EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
            EditorAuthoringTransactionPendingMarker.PublishUnderLock(
                ProjectRootPath,
                transaction.TransactionId,
                new[] { entry.DestinationRelativePath });
        }
        transaction.ReleaseLeaseForTesting();

        using EditorProjectAuthoringSession recovered = CreateSession(ProjectRootPath);

        Assert.Equal(originalBytes, File.ReadAllBytes(destinationPath));
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(Directory.Exists(transactionDirectory));
    }

    [Fact]
    public void LoadMaterialAsset_ReadsTheStagedCommonAndPlatformDocumentsBeforePublication() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        transaction.WriteMaterial("materials/TestMaterial.hasset", CreateGeneratedMaterial("Materials/TestMaterial"));

        ShaderMaterialAsset material = session.LoadMaterialAsset(
            "materials/TestMaterial.hasset",
            "windows",
            transaction);

        Assert.Equal("ForwardStandardShader", material.ShaderAssetId);
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "materials", "TestMaterial.hasset")));
    }

    [Fact]
    public void WriteGeneratedTexture_StagesSourceAndSettingsFromTheSameBytes() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        byte[] sourceBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        TextureAssetImportSettings intent = new TextureAssetImportSettings();
        intent.Importer.ImporterId = "generated";

        TextureAssetImportSettings prepared = session.WriteGeneratedTexture(
            "textures/generated.png",
            sourceBytes,
            intent,
            transaction);

        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceBytes)).ToLowerInvariant(),
            prepared.Importer.SourceChecksum);
        Assert.NotNull(prepared.Importer.AssetId);
        Assert.Equal(sourceBytes, transaction.ReadStagedFile("assets/textures/generated.png"));
        Assert.NotEmpty(transaction.ReadStagedFile("assets/textures/generated.png.hasset"));
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "textures", "generated.png")));
        transaction.Commit();
        Assert.Equal(sourceBytes, File.ReadAllBytes(Path.Combine(ProjectRootPath, "assets", "textures", "generated.png")));
        Assert.NotNull(session.IdentityIndexValue.FindByPath("textures/generated.png"));
        Assert.True(File.Exists(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-write.generation")));
    }

    [Fact]
    public void WriteGeneratedTexture_WhenPublicationFails_RollsBackSourceMetadataAndIndex() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAuthoringTransactionHooks hooks = new EditorAuthoringTransactionHooks {
            AfterReplacement = (_, _) => throw new IOException("injected generated texture publication failure")
        };
        using EditorAuthoringTransaction transaction = session.BeginTransaction(hooks);
        TextureAssetImportSettings intent = new TextureAssetImportSettings();
        intent.Importer.ImporterId = "generated";
        session.WriteGeneratedTexture("textures/rollback.png", new byte[] { 0x09, 0x08 }, intent, transaction);

        Assert.Throws<IOException>(() => transaction.Commit());

        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "textures", "rollback.png")));
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "textures", "rollback.png.hmeta")));
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "textures", "rollback.png.hasset")));
        Assert.Null(session.IdentityIndexValue.FindByPath("textures/rollback.png"));
    }

    [Fact]
    public void WriteGeneratedTexture_WhenSourceChanges_RecomputesStaleIntentIdentityBeforeCommit() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        TextureAssetImportSettings firstIntent = new TextureAssetImportSettings();
        firstIntent.Importer.ImporterId = "generated";
        using (EditorAuthoringTransaction first = session.BeginTransaction()) {
            TextureAssetImportSettings firstSettings = session.WriteGeneratedTexture(
                "textures/changed.png",
                new byte[] { 0x01 },
                firstIntent,
                first);
            first.Commit();
            firstIntent = firstSettings;
        }

        byte[] changedBytes = new byte[] { 0x02, 0x03 };
        using EditorAuthoringTransaction second = session.BeginTransaction();
        TextureAssetImportSettings changedSettings = session.WriteGeneratedTexture(
            "textures/changed.png",
            changedBytes,
            firstIntent,
            second);

        Assert.NotEqual(firstIntent.Importer.AssetId, changedSettings.Importer.AssetId);
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(changedBytes)).ToLowerInvariant(),
            changedSettings.Importer.SourceChecksum);
        Assert.Equal(new byte[] { 0x01 }, File.ReadAllBytes(Path.Combine(ProjectRootPath, "assets", "textures", "changed.png")));
        second.Commit();
        Assert.Equal(changedBytes, File.ReadAllBytes(Path.Combine(ProjectRootPath, "assets", "textures", "changed.png")));
    }

    [Fact]
    public void WriteGeneratedTexture_PreservesExistingSemanticIdentityOverAProposedCatalogId() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        TextureAssetImportSettings firstIntent = new TextureAssetImportSettings();
        firstIntent.Importer.ImporterId = "generated";
        TextureAssetImportSettings firstSettings;
        using (EditorAuthoringTransaction first = session.BeginTransaction()) {
            firstSettings = session.WriteGeneratedTexture(
                "textures/identity.png",
                new byte[] { 0x71, 0x72 },
                firstIntent,
                first);
            first.Commit();
        }

        TextureAssetImportSettings proposed = new TextureAssetImportSettings();
        proposed.Importer.ImporterId = firstSettings.Importer.ImporterId;
        proposed.Importer.AssetId = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        proposed.Processor = firstSettings.Processor;
        using EditorAuthoringTransaction repeat = session.BeginTransaction();
        TextureAssetImportSettings repeated = session.WriteGeneratedTexture(
            "textures/identity.png",
            new byte[] { 0x71, 0x72 },
            proposed,
            repeat);

        Assert.Equal(firstSettings.Importer.AssetId, repeated.Importer.AssetId);
        repeat.Commit();
    }

    [Fact]
    public void WriteGeneratedTexture_WhenProcessingIntentChanges_DoesNotReuseExistingIdentity() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        TextureAssetImportSettings initialIntent = new TextureAssetImportSettings();
        initialIntent.Importer.ImporterId = "generated";
        TextureAssetImportSettings initialSettings;
        using (EditorAuthoringTransaction first = session.BeginTransaction()) {
            initialSettings = session.WriteGeneratedTexture(
                "textures/semantic.png",
                new byte[] { 0x21, 0x22 },
                initialIntent,
                first);
            first.Commit();
        }

        TextureAssetImportSettings changedIntent = new TextureAssetImportSettings {
            Importer = new AssetImporterSettings {
                ImporterId = initialSettings.Importer.ImporterId
            }
        };
        changedIntent.Processor.Platforms["windows"] = new TextureAssetProcessorSettings {
            MaxResolution = 64,
            ColorFormatId = TextureAssetColorFormat.Rgba4444.ToString(),
            AlphaPrecision = TextureAssetAlphaPrecision.A4
        };
        using EditorAuthoringTransaction second = session.BeginTransaction();
        TextureAssetImportSettings changedSettings = session.WriteGeneratedTexture(
            "textures/semantic.png",
            new byte[] { 0x21, 0x22 },
            changedIntent,
            second);

        Assert.NotEqual(initialSettings.Importer.AssetId, changedSettings.Importer.AssetId);
        second.Commit();
    }

    [Fact]
    public void WriteGeneratedTexture_RefreshesExternalSourceGraphAndLeavesIdenticalRunsUnchanged() {
        using EditorProjectAuthoringSession author = CreateSession(ProjectRootPath);
        using EditorProjectAuthoringSession observer = CreateSession(ProjectRootPath);
        TextureAssetImportSettings intent = new TextureAssetImportSettings();
        intent.Importer.ImporterId = "generated";
        byte[] sourceBytes = new byte[] { 0x42, 0x43, 0x44 };
        using (EditorAuthoringTransaction transaction = author.BeginTransaction()) {
            author.WriteGeneratedTexture("textures/graph.png", sourceBytes, intent, transaction);
            Assert.ThrowsAny<Exception>(() => observer.CreateReference("textures/graph.png", AssetEntryKind.Image));
            transaction.Commit();
        }

        SceneAssetReference publishedReference = observer.CreateReference("textures/graph.png", AssetEntryKind.Image);
        Assert.False(string.IsNullOrWhiteSpace(publishedReference.AssetId));
        Assert.NotNull(observer.IdentityIndexValue.FindByPath("textures/graph.png"));
        string sourcePath = Path.Combine(ProjectRootPath, "assets", "textures", "graph.png");
        DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourcePath);
        byte[] publishedBytes = File.ReadAllBytes(sourcePath);

        using (EditorAuthoringTransaction repeat = author.BeginTransaction()) {
            author.WriteGeneratedTexture("textures/graph.png", sourceBytes, intent, repeat);
            repeat.Commit();
        }

        Assert.Equal(publishedBytes, File.ReadAllBytes(sourcePath));
        Assert.Equal(sourceWriteTime, File.GetLastWriteTimeUtc(sourcePath));
        Assert.NotNull(observer.IdentityIndexValue.FindByPath("textures/graph.png"));
    }

    [Fact]
    public void WriteGeneratedTexture_WhenExistingSourceLacksMetadata_PublishesTheNewIdentityPair() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        string sourcePath = Path.Combine(ProjectRootPath, "assets", "textures", "metadata.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        byte[] sourceBytes = new byte[] { 0x51, 0x52 };
        File.WriteAllBytes(sourcePath, sourceBytes);
        TextureAssetImportSettings intent = new TextureAssetImportSettings();
        intent.Importer.ImporterId = "generated";
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        session.WriteGeneratedTexture("textures/metadata.png", sourceBytes, intent, transaction);
        transaction.Commit();

        Assert.True(File.Exists(sourcePath + ".hmeta"));
        Assert.NotNull(session.IdentityIndexValue.FindByPath("textures/metadata.png"));
        Assert.Equal(
            "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceBytes)).ToLowerInvariant(),
            session.HashCacheValue.GetContentHash(sourcePath));
    }

    [Fact]
    public void WriteGeneratedTexture_WhenImporterIdNeedsRepair_StagesRepairAndRollsItBackWithPublication() {
        string sourcePath = Path.Combine(ProjectRootPath, "assets", "textures", "repair.png");
        string settingsPath = sourcePath + ".hasset";
        using EditorProjectAuthoringSession session = CreateSession(
            ProjectRootPath,
            new IAssetImporterRegistration[] {
                new TextureImporterRegistration("repaired", new TestTextureImporter(), new[] { ".png" })
            });
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        byte[] sourceBytes = new byte[] { 0x11, 0x22, 0x33 };
        File.WriteAllBytes(sourcePath, sourceBytes);
        TextureAssetImportSettings invalidSettings = new TextureAssetImportSettings();
        invalidSettings.Importer.ImporterId = "missing-importer";
        using (FileStream stream = File.Create(settingsPath)) {
            TextureAssetImportSettingsBinarySerializer.Serialize(stream, invalidSettings);
        }
        byte[] priorSettingsBytes = File.ReadAllBytes(settingsPath);

        EditorAuthoringTransactionHooks hooks = new EditorAuthoringTransactionHooks {
            AfterReplacement = (index, _) => {
                if (index == 1) {
                    throw new IOException("injected repaired texture publication failure");
                }
            }
        };
        using EditorAuthoringTransaction transaction = session.BeginTransaction(hooks);
        TextureAssetImportSettings intent = new TextureAssetImportSettings();
        intent.Importer.ImporterId = "missing-importer";
        TextureAssetImportSettings prepared = session.WriteGeneratedTexture(
            "textures/repair.png",
            sourceBytes,
            intent,
            transaction);

        Assert.NotEqual("missing-importer", prepared.Importer.ImporterId);
        using (MemoryStream stagedStream = new MemoryStream(transaction.ReadStagedFile("assets/textures/repair.png.hasset"))) {
            TextureAssetImportSettings staged = TextureAssetImportSettingsBinarySerializer.Deserialize(stagedStream);
            Assert.Equal(prepared.Importer.ImporterId, staged.Importer.ImporterId);
        }
        Assert.Throws<IOException>(() => transaction.Commit());

        Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
        Assert.Equal(priorSettingsBytes, File.ReadAllBytes(settingsPath));
        Assert.False(File.Exists(sourcePath + ".hmeta"));
        Assert.Null(session.IdentityIndexValue.FindByPath("textures/repair.png"));
    }

    [Fact]
    public void LoadImportedRuntimeModel_ReadsTheNativeModelStagedByTheSameTransaction() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        transaction.WriteAsset("models/staged.hasset", CreateModel("Staged"));

        RuntimeModel runtimeModel = session.LoadImportedRuntimeModel("models/staged.hasset", transaction);

        Assert.NotNull(runtimeModel);
        transaction.Commit();
    }

    [Fact]
    public void LoadImportedRuntimeModel_PrefersChangedStagedNativeModelOverPublishedDestination() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using (EditorAuthoringTransaction initial = session.BeginTransaction()) {
            initial.WriteAsset("models/staged-change.hasset", CreateModel("Published"));
            initial.Commit();
        }

        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        transaction.WriteAsset("models/staged-change.hasset", CreateModel("Changed"));
        RuntimeModel runtimeModel = session.LoadImportedRuntimeModel("models/staged-change.hasset", transaction);

        Assert.Equal("Changed", runtimeModel.Id);
        Assert.Equal("Published", session.LoadImportedRuntimeModel("models/staged-change.hasset").Id);
        transaction.Commit();
    }

    [Fact]
    public void Commit_DefersIdentityRegistrationUntilAfterDestinationPublication() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        bool destinationVisibleAtGraphUpdate = false;
        using EditorAuthoringTransaction transaction = session.BeginTransaction(new EditorAuthoringTransactionHooks {
            BeforePublication = (_, path) => {
                Assert.True(File.Exists(Path.Combine(ProjectRootPath, "assets", path.Replace('/', Path.DirectorySeparatorChar))));
            },
            BeforeGraphUpdate = (_, path) => {
                destinationVisibleAtGraphUpdate = File.Exists(Path.Combine(ProjectRootPath, "assets", path.Replace('/', Path.DirectorySeparatorChar)));
            }
        });
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));

        transaction.Commit();

        Assert.True(destinationVisibleAtGraphUpdate);
        Assert.NotNull(session.CreateReference("models/ship.hasset", AssetEntryKind.Model));
    }

    [Fact]
    public void WriteMaterial_WhenPublicationFails_RollsBackCommonAndOverridesTogether() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAuthoringTransactionHooks hooks = new EditorAuthoringTransactionHooks {
            AfterReplacement = (_, _) => throw new IOException("injected material publication failure")
        };
        using EditorAuthoringTransaction transaction = session.BeginTransaction(hooks);
        transaction.WriteMaterial("materials/TestMaterial.hasset", CreateGeneratedMaterial("Materials/TestMaterial"));

        Assert.Throws<IOException>(() => transaction.Commit());
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "materials", "TestMaterial.hasset")));
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "materials", "TestMaterial.hasset.windows.hasset")));
    }

    [Fact]
    public void SessionRejectsSameRootTransactionBorrowedFromAnotherSession() {
        using EditorProjectAuthoringSession firstSession = CreateSession(ProjectRootPath);
        using EditorProjectAuthoringSession secondSession = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction secondTransaction = secondSession.BeginTransaction();

        Assert.False(firstSession.OwnsTransaction(secondTransaction));
        Assert.Throws<InvalidOperationException>(() => firstSession.WriteGeneratedMaterial(
            "materials/TestMaterial.hasset",
            CreateGeneratedMaterial("Materials/TestMaterial"),
            secondTransaction));
    }

    [Fact]
    public void SessionReference_UsesTheExactStagedIdentityBeforeCommit() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        EditorAssetWriteResult staged = transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));

        SceneAssetReference reference = session.CreateReference("models/ship.hasset", AssetEntryKind.Model);

        Assert.Equal(staged.AssetId, reference.AssetId);
        Assert.Equal(staged.ContentHash, reference.ContentHash);
        Assert.False(File.Exists(staged.FullPath));
    }

    [Fact]
    public void Commit_WhenStagedIdentityBytesAreCorrupted_FailsClosedWithoutPublishing() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        string stagedPath = Path.Combine(
            ProjectRootPath,
            "cache",
            "editor",
            "authoring-transactions",
            transaction.TransactionId,
            "staged",
            "00000000.payload");
        Asset stagedAsset;
        using (FileStream stream = new FileStream(stagedPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            stagedAsset = AssetSerializer.Deserialize(stream);
        }
        stagedAsset.AuthoringAssetId = "abcdefabcdefabcdefabcdefabcdefab";
        File.WriteAllBytes(stagedPath, AssetSerializer.SerializeToBytes(stagedAsset));

        Assert.Throws<InvalidDataException>(() => transaction.Commit());
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
    }

    [Fact]
    public void SecondSession_DoesNotDeleteLiveStagingTransaction() {
        using EditorProjectAuthoringSession author = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = author.BeginTransaction();
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        string transactionDirectory = Path.Combine(
            ProjectRootPath,
            "cache",
            "editor",
            "authoring-transactions",
            transaction.TransactionId);

        using EditorProjectAuthoringSession observer = CreateSession(ProjectRootPath);

        Assert.True(Directory.Exists(transactionDirectory));
        transaction.Commit();
        Assert.True(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
    }

    [Fact]
    public void PendingTransactionMarker_BlocksAlreadyOpenSessionReads() {
        using EditorProjectAuthoringSession observer = CreateSession(ProjectRootPath);
        string markerPath = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions.pending");
        Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
        File.WriteAllText(markerPath, "{\"version\":1,\"transactionId\":\"00112233445566778899aabbccddeeff\",\"relativePaths\":[\"models/ship.hasset\"]}");

        Assert.Throws<InvalidOperationException>(() => observer.RefreshExternalChanges());
    }

    [Fact]
    public void Commit_ReplaysCommittedGenerationBeforeValidatingPreparedIdentityClaims() {
        using EditorProjectAuthoringSession first = CreateSession(ProjectRootPath);
        using EditorProjectAuthoringSession second = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction firstTransaction = first.BeginTransaction();
        using EditorAuthoringTransaction secondTransaction = second.BeginTransaction();
        ModelAsset firstAsset = CreateModel("First");
        ModelAsset secondAsset = CreateModel("Second");
        firstAsset.AuthoringAssetId = "00112233445566778899aabbccddeeff";
        secondAsset.AuthoringAssetId = firstAsset.AuthoringAssetId;
        firstTransaction.WriteAsset("models/first.hasset", firstAsset);
        secondTransaction.WriteAsset("models/second.hasset", secondAsset);

        firstTransaction.Commit();

        Assert.Throws<InvalidOperationException>(() => secondTransaction.Commit());
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "second.hasset")));
    }

    [Fact]
    public void Commit_WhenFailureFollowsGenerationPublication_ReplaysRestoredGeneration() {
        using EditorProjectAuthoringSession observer = CreateSession(ProjectRootPath);
        EditorAuthoringTransactionHooks hooks = new EditorAuthoringTransactionHooks {
            AfterPublication = () => throw new IOException("injected post-generation failure")
        };
        using EditorProjectAuthoringSession author = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = author.BeginTransaction(hooks);
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));

        Assert.Throws<IOException>(() => transaction.Commit());
        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
        Assert.ThrowsAny<Exception>(() => observer.CreateReference("models/ship.hasset", AssetEntryKind.Model));
    }

    [Fact]
    public void Commit_WhenRollbackMarkerClearFails_RetainsAbortingJournalForRecovery() {
        using EditorProjectAuthoringSession author = CreateSession(ProjectRootPath);
        EditorAuthoringTransactionHooks hooks = new EditorAuthoringTransactionHooks {
            AfterPublication = () => throw new IOException("injected post-generation failure"),
            BeforePendingMarkerClear = () => throw new IOException("injected rollback marker clear failure")
        };
        EditorAuthoringTransaction transaction = author.BeginTransaction(hooks);
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        string transactionDirectory = Path.Combine(
            ProjectRootPath,
            "cache",
            "editor",
            "authoring-transactions",
            transaction.TransactionId);

        Assert.Throws<AggregateException>(() => transaction.Commit());
        Assert.True(Directory.Exists(transactionDirectory));
        transaction.Dispose();

        using EditorProjectAuthoringSession recovered = CreateSession(ProjectRootPath);

        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
        Assert.False(Directory.Exists(transactionDirectory));

        long recoveredGeneration = EditorProjectWriteGeneration.Read(ProjectRootPath);
        using EditorProjectAuthoringSession recoveredAgain = CreateSession(ProjectRootPath);
        Assert.Equal(recoveredGeneration, EditorProjectWriteGeneration.Read(ProjectRootPath));
    }

    [Fact]
    public void Commit_WhenRollbackOperationFails_RetainsJournalAndRecoveryBlocker() {
        using EditorProjectAuthoringSession author = CreateSession(ProjectRootPath);
        EditorAuthoringTransactionHooks hooks = new EditorAuthoringTransactionHooks {
            AfterPublication = () => throw new IOException("injected post-generation failure"),
            BeforeRollback = (_, _) => throw new IOException("injected rollback replacement failure")
        };
        EditorAuthoringTransaction transaction = author.BeginTransaction(hooks);
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        string transactionDirectory = Path.Combine(
            ProjectRootPath,
            "cache",
            "editor",
            "authoring-transactions",
            transaction.TransactionId);
        string markerPath = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions.pending");

        Assert.Throws<AggregateException>(() => transaction.Commit());
        Assert.True(Directory.Exists(transactionDirectory));
        Assert.True(File.Exists(markerPath));
        Assert.Equal(EditorAuthoringTransactionOutcome.Failed, transaction.Outcome);

        transaction.Dispose();
        using EditorProjectAuthoringSession recovered = CreateSession(ProjectRootPath);

        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
        Assert.False(File.Exists(markerPath));
        Assert.False(Directory.Exists(transactionDirectory));
    }

    [Fact]
    public void BeginTransaction_HoldsProjectLockUntilManifestConstructionCompletes() {
        using EditorProjectAuthoringSession author = CreateSession(ProjectRootPath);
        using ManualResetEventSlim constructionEntered = new ManualResetEventSlim(false);
        using ManualResetEventSlim releaseConstruction = new ManualResetEventSlim(false);
        EditorAuthoringTransactionHooks hooks = new EditorAuthoringTransactionHooks {
            BeforeManifestWrite = () => {
                constructionEntered.Set();
                releaseConstruction.Wait(TimeSpan.FromSeconds(5));
            }
        };

        Task<EditorAuthoringTransaction> beginTask = Task.Run(() => author.BeginTransaction(hooks));
        Assert.True(constructionEntered.Wait(TimeSpan.FromSeconds(5)));
        Task<EditorProjectAuthoringSession> observerTask = Task.Run(() => CreateSession(ProjectRootPath));
        Assert.False(observerTask.Wait(TimeSpan.FromMilliseconds(100)));
        releaseConstruction.Set();

        using EditorAuthoringTransaction transaction = beginTask.GetAwaiter().GetResult();
        using EditorProjectAuthoringSession observer = observerTask.GetAwaiter().GetResult();
        transaction.Dispose();
    }

    [Fact]
    public void BeginTransaction_WhenManifestConstructionFails_CleansItsOwnDirectory() {
        using EditorProjectAuthoringSession author = CreateSession(ProjectRootPath);

        Assert.Throws<IOException>(() => author.BeginTransaction(new EditorAuthoringTransactionHooks {
            BeforeManifestWrite = () => throw new IOException("injected manifest failure")
        }));

        string transactionRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions");
        Assert.True(!Directory.Exists(transactionRoot) || !Directory.EnumerateDirectories(transactionRoot).Any());
    }

    [Fact]
    public void BeginTransaction_AllowsOnlyOneActiveTransaction() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();

        Assert.Throws<InvalidOperationException>(() => session.BeginTransaction());
    }

    [Fact]
    public void DisposeThenCommit_ThrowsAndDoesNotReportSuccess() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAuthoringTransaction transaction = session.BeginTransaction();

        transaction.Dispose();

        Assert.Equal(EditorAuthoringTransactionOutcome.Disposed, transaction.Outcome);
        Assert.Throws<ObjectDisposedException>(() => transaction.Commit());
    }

    [Fact]
    public void RolledBackTransaction_CannotBeCommittedAgain() {
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
        Assert.Equal(EditorAuthoringTransactionOutcome.RolledBack, transaction.Outcome);
        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
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
    public void Commit_WhenCleanupFailsLeavesCommittedJournalForStartupCleanup() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAuthoringTransactionHooks hooks = new EditorAuthoringTransactionHooks {
            BeforeCleanup = () => throw new IOException("injected cleanup failure")
        };
        using EditorAuthoringTransaction transaction = session.BeginTransaction(hooks);
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        string transactionDirectory = Path.Combine(
            ProjectRootPath,
            "cache",
            "editor",
            "authoring-transactions",
            transaction.TransactionId);

        transaction.Commit();

        Assert.True(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
        Assert.True(Directory.Exists(transactionDirectory));
        Assert.Equal(EditorAuthoringTransactionState.Committed, transaction.State);
        using EditorProjectAuthoringSession recovered = CreateSession(ProjectRootPath);
        Assert.False(Directory.Exists(transactionDirectory));
    }

    [Fact]
    public void Commit_WhenPendingMarkerClearFailsLeavesCommittedStateForRecovery() {
        using EditorProjectAuthoringSession author = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = author.BeginTransaction(new EditorAuthoringTransactionHooks {
            BeforePendingMarkerClear = () => throw new IOException("injected marker clear failure")
        });
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));
        string markerPath = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions.pending");

        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
        Assert.True(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
        Assert.True(File.Exists(markerPath));
        Assert.Equal(EditorAuthoringTransactionState.Committed, transaction.State);

        using EditorProjectAuthoringSession recovered = CreateSession(ProjectRootPath);

        Assert.False(File.Exists(markerPath));
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
    public void Dispose_WhenRetireRenameFails_RetainsPublishedDirectoryForRetry() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        int retireAttempts = 0;
        using EditorAuthoringTransaction transaction = session.BeginTransaction(new EditorAuthoringTransactionHooks {
            BeforeRetireRename = () => {
                if (retireAttempts++ == 0) {
                    throw new IOException("injected retire rename failure");
                }
            }
        });
        string transactionDirectory = Path.Combine(
            ProjectRootPath,
            "cache",
            "editor",
            "authoring-transactions",
            transaction.TransactionId);

        Assert.Throws<IOException>(() => transaction.Dispose());
        Assert.True(Directory.Exists(transactionDirectory));
        Assert.Equal(EditorAuthoringTransactionOutcome.Active, transaction.Outcome);

        transaction.Dispose();
        Assert.False(Directory.Exists(transactionDirectory));
    }

    [Fact]
    public void Dispose_WhenRetireDeletionFails_LeavesDeletingDirectoryForStartupRecovery() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        EditorAuthoringTransaction transaction = session.BeginTransaction(new EditorAuthoringTransactionHooks {
            AfterRetireRename = () => throw new IOException("injected retire deletion failure")
        });
        string transactionDirectory = Path.Combine(
            ProjectRootPath,
            "cache",
            "editor",
            "authoring-transactions",
            transaction.TransactionId);
        string deletingDirectory = Path.Combine(
            ProjectRootPath,
            "cache",
            "editor",
            "authoring-transactions",
            ".deleting-" + transaction.TransactionId);

        Assert.Throws<IOException>(() => transaction.Dispose());
        Assert.False(Directory.Exists(transactionDirectory));
        Assert.True(Directory.Exists(deletingDirectory));
        transaction.Dispose();

        using EditorProjectAuthoringSession recovered = CreateSession(ProjectRootPath);
        Assert.False(Directory.Exists(deletingDirectory));
    }

    [Fact]
    public void BeginTransaction_AndSessionDispose_AreSerializedByTheTransactionGate() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using ManualResetEventSlim constructionEntered = new ManualResetEventSlim(false);
        using ManualResetEventSlim releaseConstruction = new ManualResetEventSlim(false);
        EditorAuthoringTransactionHooks hooks = new EditorAuthoringTransactionHooks {
            BeforeManifestWrite = () => {
                constructionEntered.Set();
                releaseConstruction.Wait(TimeSpan.FromSeconds(5));
            }
        };

        Task<EditorAuthoringTransaction> beginTask = Task.Run(() => session.BeginTransaction(hooks));
        Assert.True(constructionEntered.Wait(TimeSpan.FromSeconds(5)));
        Task disposeTask = Task.Run(session.Dispose);
        Assert.False(disposeTask.Wait(TimeSpan.FromMilliseconds(100)));
        releaseConstruction.Set();

        using EditorAuthoringTransaction transaction = beginTask.GetAwaiter().GetResult();
        disposeTask.GetAwaiter().GetResult();
        Assert.Equal(EditorAuthoringTransactionOutcome.Disposed, transaction.Outcome);
        Assert.Throws<ObjectDisposedException>(() => session.BeginTransaction());
    }

    [Fact]
    public void StartupRecovery_ApplyingAfterReplacementRestoresBackedUpDestination() {
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
        entry.BackupContentHash = original.ContentHash;
        entry.BackupSerializedHash = entry.PriorSerializedHash;
        entry.ExpectedAssetId = original.AssetId;
        entry.ExpectedAssetKind = "ModelAsset";
        entry.StagedSerializedHash = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(transactionDirectory, entry.StagedRelativePath.Replace('/', Path.DirectorySeparatorChar))))).ToLowerInvariant();
        entry.Changed = true;
        // Keep the crash cut in Applying while the staged bytes are already
        // visible at the destination. Recovery must prove the replacement
        // from bytes, not infer it from a later journal state.
        entry.Progress = EditorAuthoringTransactionEntryProgress.Applying;
        File.Copy(Path.Combine(transactionDirectory, entry.StagedRelativePath.Replace('/', Path.DirectorySeparatorChar)), original.FullPath, true);
        document.State = EditorAuthoringTransactionState.Committing;
        entry.State = document.State;
        File.WriteAllText(manifestPath, System.Text.Json.JsonSerializer.Serialize(document, EditorAuthoringTransactionDocument.JsonOptions));
        using (EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
            EditorAuthoringTransactionPendingMarker.PublishUnderLock(
                ProjectRootPath,
                transaction.TransactionId,
                new[] { entry.DestinationRelativePath });
        }
        transaction.ReleaseLeaseForTesting();

        using EditorProjectAuthoringSession recovered = CreateSession(ProjectRootPath);

        Assert.Equal(originalBytes, File.ReadAllBytes(original.FullPath));
        Assert.False(Directory.Exists(transactionDirectory));
    }

    [Fact]
    public void StartupRecovery_ApplyingBeforeReplacementRestoresNothingAndPublishesRollbackGeneration() {
        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);
        using EditorAuthoringTransaction transaction = session.BeginTransaction();
        transaction.WriteAsset("models/ship.hasset", CreateModel("Ship"));
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
        entry.Progress = EditorAuthoringTransactionEntryProgress.Applying;
        document.State = EditorAuthoringTransactionState.Committing;
        entry.State = document.State;
        File.WriteAllText(manifestPath, System.Text.Json.JsonSerializer.Serialize(document, EditorAuthoringTransactionDocument.JsonOptions));
        using (EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
            EditorAuthoringTransactionPendingMarker.PublishUnderLock(
                ProjectRootPath,
                transaction.TransactionId,
                new[] { entry.DestinationRelativePath });
        }
        transaction.ReleaseLeaseForTesting();

        using EditorProjectAuthoringSession recovered = CreateSession(ProjectRootPath);

        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "assets", "models", "ship.hasset")));
        Assert.False(Directory.Exists(transactionDirectory));
    }

    [Fact]
    public void StartupRecovery_RejectsTraversalJournalBeforeTouchingOutsidePath() {
        string transactionRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions");
        string transactionDirectory = Path.Combine(transactionRoot, Guid.NewGuid().ToString("N"));
        string stagedPath = Path.Combine(transactionDirectory, "staged", "payload");
        Directory.CreateDirectory(Path.GetDirectoryName(stagedPath));
        Directory.CreateDirectory(Path.Combine(transactionDirectory, "backups"));
        File.WriteAllBytes(Path.Combine(transactionDirectory, "lease"), Array.Empty<byte>());
        ModelAsset stagedAsset = CreateModel("Traversal");
        stagedAsset.AuthoringAssetId = "00112233445566778899aabbccddeeff";
        byte[] stagedBytes = AssetSerializer.SerializeToBytes(stagedAsset);
        EditorAuthoringTransactionDocument document = new EditorAuthoringTransactionDocument {
            TransactionId = Path.GetFileName(transactionDirectory),
            State = EditorAuthoringTransactionState.Committing,
            Entries = new List<EditorAuthoringTransactionEntry> {
                new EditorAuthoringTransactionEntry {
                    DestinationRelativePath = "../outside.hasset",
                    StagedRelativePath = "staged/payload",
                    State = EditorAuthoringTransactionState.Committing,
                    Progress = EditorAuthoringTransactionEntryProgress.Applying,
                    Changed = true,
                    StagedContentHash = EditorNativeAssetWriteService.ComputeCanonicalNativeHash(
                        stagedBytes,
                        Path.Combine(ProjectRootPath, "assets", "outside.hasset")),
                    StagedSerializedHash = "sha256:" + Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(stagedBytes)).ToLowerInvariant(),
                    ExpectedAssetId = stagedAsset.AuthoringAssetId,
                    ExpectedAssetKind = "ModelAsset"
                }
            }
        };
        File.WriteAllBytes(stagedPath, stagedBytes);
        File.WriteAllText(
            Path.Combine(transactionDirectory, "transaction.json"),
            System.Text.Json.JsonSerializer.Serialize(document, EditorAuthoringTransactionDocument.JsonOptions));
        string markerPath = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions.pending");
        File.WriteAllText(
            markerPath,
            "{\"version\":1,\"transactionId\":\"" + document.TransactionId + "\",\"relativePaths\":[\"models/safe.hasset\"]}");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => CreateSession(ProjectRootPath));
        Assert.Contains("escapes its containing root", exception.Message, StringComparison.Ordinal);
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

    [Fact]
    public void StartupRecovery_RemovesMarkerFreeStagingWithoutPayloadProof() {
        string transactionRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions");
        string stagingDirectory = Path.Combine(transactionRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(stagingDirectory, "staged"));
        Directory.CreateDirectory(Path.Combine(stagingDirectory, "backups"));
        File.WriteAllBytes(Path.Combine(stagingDirectory, "lease"), Array.Empty<byte>());
        EditorAuthoringTransactionDocument document = new EditorAuthoringTransactionDocument {
            TransactionId = Path.GetFileName(stagingDirectory),
            State = EditorAuthoringTransactionState.Staging,
            Entries = new List<EditorAuthoringTransactionEntry> {
                new EditorAuthoringTransactionEntry {
                    DestinationRelativePath = "models/missing.hasset",
                    StagedRelativePath = "staged/missing.payload",
                    State = EditorAuthoringTransactionState.Staging,
                    Progress = EditorAuthoringTransactionEntryProgress.Staged,
                    Changed = true
                }
            }
        };
        File.WriteAllText(
            Path.Combine(stagingDirectory, "transaction.json"),
            System.Text.Json.JsonSerializer.Serialize(document, EditorAuthoringTransactionDocument.JsonOptions));

        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);

        Assert.False(Directory.Exists(stagingDirectory));
    }

    [Fact]
    public void StartupRecovery_RemovesPartialCreatingDirectoryWithoutManifestOrLease() {
        string transactionRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions");
        string creatingDirectory = Path.Combine(transactionRoot, ".creating-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(creatingDirectory, "staged"));
        File.WriteAllBytes(Path.Combine(creatingDirectory, "staged", "partial.payload"), new byte[] { 1, 2, 3 });

        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);

        Assert.False(Directory.Exists(creatingDirectory));
    }

    [Fact]
    public void StartupRecovery_FinishesPartialDeletingDirectoryWithoutManifestOrLease() {
        string transactionRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-transactions");
        string deletingDirectory = Path.Combine(transactionRoot, ".deleting-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(deletingDirectory, "backups"));
        File.WriteAllBytes(Path.Combine(deletingDirectory, "backups", "partial.payload"), new byte[] { 4, 5, 6 });

        using EditorProjectAuthoringSession session = CreateSession(ProjectRootPath);

        Assert.False(Directory.Exists(deletingDirectory));
    }

    [DirectoryLinkFact]
    public void Session_WhenTransactionContainerIsReparsePoint_RejectsBeforeOutsideMutation() {
        string outsideRoot = Path.Combine(Path.GetTempPath(), "helengine-authoring-transaction-outside-" + Guid.NewGuid().ToString("N"));
        string cacheRoot = Path.Combine(ProjectRootPath, "cache");
        Directory.CreateDirectory(outsideRoot);
        try {
            Directory.CreateSymbolicLink(cacheRoot, outsideRoot);

            Assert.ThrowsAny<Exception>(() => CreateSession(ProjectRootPath));
            Assert.False(Directory.EnumerateFileSystemEntries(outsideRoot).Any());
        } finally {
            if (Directory.Exists(cacheRoot)) {
                Directory.Delete(cacheRoot);
            }
            if (Directory.Exists(outsideRoot)) {
                Directory.Delete(outsideRoot, true);
            }
        }
    }

    static void WriteEmptyDocument(string transactionDirectory, EditorAuthoringTransactionState state) {
        Directory.CreateDirectory(Path.Combine(transactionDirectory, "staged"));
        Directory.CreateDirectory(Path.Combine(transactionDirectory, "backups"));
        File.WriteAllBytes(Path.Combine(transactionDirectory, "lease"), Array.Empty<byte>());
        EditorAuthoringTransactionDocument document = new EditorAuthoringTransactionDocument {
            TransactionId = Path.GetFileName(transactionDirectory),
            State = state,
            Entries = new List<EditorAuthoringTransactionEntry>()
        };
        File.WriteAllText(
            Path.Combine(transactionDirectory, "transaction.json"),
            System.Text.Json.JsonSerializer.Serialize(document, EditorAuthoringTransactionDocument.JsonOptions));
    }

    EditorProjectAuthoringSession CreateSession(string projectRootPath) {
        return CreateSession(projectRootPath, Array.Empty<IAssetImporterRegistration>());
    }

    EditorProjectAuthoringSession CreateSession(
        string projectRootPath,
        IReadOnlyList<IAssetImporterRegistration> importers) {
        Core core = new Core(new CoreInitializationOptions {
            ContentStreamSource = new HostFileSystemContentStreamSource(projectRootPath)
        });
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        TestGeneratedAssetGraph graph = new TestGeneratedAssetGraph(core);
        GeneratedGraphs.Add(graph);
        return new EditorProjectAuthoringSession(
            projectRootPath,
            importers,
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets"))),
            graph.Registry,
            graph.ModelCache,
            graph.MaterialCache,
            graph.RendererResources);
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

    static GeneratedMaterialAssetDefinition CreateGeneratedMaterial(string id) {
        GeneratedMaterialAssetDefinition definition = new GeneratedMaterialAssetDefinition {
            MaterialAsset = new ShaderMaterialAsset {
                Id = id,
                RenderState = new MaterialRenderState(),
                CastsShadows = true,
                ReceivesShadows = true
            }
        };
        GeneratedMaterialPlatformDefinition windows = definition.GetOrCreatePlatform("windows");
        windows.SchemaId = "standard-shader";
        windows.SetFieldValue("use-custom-shader", "false");
        windows.SetFieldValue("shader-asset-id", "ForwardStandardShader");
        windows.SetFieldValue("casts-shadow", "true");
        windows.SetFieldValue("receives-shadow", "true");
        windows.SetFieldValue("base-color", "#FFFFFFFF");
        windows.SetFieldValue("roughness", "0.5");
        GeneratedMaterialPlatformDefinition ps2 = definition.GetOrCreatePlatform("ps2");
        ps2.SchemaId = "ps2-simple-lit";
        ps2.SetFieldValue("double-sided", "true");
        return definition;
    }
}
