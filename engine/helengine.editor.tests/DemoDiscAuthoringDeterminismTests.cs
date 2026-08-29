using System.Security.Cryptography;
using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.platforms;
using helengine.projectfile;
using helengine.vulkan;

namespace helengine.editor.tests;

/// <summary>
/// Exercises the public project-authoring boundary with a small, demodisc-shaped
/// generator.  The fixture intentionally creates its project in a temporary root
/// so no checkout content or ambient project path participates in the proof.
/// </summary>
public sealed class DemoDiscAuthoringDeterminismTests : IDisposable {
    readonly string ProjectRootPath;
    readonly string CommandOutputRootPath;
    readonly string ProjectFilePath;
    readonly Core CoreValue;
    readonly TestGeneratedAssetGraph GeneratedAssetGraph;

    /// <summary>
    /// Creates one current-format generated-project fixture in an isolated root.
    /// </summary>
    public DemoDiscAuthoringDeterminismTests() {
        ProjectRootPath = Path.Combine(
            Path.GetTempPath(),
            "helengine-demodisc-authoring-" + Guid.NewGuid().ToString("N"));
        CommandOutputRootPath = Path.Combine(
            Path.GetTempPath(),
            "helengine-demodisc-authoring-command-" + Guid.NewGuid().ToString("N"));
        ProjectFilePath = Path.Combine(ProjectRootPath, "project.heproj");
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
        Directory.CreateDirectory(CommandOutputRootPath);
        File.WriteAllText(
            ProjectFilePath,
            "{\"projectFormatVersion\":1,\"name\":\"Deterministic fixture\",\"requiredEngineVersion\":\"0.4.0\",\"supportedPlatforms\":[\"windows\"],\"created\":\"2026-04-01T00:00:00Z\",\"lastOpened\":\"2026-04-20T00:00:00Z\",\"version\":\"1.0.0\"}");
        CoreValue = new Core(new CoreInitializationOptions {
            ContentStreamSource = new HostFileSystemContentStreamSource(ProjectRootPath)
        });
        CoreValue.Initialize(
            new TestRenderManager3D(),
            new TestRenderManager2D(),
            null,
            new PlatformInfo("test", "test-version"));
        GeneratedAssetGraph = new TestGeneratedAssetGraph(CoreValue);
    }

    /// <summary>
    /// Disposes the generated graph before removing the isolated project root.
    /// </summary>
    public void Dispose() {
        GeneratedAssetGraph.Dispose();
        CoreValue.Dispose();
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
        if (Directory.Exists(CommandOutputRootPath)) {
            Directory.Delete(CommandOutputRootPath, true);
        }
    }

    /// <summary>
    /// Ensures two complete public-session generation passes publish the same
    /// files, bytes, timestamps, references, and no-op write dispositions.
    /// </summary>
    [Fact]
    public void PublicGeneration_TwoIdenticalPassesHaveExactProjectSnapshotAndNoOpWrites() {
        GenerationPass first = RunGenerationPass();
        GenerationPass second = RunGenerationPass();

        Assert.True(first.Execution.Succeeded, first.Execution.Message);
        Assert.True(second.Execution.Succeeded, second.Execution.Message);
        Assert.True(first.GenerationAfter > first.GenerationBefore);
        Assert.Equal(first.GenerationAfter, second.GenerationBefore);
        Assert.Equal(first.GenerationAfter, second.GenerationAfter);
        Assert.NotEmpty(first.Changes);
        Assert.Empty(second.Changes);
        Assert.All(first.VerificationResults, result => Assert.Equal(EditorAssetWriteDisposition.Unchanged, result.Disposition));
        Assert.All(second.VerificationResults, result => Assert.Equal(EditorAssetWriteDisposition.Unchanged, result.Disposition));
        Assert.NotEmpty(first.Snapshot);
        Assert.Contains(first.Snapshot.Keys, path => path.Equals("assets/models/generated.hasset", StringComparison.Ordinal));
        Assert.Contains(first.Snapshot.Keys, path => path.Equals("assets/materials/generated.hasset", StringComparison.Ordinal));
        Assert.Empty(second.RepairRecords);
        Assert.Equal(first.ModelReference.AssetId, second.ModelReference.AssetId);
        Assert.Equal(first.MaterialReference.AssetId, second.MaterialReference.AssetId);
        AssertReferenceEqual(first.ModelResolution.CanonicalReference, second.ModelResolution.CanonicalReference);
        AssertSnapshotsEqual(first.Snapshot, second.Snapshot);
        AssertNoTransactionArtifacts(second.Snapshot);
    }

    /// <summary>
    /// Ensures a moved current external source is healed by identity and stays
    /// canonical on the next independent session.
    /// </summary>
    [Fact]
    public void MovedCurrentExternalSource_HealsPathAndSecondSessionIsStable() {
        string originalPath = Path.Combine(ProjectRootPath, "assets", "models", "before.obj");
        Directory.CreateDirectory(Path.GetDirectoryName(originalPath));
        File.WriteAllBytes(originalPath, new byte[] { 0x10, 0x20, 0x30, 0x40 });

        SceneAssetReference savedReference;
        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath)) {
            savedReference = session.CreateReference("models/before.obj", AssetEntryKind.Model);
        }

        string movedPath = Path.Combine(ProjectRootPath, "assets", "models", "after.obj");
        File.Move(originalPath, movedPath);
        File.Move(originalPath + ".hmeta", movedPath + ".hmeta");

        HealingPass first = RunHealingPass(savedReference);
        HealingPass second = RunHealingPass(first.Resolution.CanonicalReference);

        Assert.Equal("models/after.obj", first.Resolution.CanonicalReference.RelativePath);
        Assert.Equal(savedReference.AssetId, first.Resolution.CanonicalReference.AssetId);
        Assert.True(first.Resolution.ReferenceChanged);
        Assert.Equal(
            new[] { EditorAssetRepairKind.PathHealing, EditorAssetRepairKind.CanonicalReferenceRefresh },
            first.RepairRecords.Select(repair => repair.Kind));
        Assert.Equal(savedReference.AssetId, first.RepairRecords[0].PreviousAssetId);
        Assert.Equal(savedReference.AssetId, first.RepairRecords[0].CurrentAssetId);
        Assert.Equal(AssetReferenceResolutionTier.AssetId, first.RepairRecords[0].ResolutionTier);
        Assert.Equal(
            "current-id=True; saved-path=False; saved-hash=False; recorded-owner=True; path='models/after.obj'",
            first.RepairRecords[0].Evidence);
        Assert.Equal(string.Empty, first.RepairRecords[0].OwningDocument);
        Assert.Equal("Healed the saved asset path to the selected authored source.", first.RepairRecords[0].Diagnostic);
        Assert.Equal(EditorAssetRepairKind.CanonicalReferenceRefresh, first.RepairRecords[1].Kind);
        Assert.Equal("Refreshed the saved asset reference to its canonical identity, path, and hash.", first.RepairRecords[1].Diagnostic);
        Assert.False(second.Resolution.ReferenceChanged);
        Assert.Empty(second.RepairRecords);
        AssertSnapshotsEqual(first.Snapshot, second.Snapshot);
    }

    /// <summary>
    /// Ensures deleting current external metadata is repaired once, adopts a
    /// current identity for the saved source, and does not repeat on reload.
    /// </summary>
    [Fact]
    public void DeletedCurrentExternalMetadata_IsRecreatedAndSecondSessionIsStable() {
        string sourcePath = Path.Combine(ProjectRootPath, "assets", "models", "metadata.obj");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        File.WriteAllBytes(sourcePath, new byte[] { 0x51, 0x61, 0x71 });

        SceneAssetReference savedReference;
        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath)) {
            savedReference = session.CreateReference("models/metadata.obj", AssetEntryKind.Model);
        }
        File.Delete(sourcePath + ".hmeta");

        HealingPass first = RunHealingPass(savedReference);
        HealingPass second = RunHealingPass(first.Resolution.CanonicalReference);

        Assert.Equal("models/metadata.obj", first.Resolution.CanonicalReference.RelativePath);
        Assert.Equal(savedReference.AssetId, first.Resolution.CanonicalReference.AssetId);
        Assert.True(File.Exists(sourcePath + ".hmeta"));
        Assert.Equal(
            new[] { EditorAssetRepairKind.MissingExternalMetadataCreation, EditorAssetRepairKind.SavedIdAdoption },
            first.RepairRecords.Select(repair => repair.Kind));
        EditorAssetRepairRecord metadataCreation = first.RepairRecords[0];
        Assert.Equal("models/metadata.obj", metadataCreation.RelativePath);
        Assert.Equal(string.Empty, metadataCreation.PreviousAssetId);
        Assert.Matches("^[0-9a-f]{32}$", metadataCreation.CurrentAssetId);
        Assert.Null(metadataCreation.ResolutionTier);
        Assert.Equal("external identity document was missing", metadataCreation.Evidence);
        Assert.Equal(sourcePath + ".hmeta", metadataCreation.OwningDocument);
        Assert.Equal("Created missing external asset identity metadata.", metadataCreation.Diagnostic);
        EditorAssetRepairRecord adoption = first.RepairRecords[1];
        Assert.Equal("models/metadata.obj", adoption.RelativePath);
        Assert.Equal(metadataCreation.CurrentAssetId, adoption.PreviousAssetId);
        Assert.Equal(savedReference.AssetId, adoption.CurrentAssetId);
        Assert.Equal(AssetReferenceResolutionTier.Path, adoption.ResolutionTier);
        Assert.Equal("saved identity adopted by exact normalized path", adoption.Evidence);
        Assert.Equal(string.Empty, adoption.OwningDocument);
        Assert.Equal("Adopted the saved identity for the existing authored source.", adoption.Diagnostic);
        Assert.False(second.Resolution.ReferenceChanged);
        Assert.Empty(second.RepairRecords);
        AssertSnapshotsEqual(first.Snapshot, second.Snapshot);
    }

    /// <summary>
    /// Ensures a moved external source whose current metadata is deleted is
    /// recovered through its saved hash, with one deterministic repair batch.
    /// </summary>
    [Fact]
    public void DeletedCurrentExternalMetadata_AfterMove_RecoversByHashAndDoesNotRepeatRepair() {
        string originalPath = Path.Combine(ProjectRootPath, "assets", "models", "hash-before.obj");
        Directory.CreateDirectory(Path.GetDirectoryName(originalPath));
        byte[] sourceBytes = new byte[] { 0x31, 0x41, 0x59, 0x26 };
        File.WriteAllBytes(originalPath, sourceBytes);

        SceneAssetReference savedReference;
        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath)) {
            savedReference = session.CreateReference("models/hash-before.obj", AssetEntryKind.Model);
        }

        string movedPath = Path.Combine(ProjectRootPath, "assets", "models", "hash-after.obj");
        File.Move(originalPath, movedPath);
        File.Move(originalPath + ".hmeta", movedPath + ".hmeta");
        File.Delete(movedPath + ".hmeta");

        HealingPass first = RunHealingPass(savedReference);
        HealingPass second = RunHealingPass(first.Resolution.CanonicalReference);

        Assert.Equal("models/hash-after.obj", first.Resolution.CanonicalReference.RelativePath);
        Assert.NotEqual(savedReference.AssetId, first.Resolution.CanonicalReference.AssetId);
        Assert.Equal(savedReference.ContentHash, first.Resolution.CanonicalReference.ContentHash);
        Assert.Equal(
            new[] {
                EditorAssetRepairKind.MissingExternalMetadataCreation,
                EditorAssetRepairKind.PathHealing,
                EditorAssetRepairKind.CanonicalReferenceRefresh
            },
            first.RepairRecords.Select(repair => repair.Kind));
        EditorAssetRepairRecord metadataCreation = first.RepairRecords[0];
        Assert.Equal("models/hash-after.obj", metadataCreation.RelativePath);
        Assert.Equal(string.Empty, metadataCreation.PreviousAssetId);
        Assert.Matches("^[0-9a-f]{32}$", metadataCreation.CurrentAssetId);
        Assert.Null(metadataCreation.ResolutionTier);
        Assert.Equal("external identity document was missing", metadataCreation.Evidence);
        Assert.Equal(movedPath + ".hmeta", metadataCreation.OwningDocument);
        Assert.Equal("Created missing external asset identity metadata.", metadataCreation.Diagnostic);
        for (int index = 1; index < first.RepairRecords.Count; index++) {
            EditorAssetRepairRecord repair = first.RepairRecords[index];
            Assert.Equal("models/hash-after.obj", repair.RelativePath);
            Assert.Equal(savedReference.AssetId, repair.PreviousAssetId);
            Assert.Equal(metadataCreation.CurrentAssetId, repair.CurrentAssetId);
            Assert.Equal(AssetReferenceResolutionTier.ContentHash, repair.ResolutionTier);
            Assert.Equal(
                "current-id=False; saved-path=False; saved-hash=True; recorded-owner=False; path='models/hash-after.obj'",
                repair.Evidence);
            Assert.Equal(string.Empty, repair.OwningDocument);
        }
        Assert.Equal("Healed the saved asset path to the selected authored source.", first.RepairRecords[1].Diagnostic);
        Assert.Equal("Refreshed the saved asset reference to its canonical identity, path, and hash.", first.RepairRecords[2].Diagnostic);
        Assert.False(second.Resolution.ReferenceChanged);
        Assert.Empty(second.RepairRecords);
        AssertSnapshotsEqual(first.Snapshot, second.Snapshot);
    }

    /// <summary>
    /// Ensures a copied current native identity keeps the deterministic ordinal
    /// owner while the copy receives a fresh identity and remains independently
    /// writable through the public session boundary.
    /// </summary>
    [Fact]
    public void DuplicateCurrentNativeIdentity_SelectsDeterministicOwnerAndRekeysCopy() {
        EditorAssetWriteResult seeded;
        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath)) {
            seeded = session.WriteAsset("models/a-owner.hasset", CreateModel("Owner", 0f));
        }

        string ownerPath = seeded.FullPath;
        string copyPath = Path.Combine(ProjectRootPath, "assets", "models", "z-copy.hasset");
        File.Copy(ownerPath, copyPath);

        DuplicatePass first = RunDuplicatePass(true);
        DuplicatePass second = RunDuplicatePass(false);

        Assert.Equal("models/a-owner.hasset", first.OwnerReference.RelativePath);
        Assert.Equal(seeded.AssetId, first.OwnerReference.AssetId);
        Assert.NotEqual(first.OwnerReference.AssetId, first.CopyReference.AssetId);
        AssertRepair(first.RepairRecords, EditorAssetRepairKind.DuplicateIdReassignment, "models/z-copy.hasset", null);
        EditorAssetRepairRecord reassignment = Assert.Single(first.RepairRecords, repair =>
            repair.Kind == EditorAssetRepairKind.DuplicateIdReassignment
            && repair.RelativePath == "models/z-copy.hasset");
        Assert.Equal(seeded.AssetId, reassignment.PreviousAssetId);
        Assert.Equal(first.CopyReference.AssetId, reassignment.CurrentAssetId);
        Assert.Contains("owner", reassignment.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.All(first.RepairRecords, AssertCompleteRepairRecord);
        Assert.Empty(second.RepairRecords);
        AssertReferenceEqual(first.OwnerReference, second.OwnerReference);
        AssertReferenceEqual(first.CopyReference, second.CopyReference);
        AssertSnapshotsEqual(first.Snapshot, second.Snapshot);
    }

    /// <summary>
    /// Ensures copied external identity metadata is repaired independently of
    /// source insertion order, without prompting, while importer settings stay
    /// attached to their own source paths.
    /// </summary>
    [Fact]
    public void DuplicateCurrentExternalIdentity_WithCopiedMetadataAndDifferentSettings_IsOrderIndependent() {
        string firstRoot = CreateIndependentProjectRoot();
        string secondRoot = CreateIndependentProjectRoot();
        const string copiedAssetId = "00112233445566778899aabbccddeeff";
        try {
            PrepareExternalDuplicateFixture(firstRoot, false, copiedAssetId);
            PrepareExternalDuplicateFixture(secondRoot, true, copiedAssetId);

            ExternalDuplicatePass first = RunExternalDuplicatePass(firstRoot);
            ExternalDuplicatePass second = RunExternalDuplicatePass(secondRoot);

            AssertExternalDuplicatePass(first, copiedAssetId);
            AssertExternalDuplicatePass(second, copiedAssetId);
            Assert.Equal(first.OwnerReference.RelativePath, second.OwnerReference.RelativePath);
            Assert.Equal(first.RepairRecords[0].Evidence, second.RepairRecords[0].Evidence);
            Assert.Equal(first.RepairRecords[0].ResolutionTier, second.RepairRecords[0].ResolutionTier);

            AssertExternalSettingsRemainIndependent(firstRoot, first.CopyReference.AssetId);
            AssertExternalSettingsRemainIndependent(secondRoot, second.CopyReference.AssetId);

            AliasCompetitionPass firstAlias = RunCurrentFormerAliasCompetition(firstRoot, first.OwnerReference.ContentHash, copiedAssetId);
            AliasCompetitionPass secondAlias = RunCurrentFormerAliasCompetition(secondRoot, second.OwnerReference.ContentHash, copiedAssetId);
            AssertCurrentIdentityWinsFormerAlias(firstAlias, copiedAssetId);
            AssertCurrentIdentityWinsFormerAlias(secondAlias, copiedAssetId);
            Assert.Equal(firstAlias.Resolution.CanonicalReference.RelativePath, secondAlias.Resolution.CanonicalReference.RelativePath);
            Assert.Equal(firstAlias.Resolution.Tier, secondAlias.Resolution.Tier);
        } finally {
            DeleteIndependentProjectRoot(firstRoot);
            DeleteIndependentProjectRoot(secondRoot);
        }
    }

    static string CreateIndependentProjectRoot() {
        string root = Path.Combine(Path.GetTempPath(), "helengine-demodisc-duplicate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets", "models"));
        return root;
    }

    static void DeleteIndependentProjectRoot(string root) {
        if (Directory.Exists(root)) {
            Directory.Delete(root, true);
        }
    }

    void PrepareExternalDuplicateFixture(string root, bool reverseCreationOrder, string copiedAssetId) {
        string ownerPath = Path.Combine(root, "assets", "models", "a-owner.obj");
        string copyPath = Path.Combine(root, "assets", "models", "z-copy.obj");
        string[] creationOrder = reverseCreationOrder
            ? new[] { copyPath, ownerPath }
            : new[] { ownerPath, copyPath };
        for (int index = 0; index < creationOrder.Length; index++) {
            File.WriteAllBytes(creationOrder[index], index == 0
                ? new byte[] { 0x10, 0x20, 0x30 }
                : new byte[] { 0x40, 0x50, 0x60 });
        }

        AssetIdentityMetadataService metadata = new AssetIdentityMetadataService(root);
        metadata.Save(ownerPath, new AssetIdentityMetadataDocument {
            AssetId = copiedAssetId,
            FormerAssetIds = new List<string>()
        });

        IReadOnlyList<IAssetImporterRegistration> importers = CreateExternalFixtureImporters();
        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(root, importers)) {
            ModelAssetImportSettings ownerSettings = session.LoadOrCreateModelImportSettings(ownerPath);
            ownerSettings.Importer.ImporterId = "fixture-model";
            ownerSettings.Processor.Platforms["windows"] = new ModelAssetProcessorSettings {
                FlipWinding = false,
                Tessellate = false,
                TessellationMaxEdgeLength = 1.0d
            };
            session.SaveModelImportSettings(ownerPath, ownerSettings);
        }

        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(root, importers)) {
            ModelAssetImportSettings copySettings = session.LoadOrCreateModelImportSettings(copyPath);
            copySettings.Importer.ImporterId = "fixture-model";
            copySettings.Processor.Platforms["windows"] = new ModelAssetProcessorSettings {
                FlipWinding = true,
                Tessellate = true,
                TessellationMaxEdgeLength = 2.0d
            };
            session.SaveModelImportSettings(copyPath, copySettings);
        }

        File.Copy(ownerPath + ".hmeta", copyPath + ".hmeta", true);
    }

    ExternalDuplicatePass RunExternalDuplicatePass(string root) {
        using IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(root, CreateExternalFixtureImporters());
        string ownerPath = OperatingSystem.IsWindows() ? "MODELS/A-OWNER.obj" : "models/a-owner.obj";
        string copyPath = OperatingSystem.IsWindows() ? "MODELS/Z-COPY.obj" : "models/z-copy.obj";
        SceneAssetReference ownerReference = session.CreateReference(ownerPath, AssetEntryKind.Model);
        SceneAssetReference copyReference = session.CreateReference(copyPath, AssetEntryKind.Model);
        return new ExternalDuplicatePass(root, ownerReference, copyReference, session.RepairReport.Snapshot);
    }

    AliasCompetitionPass RunCurrentFormerAliasCompetition(string root, string ownerHash, string copiedAssetId) {
        using IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(root, CreateExternalFixtureImporters());
        SceneAssetReference savedReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            copiedAssetId,
            OperatingSystem.IsWindows() ? "MODELS/MISSING.obj" : "models/missing.obj",
            ownerHash);
        AssetReferenceResolution resolution = session.ResolveReference(savedReference, AssetEntryKind.Model);
        return new AliasCompetitionPass(resolution, session.RepairReport.Snapshot);
    }

    static IReadOnlyList<IAssetImporterRegistration> CreateExternalFixtureImporters() {
        return new IAssetImporterRegistration[] {
            new ModelImporterRegistration("fixture-model", new TestModelImporter(), new[] { ".obj" })
        };
    }

    static void AssertExternalDuplicatePass(ExternalDuplicatePass pass, string copiedAssetId) {
        Assert.Equal("models/a-owner.obj", pass.OwnerReference.RelativePath);
        Assert.Equal(copiedAssetId, pass.OwnerReference.AssetId);
        Assert.NotEqual(copiedAssetId, pass.CopyReference.AssetId);
        Assert.Matches("^[0-9a-f]{32}$", pass.CopyReference.AssetId);
        AssetIdentityMetadataDocument copyMetadata = new AssetIdentityMetadataService(pass.CopyRootPath)
            .Load(Path.Combine(pass.CopyRootPath, "assets", "models", "z-copy.obj"));
        Assert.Equal(pass.CopyReference.AssetId, copyMetadata.AssetId);
        Assert.Equal(new[] { copiedAssetId }, copyMetadata.FormerAssetIds);
        Assert.Equal(
            new[] { EditorAssetRepairKind.DuplicateIdReassignment },
            pass.RepairRecords.Select(repair => repair.Kind));
        EditorAssetRepairRecord repair = pass.RepairRecords[0];
        Assert.Equal("models/z-copy.obj", repair.RelativePath);
        Assert.Equal(copiedAssetId, repair.PreviousAssetId);
        Assert.Equal(pass.CopyReference.AssetId, repair.CurrentAssetId);
        Assert.Null(repair.ResolutionTier);
        Assert.Equal("selected ordinal owner path='models/a-owner.obj'", repair.Evidence);
        Assert.Equal(Path.Combine(pass.CopyRootPath, "assets", "models", "z-copy.obj.hmeta"), repair.OwningDocument);
        Assert.Equal("Reassigned copied identity to the non-owning asset.", repair.Diagnostic);
    }

    void AssertExternalSettingsRemainIndependent(string root, string copyAssetId) {
        string ownerPath = Path.Combine(root, "assets", "models", "a-owner.obj");
        string copyPath = Path.Combine(root, "assets", "models", "z-copy.obj");
        using IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(root, CreateExternalFixtureImporters());
        ModelAssetImportSettings ownerSettings = session.LoadOrCreateModelImportSettings(ownerPath);
        ModelAssetImportSettings copySettings = session.LoadOrCreateModelImportSettings(copyPath);
        Assert.Equal("fixture-model", ownerSettings.Importer.ImporterId);
        Assert.Equal("fixture-model", copySettings.Importer.ImporterId);
        Assert.False(ownerSettings.Processor.Platforms["windows"].FlipWinding);
        Assert.True(copySettings.Processor.Platforms["windows"].FlipWinding);
        Assert.True(copySettings.Processor.Platforms["windows"].Tessellate);
        Assert.Equal(copyAssetId, new AssetIdentityMetadataService(root).Load(copyPath).AssetId);
    }

    static void AssertCurrentIdentityWinsFormerAlias(AliasCompetitionPass pass, string copiedAssetId) {
        Assert.Equal(copiedAssetId, pass.Resolution.CanonicalReference.AssetId);
        Assert.Equal("models/a-owner.obj", pass.Resolution.CanonicalReference.RelativePath);
        Assert.Equal(AssetReferenceResolutionTier.AssetId, pass.Resolution.Tier);
        Assert.True(pass.Resolution.CandidateEvidence.IsCurrentId);
        Assert.Equal(
            new[] { EditorAssetRepairKind.PathHealing, EditorAssetRepairKind.CanonicalReferenceRefresh },
            pass.RepairRecords.Select(repair => repair.Kind));
        Assert.Equal(
            "current-id=True; saved-path=False; saved-hash=False; recorded-owner=True; path='models/a-owner.obj'",
            pass.RepairRecords[0].Evidence);
        Assert.Equal(AssetReferenceResolutionTier.AssetId, pass.RepairRecords[0].ResolutionTier);
        Assert.Equal(copiedAssetId, pass.RepairRecords[0].PreviousAssetId);
        Assert.Equal(copiedAssetId, pass.RepairRecords[0].CurrentAssetId);
        Assert.Equal("models/a-owner.obj", pass.RepairRecords[0].RelativePath);
        Assert.Equal(string.Empty, pass.RepairRecords[0].OwningDocument);
        Assert.Equal("Healed the saved asset path to the selected authored source.", pass.RepairRecords[0].Diagnostic);
        Assert.Equal(EditorAssetRepairKind.CanonicalReferenceRefresh, pass.RepairRecords[1].Kind);
        Assert.Equal("Refreshed the saved asset reference to its canonical identity, path, and hash.", pass.RepairRecords[1].Diagnostic);
    }

    GenerationPass RunGenerationPass() {
        long generationBefore = EditorProjectWriteGeneration.Read(ProjectRootPath);
        EditorBuildExecutionResult execution;
        IReadOnlyList<EditorAssetRepairRecord> repairRecords;
        using (IEditorProjectAuthoringSession authoring = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath))
        using (EditorGameScriptAssemblyHost commandHost = CreateDiscoveredCommandHost()) {
            EditorProjectCommandDescriptor command = Assert.Single(
                commandHost.GetAvailableEditorCommands(),
                descriptor => string.Equals(
                    descriptor.CommandId,
                    DeterministicDemodiscAuthoringCommand.CommandIdValue,
                    StringComparison.Ordinal));
            execution = new EditorCliCommandRunner(
                CreateEditorFont(),
                new EditorProjectAssetAuthoringServiceFactory(Array.Empty<IAssetImporterRegistration>())).RunInSessionGraph(
                    CreateFixtureBootstrap(),
                    new EditorCliCommandOptions(ProjectFilePath, command.CommandId),
                    authoring,
                    CreateBackends(),
                    CoreValue,
                    GeneratedAssetGraph.InteractionServices,
                    GeneratedAssetGraph.RendererResources,
                    GeneratedAssetGraph.Registry,
                    new DiscoveredCommandCatalog(commandHost),
                    commandHost.ScriptTypeResolver);
            repairRecords = authoring.RepairReport.Snapshot;
        }

        long generationAfter = EditorProjectWriteGeneration.Read(ProjectRootPath);
        IReadOnlyList<EditorProjectWriteChange> changes = EditorProjectWriteGeneration
            .ReadAfter(ProjectRootPath, generationBefore);

        List<EditorAssetWriteResult> verificationResults = new List<EditorAssetWriteResult>();
        SceneAssetReference modelReference;
        SceneAssetReference materialReference;
        AssetReferenceResolution modelResolution;
        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath)) {
            verificationResults.Add(session.WriteAsset("models/generated.hasset", CreateModel("Generated", 1f)));
            verificationResults.Add(session.WriteAsset("models/generated-copy.hasset", CreateModel("GeneratedCopy", 2f)));
            session.WriteNativeMaterial("materials/generated.hasset", CreateMaterial("GeneratedMaterial"));
            modelReference = session.CreateReference("models/generated.hasset", AssetEntryKind.Model);
            materialReference = session.CreateReference("materials/generated.hasset", AssetEntryKind.Material);
            modelResolution = session.ResolveReference(modelReference, AssetEntryKind.Model);
        }

        return new GenerationPass(
            CaptureProjectSnapshot(ProjectRootPath),
            execution,
            generationBefore,
            generationAfter,
            changes,
            verificationResults,
            modelReference,
            materialReference,
            modelResolution,
            repairRecords);
    }

    HealingPass RunHealingPass(SceneAssetReference reference) {
        AssetReferenceResolution resolution;
        IReadOnlyList<EditorAssetRepairRecord> repairRecords;
        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath)) {
            resolution = session.ResolveReference(reference, AssetEntryKind.Model);
            repairRecords = session.RepairReport.Snapshot;
        }
        return new HealingPass(CaptureProjectSnapshot(ProjectRootPath), resolution, repairRecords);
    }

    DuplicatePass RunDuplicatePass(bool expectIndependentCopyChange) {
        SceneAssetReference ownerReference;
        SceneAssetReference copyReference;
        IReadOnlyList<EditorAssetRepairRecord> repairRecords;
        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath)) {
            ownerReference = session.CreateReference("models/a-owner.hasset", AssetEntryKind.Model);
            copyReference = session.CreateReference("models/z-copy.hasset", AssetEntryKind.Model);
            EditorAssetWriteResult independentCopy = session.WriteAsset(
                "models/z-copy.hasset",
                CreateModel("IndependentCopy", 3f));
            Assert.Equal(copyReference.AssetId, independentCopy.AssetId);
            Assert.Equal(
                expectIndependentCopyChange
                    ? EditorAssetWriteDisposition.Changed
                    : EditorAssetWriteDisposition.Unchanged,
                independentCopy.Disposition);
            copyReference = session.CreateReference("models/z-copy.hasset", AssetEntryKind.Model);
            repairRecords = session.RepairReport.Snapshot;
        }
        return new DuplicatePass(CaptureProjectSnapshot(ProjectRootPath), ownerReference, copyReference, repairRecords);
    }

    EditorGameScriptAssemblyHost CreateDiscoveredCommandHost() {
        string outputDirectoryPath = Path.Combine(CommandOutputRootPath, "fixture.editor", "Debug", "net9.0");
        Directory.CreateDirectory(outputDirectoryPath);
        string assemblyPath = Path.Combine(outputDirectoryPath, "fixture.editor.dll");
        File.Copy(typeof(DeterministicDemodiscAuthoringCommand).Assembly.Location, assemblyPath, true);

        EditorGameScriptAssemblyHost host = new EditorGameScriptAssemblyHost(ProjectRootPath);
        try {
            host.Reload(new[] {
                new EditorScriptAssemblyDescriptor(
                    "fixture.editor",
                    outputDirectoryPath,
                    assemblyPath,
                    EditorCodeModuleKind.Editor)
            });
            return host;
        } catch {
            host.Dispose();
            throw;
        }
    }

    EditorProjectBootstrapContext CreateFixtureBootstrap() {
        ProjectFileDocument projectDocument = new ProjectFileDocument {
            Name = "Deterministic fixture",
            Version = "1.0.0",
            RequiredEngineVersion = "0.4.0",
            SupportedPlatforms = new List<string> { "windows" }
        };
        return new EditorProjectBootstrapContext(
            ProjectFilePath,
            ProjectRootPath,
            "project.heproj",
            projectDocument,
            projectDocument.SupportedPlatforms,
            Array.Empty<AvailablePlatformDescriptor>(),
            new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions()),
            new EditorPlatformCatalogService(Array.Empty<AvailablePlatformDescriptor>()),
            new EditorProjectSceneCatalogService(ProjectRootPath),
            new EditorBuildConfigService(ProjectRootPath),
            new EditorProfileSettingsService(ProjectRootPath));
    }

    static ShaderBackendRegistry CreateBackends() {
        ShaderBackendRegistry registry = new ShaderBackendRegistry();
        registry.Register(new DirectX11ShaderBackend());
        registry.Register(new VulkanShaderBackend());
        return registry;
    }

    static FontAsset CreateEditorFont() {
        Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
        const string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?+-_[]()/'\\\\=<>";
        for (int index = 0; index < glyphs.Length; index++) {
            char glyph = glyphs[index];
            if (!characters.ContainsKey(glyph)) {
                float width = glyph == ' ' ? 4f : 8f;
                characters.Add(glyph, new FontChar(new float4(0f, 0f, width, 12f), 0f, width, 0f, 0f));
            }
        }

        return new FontAsset(
            new FontInfo("Deterministic fixture", 16, 4f),
            new TestRuntimeTexture { Width = 16, Height = 16 },
            characters,
            16f,
            64,
            64);
    }

    static ModelAsset CreateModel(string id, float positionOffset) {
        return new ModelAsset {
            Id = id,
            Positions = new[] {
                new float3(positionOffset, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f)
            },
            Normals = new[] {
                new float3(0f, 0f, 1f),
                new float3(0f, 0f, 1f),
                new float3(0f, 0f, 1f)
            },
            TexCoords = new[] {
                new float2(0f, 0f),
                new float2(1f, 0f),
                new float2(0f, 1f)
            },
            Indices16 = new ushort[] { 0, 1, 2 },
            Indices32 = Array.Empty<uint>(),
            Submeshes = Array.Empty<ModelSubmeshAsset>()
        };
    }

    static GeneratedMaterialAssetDefinition CreateMaterial(string id) {
        GeneratedMaterialAssetDefinition definition = new GeneratedMaterialAssetDefinition {
            MaterialAsset = new MaterialAsset {
                Id = id,
                RenderState = new MaterialRenderState(),
                CastsShadows = true,
                ReceivesShadows = true
            }
        };
        GeneratedMaterialPlatformDefinition windows = definition.GetOrCreatePlatform("windows");
        windows.SchemaId = "standard-shader";
        windows.SetFieldValue("use-custom-shader", "false");
        windows.SetFieldValue("casts-shadow", "true");
        windows.SetFieldValue("receives-shadow", "true");
        windows.SetFieldValue("base-color", "#FFFFFFFF");
        GeneratedMaterialPlatformDefinition ps2 = definition.GetOrCreatePlatform("ps2");
        ps2.SchemaId = "ps2-simple-lit";
        ps2.SetFieldValue("double-sided", "true");
        return definition;
    }

    static IReadOnlyDictionary<string, AuthoredFileSnapshot> CaptureProjectSnapshot(string projectRootPath) {
        Dictionary<string, AuthoredFileSnapshot> snapshot = new Dictionary<string, AuthoredFileSnapshot>(StringComparer.Ordinal);
        if (!Directory.Exists(projectRootPath)) {
            return snapshot;
        }

        foreach (string fullPath in Directory.EnumerateFiles(projectRootPath, "*", SearchOption.AllDirectories)) {
            string relativePath = Path.GetRelativePath(projectRootPath, fullPath).Replace('\\', '/');
            byte[] bytes = File.ReadAllBytes(fullPath);
            snapshot.Add(relativePath, new AuthoredFileSnapshot(
                bytes,
                SHA256.HashData(bytes),
                File.GetLastWriteTimeUtc(fullPath)));
        }
        return snapshot;
    }

    static void AssertSnapshotsEqual(
        IReadOnlyDictionary<string, AuthoredFileSnapshot> expected,
        IReadOnlyDictionary<string, AuthoredFileSnapshot> actual) {
        Assert.Equal(
            expected.Keys.OrderBy(path => path, StringComparer.Ordinal),
            actual.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (string path in expected.Keys.OrderBy(path => path, StringComparer.Ordinal)) {
            AuthoredFileSnapshot expectedFile = expected[path];
            AuthoredFileSnapshot actualFile = actual[path];
            Assert.True(
                expectedFile.Bytes.SequenceEqual(actualFile.Bytes),
                $"Bytes changed for '{path}'.");
            Assert.True(
                expectedFile.Hash.SequenceEqual(actualFile.Hash),
                $"Hash changed for '{path}'.");
            Assert.True(
                expectedFile.LastWriteTimeUtc == actualFile.LastWriteTimeUtc,
                $"Timestamp changed for '{path}': {expectedFile.LastWriteTimeUtc:o} -> {actualFile.LastWriteTimeUtc:o}");
        }
    }

    static void AssertReferenceEqual(SceneAssetReference expected, SceneAssetReference actual) {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.AssetId, actual.AssetId);
        Assert.Equal(expected.RelativePath, actual.RelativePath);
        Assert.Equal(expected.ContentHash, actual.ContentHash);
        Assert.Equal(expected.ProviderId, actual.ProviderId);
        Assert.Equal(expected.SourceKind, actual.SourceKind);
    }

    static void AssertNoTransactionArtifacts(IReadOnlyDictionary<string, AuthoredFileSnapshot> snapshot) {
        Assert.DoesNotContain(snapshot.Keys, path => path.Contains("authoring-transactions", StringComparison.Ordinal));
        Assert.DoesNotContain(snapshot.Keys, path => path.Contains(".creating-", StringComparison.Ordinal));
        Assert.DoesNotContain(snapshot.Keys, path => path.Contains(".deleting-", StringComparison.Ordinal));
        Assert.DoesNotContain(snapshot.Keys, path => path.EndsWith("authoring-transactions.pending", StringComparison.Ordinal));
    }

    static void AssertRepair(
        IReadOnlyList<EditorAssetRepairRecord> records,
        EditorAssetRepairKind kind,
        string relativePath,
        AssetReferenceResolutionTier? tier) {
        EditorAssetRepairRecord repair = Assert.Single(records, item =>
            item.Kind == kind && item.RelativePath == relativePath);
        if (tier.HasValue) {
            Assert.Equal(tier, repair.ResolutionTier);
        }
        Assert.NotEqual(string.Empty, repair.Diagnostic);
    }

    static void AssertCompleteRepairRecord(EditorAssetRepairRecord repair) {
        Assert.NotEqual(string.Empty, repair.RelativePath);
        Assert.NotEqual(string.Empty, repair.Diagnostic);
        if (repair.ResolutionTier.HasValue) {
            Assert.NotEqual(string.Empty, repair.Evidence);
        }
    }

    sealed record AuthoredFileSnapshot(byte[] Bytes, byte[] Hash, DateTime LastWriteTimeUtc);

    sealed class DiscoveredCommandCatalog : IEditorProjectCommandCatalogProvider {
        readonly EditorGameScriptAssemblyHost Host;

        public DiscoveredCommandCatalog(EditorGameScriptAssemblyHost host) {
            Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public IReadOnlyList<EditorProjectCommandDescriptor> GetAvailableEditorCommands() {
            return Host.GetAvailableEditorCommands();
        }
    }

    sealed record GenerationPass(
        IReadOnlyDictionary<string, AuthoredFileSnapshot> Snapshot,
        EditorBuildExecutionResult Execution,
        long GenerationBefore,
        long GenerationAfter,
        IReadOnlyList<EditorProjectWriteChange> Changes,
        IReadOnlyList<EditorAssetWriteResult> VerificationResults,
        SceneAssetReference ModelReference,
        SceneAssetReference MaterialReference,
        AssetReferenceResolution ModelResolution,
        IReadOnlyList<EditorAssetRepairRecord> RepairRecords);

    sealed record HealingPass(
        IReadOnlyDictionary<string, AuthoredFileSnapshot> Snapshot,
        AssetReferenceResolution Resolution,
        IReadOnlyList<EditorAssetRepairRecord> RepairRecords);

    sealed record DuplicatePass(
        IReadOnlyDictionary<string, AuthoredFileSnapshot> Snapshot,
        SceneAssetReference OwnerReference,
        SceneAssetReference CopyReference,
        IReadOnlyList<EditorAssetRepairRecord> RepairRecords);

    sealed record ExternalDuplicatePass(
        string CopyRootPath,
        SceneAssetReference OwnerReference,
        SceneAssetReference CopyReference,
        IReadOnlyList<EditorAssetRepairRecord> RepairRecords);

    sealed record AliasCompetitionPass(
        AssetReferenceResolution Resolution,
        IReadOnlyList<EditorAssetRepairRecord> RepairRecords);
}

/// <summary>
/// Compiled fixture command loaded by the editor script assembly host during
/// the deterministic authoring integration test.
/// </summary>
internal sealed class DeterministicDemodiscAuthoringCommand : IEditorCommand {
    /// <summary>Stable command identifier discovered from the fixture assembly.</summary>
    public const string CommandIdValue = "fixture.generate-deterministic-assets";

    /// <summary>Gets the stable fixture command identifier.</summary>
    public string CommandId => CommandIdValue;

    /// <summary>Gets the command label surfaced by the editor catalog.</summary>
    public string DisplayName => "Generate deterministic fixture assets";

    /// <summary>
    /// Authors the minimal demodisc-shaped output through one host-owned
    /// transaction and the public command context only.
    /// </summary>
    /// <param name="context">Editor command context supplied by the host.</param>
    public void Execute(IEditorCommandContext context) {
        using EditorAuthoringTransaction transaction = context.Authoring.BeginTransaction();
        transaction.WriteAsset("models/generated.hasset", CreateModel("Generated", 1f));
        transaction.WriteAsset("models/generated-copy.hasset", CreateModel("GeneratedCopy", 2f));
        transaction.WriteMaterial("materials/generated.hasset", CreateMaterial("GeneratedMaterial"));
        transaction.Commit();
    }

    static ModelAsset CreateModel(string id, float positionOffset) {
        return new ModelAsset {
            Id = id,
            Positions = new[] {
                new float3(positionOffset, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f)
            },
            Normals = new[] {
                new float3(0f, 0f, 1f),
                new float3(0f, 0f, 1f),
                new float3(0f, 0f, 1f)
            },
            TexCoords = new[] {
                new float2(0f, 0f),
                new float2(1f, 0f),
                new float2(0f, 1f)
            },
            Indices16 = new ushort[] { 0, 1, 2 },
            Indices32 = Array.Empty<uint>(),
            Submeshes = Array.Empty<ModelSubmeshAsset>()
        };
    }

    static GeneratedMaterialAssetDefinition CreateMaterial(string id) {
        GeneratedMaterialAssetDefinition definition = new GeneratedMaterialAssetDefinition {
            MaterialAsset = new MaterialAsset {
                Id = id,
                RenderState = new MaterialRenderState(),
                CastsShadows = true,
                ReceivesShadows = true
            }
        };
        GeneratedMaterialPlatformDefinition windows = definition.GetOrCreatePlatform("windows");
        windows.SchemaId = "standard-shader";
        windows.SetFieldValue("use-custom-shader", "false");
        windows.SetFieldValue("casts-shadow", "true");
        windows.SetFieldValue("receives-shadow", "true");
        windows.SetFieldValue("base-color", "#FFFFFFFF");
        GeneratedMaterialPlatformDefinition ps2 = definition.GetOrCreatePlatform("ps2");
        ps2.SchemaId = "ps2-simple-lit";
        ps2.SetFieldValue("double-sided", "true");
        return definition;
    }
}
