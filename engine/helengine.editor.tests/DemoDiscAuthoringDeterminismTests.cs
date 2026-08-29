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
    static readonly object CommandObservationGate = new object();
    readonly string ProjectRootPath;
    readonly string GeneratedOutputRootPath;
    readonly string GeneratedWorkspaceRootPath;
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
        GeneratedOutputRootPath = Path.Combine(
            Path.GetTempPath(),
            "helengine-demodisc-authoring-output-" + Guid.NewGuid().ToString("N"));
        GeneratedWorkspaceRootPath = Path.Combine(
            Path.GetTempPath(),
            "helengine-demodisc-authoring-workspace-" + Guid.NewGuid().ToString("N"));
        ProjectFilePath = Path.Combine(ProjectRootPath, "project.heproj");
        CopyFixtureProject();
        Directory.CreateDirectory(GeneratedOutputRootPath);
        Directory.CreateDirectory(GeneratedWorkspaceRootPath);
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
        if (Directory.Exists(GeneratedOutputRootPath)) {
            Directory.Delete(GeneratedOutputRootPath, true);
        }
        if (Directory.Exists(GeneratedWorkspaceRootPath)) {
            Directory.Delete(GeneratedWorkspaceRootPath, true);
        }
    }

    void CopyFixtureProject() {
        string fixtureRootPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "demodisc-authoring");
        if (!Directory.Exists(fixtureRootPath)) {
            throw new DirectoryNotFoundException($"Copied fixture source was not found: {fixtureRootPath}");
        }

        CopyDirectory(fixtureRootPath, ProjectRootPath);
    }

    static void CopyDirectory(string sourceRootPath, string destinationRootPath) {
        Directory.CreateDirectory(destinationRootPath);
        foreach (string sourceFilePath in Directory.EnumerateFiles(sourceRootPath, "*", SearchOption.AllDirectories)) {
            string relativePath = Path.GetRelativePath(sourceRootPath, sourceFilePath);
            string destinationFilePath = Path.Combine(destinationRootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));
            File.Copy(sourceFilePath, destinationFilePath, true);
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
        Assert.Equal(
            new[] { "models/generated.hasset", "models/generated-copy.hasset", "materials/generated.hasset" },
            first.CommandWrites.Select(write => write.RelativePath));
        Assert.Equal(
            new[] { "models/generated.hasset", "models/generated-copy.hasset", "materials/generated.hasset" },
            second.CommandWrites.Select(write => write.RelativePath));
        Assert.All(first.CommandWrites, write => Assert.Equal(EditorAssetWriteDisposition.Created, write.Disposition));
        Assert.All(second.CommandWrites, write => Assert.Equal(EditorAssetWriteDisposition.Unchanged, write.Disposition));
        Assert.Equal(first.CommandWrites.Select(write => write.AssetId), second.CommandWrites.Select(write => write.AssetId));
        Assert.Equal(first.CommandWrites.Select(write => write.ContentHash), second.CommandWrites.Select(write => write.ContentHash));
        Assert.Equal(
            new[] {
                "assets/codebase/fixture.editor/DeterministicDemodiscAuthoringCommand.cs",
                "assets/codebase/fixture.editor/DeterministicDemodiscAuthoringCommand.cs.hmeta",
                "assets/codebase/fixture.editor/code.module.json",
                "assets/codebase/fixture.editor/code.module.json.hmeta",
                "assets/materials/generated.hasset",
                "assets/materials/generated.hasset.ps2.hasset",
                "assets/materials/generated.hasset.windows.hasset",
                "assets/models/generated-copy.hasset",
                "assets/models/generated.hasset",
                "cache/editor/asset-identity-index.json",
                "cache/editor/asset-identity-index.json.lock",
                "cache/editor/authoring-write.generation",
                "cache/editor/authoring-write.lock",
                "project.heproj"
            },
            first.Snapshot.Keys.OrderBy(path => path, StringComparer.Ordinal));
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
        Assert.Equal(savedReference.AssetId, first.Resolution.CanonicalReference.AssetId);
        Assert.Equal(savedReference.ContentHash, first.Resolution.CanonicalReference.ContentHash);
        Assert.Equal(
            new[] {
                EditorAssetRepairKind.MissingExternalMetadataCreation,
                EditorAssetRepairKind.SavedIdAdoption,
                EditorAssetRepairKind.HashHealing,
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
        EditorAssetRepairRecord adoption = first.RepairRecords[1];
        Assert.Equal("models/hash-after.obj", adoption.RelativePath);
        Assert.Equal(metadataCreation.CurrentAssetId, adoption.PreviousAssetId);
        Assert.Equal(savedReference.AssetId, adoption.CurrentAssetId);
        Assert.Equal(AssetReferenceResolutionTier.ContentHash, adoption.ResolutionTier);
        Assert.Equal("saved identity adopted by unique content hash", adoption.Evidence);
        Assert.Equal(string.Empty, adoption.OwningDocument);
        Assert.Equal("Adopted the saved identity for the uniquely matching authored source.", adoption.Diagnostic);
        for (int index = 2; index < first.RepairRecords.Count; index++) {
            EditorAssetRepairRecord repair = first.RepairRecords[index];
            Assert.Equal("models/hash-after.obj", repair.RelativePath);
            Assert.Equal(savedReference.AssetId, repair.PreviousAssetId);
            Assert.Equal(savedReference.AssetId, repair.CurrentAssetId);
            Assert.Equal(AssetReferenceResolutionTier.ContentHash, repair.ResolutionTier);
            Assert.Equal(
                "current-id=False; saved-path=False; saved-hash=True; recorded-owner=False; path='models/hash-after.obj'",
                repair.Evidence);
            Assert.Equal(string.Empty, repair.OwningDocument);
        }
        Assert.Equal(EditorAssetRepairKind.HashHealing, first.RepairRecords[2].Kind);
        Assert.Equal("Healed the saved content hash to the selected authored source.", first.RepairRecords[2].Diagnostic);
        Assert.Equal(EditorAssetRepairKind.CanonicalReferenceRefresh, first.RepairRecords[3].Kind);
        Assert.Equal("Refreshed the saved asset reference to its canonical identity, path, and hash.", first.RepairRecords[3].Diagnostic);
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
        Assert.Equal(
            new[] { EditorAssetRepairKind.DuplicateIdReassignment },
            first.RepairRecords.Select(repair => repair.Kind));
        EditorAssetRepairRecord reassignment = Assert.Single(first.RepairRecords);
        Assert.Equal("models/z-copy.hasset", reassignment.RelativePath);
        Assert.Equal(seeded.AssetId, reassignment.PreviousAssetId);
        Assert.Equal(first.CopyReference.AssetId, reassignment.CurrentAssetId);
        Assert.Null(reassignment.ResolutionTier);
        Assert.Equal("selected ordinal owner path='models/a-owner.hasset'", reassignment.Evidence);
        Assert.Equal(Path.Combine(ProjectRootPath, "assets", "models", "z-copy.hasset"), reassignment.OwningDocument);
        Assert.Equal("Reassigned copied identity to the non-owning asset.", reassignment.Diagnostic);
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
        IReadOnlyList<FixtureCommandWrite> commandWrites;
        IReadOnlyList<EditorAssetRepairRecord> repairRecords;
        using (IEditorProjectAuthoringSession authoring = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath))
        using (EditorGameScriptHotReloadService commandHost = CreateDiscoveredCommandHost()) {
            StringWriter commandOutput = new StringWriter();
            TextWriter previousConsole = Console.Out;
            lock (CommandObservationGate) {
                Console.SetOut(commandOutput);
                try {
                    execution = commandHost.BuildAndReload();
                    if (execution.Succeeded) {
                        EditorProjectCommandDescriptor command = Assert.Single(
                            commandHost.GetAvailableEditorCommands(),
                            descriptor => string.Equals(
                                descriptor.CommandId,
                                "fixture.generate-deterministic-assets",
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
                                commandHost,
                                commandHost.ScriptTypeResolver);
                    }
                } finally {
                    Console.SetOut(previousConsole);
                }
            }
            commandWrites = ParseFixtureCommandWrites(commandOutput.ToString());
            repairRecords = authoring.RepairReport.Snapshot;
        }

        long generationAfter = EditorProjectWriteGeneration.Read(ProjectRootPath);
        IReadOnlyList<EditorProjectWriteChange> changes = EditorProjectWriteGeneration
            .ReadAfter(ProjectRootPath, generationBefore);

        SceneAssetReference modelReference;
        SceneAssetReference materialReference;
        AssetReferenceResolution modelResolution;
        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath)) {
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
            commandWrites,
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

    EditorGameScriptHotReloadService CreateDiscoveredCommandHost() {
        EditorGameSolutionService solutionService = new EditorGameSolutionService(
            ProjectRootPath,
            "Deterministic fixture",
            new TestEditorIdeLauncher(),
            GeneratedOutputRootPath,
            GeneratedWorkspaceRootPath,
            EditorScriptCompilationMode.EditorFull);
        return new EditorGameScriptHotReloadService(
            solutionService,
            new EditorDotNetScriptBuildTool(),
            new EditorGameScriptAssemblyHost(ProjectRootPath));
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

    static IReadOnlyList<FixtureCommandWrite> ParseFixtureCommandWrites(string output) {
        List<FixtureCommandWrite> writes = new List<FixtureCommandWrite>();
        foreach (string line in (output ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)) {
            const string prefix = "FIXTURE_WRITE|";
            if (!line.StartsWith(prefix, StringComparison.Ordinal)) {
                continue;
            }

            string[] fields = line.Substring(prefix.Length).Split('|');
            Assert.Equal(4, fields.Length);
            Assert.True(Enum.TryParse(fields[3], out EditorAssetWriteDisposition disposition));
            writes.Add(new FixtureCommandWrite(fields[0], fields[1], fields[2], disposition));
        }

        Assert.Equal(3, writes.Count);
        return writes;
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

    sealed record AuthoredFileSnapshot(byte[] Bytes, byte[] Hash, DateTime LastWriteTimeUtc);

    sealed record GenerationPass(
        IReadOnlyDictionary<string, AuthoredFileSnapshot> Snapshot,
        EditorBuildExecutionResult Execution,
        long GenerationBefore,
        long GenerationAfter,
        IReadOnlyList<EditorProjectWriteChange> Changes,
        IReadOnlyList<FixtureCommandWrite> CommandWrites,
        SceneAssetReference ModelReference,
        SceneAssetReference MaterialReference,
        AssetReferenceResolution ModelResolution,
        IReadOnlyList<EditorAssetRepairRecord> RepairRecords);

    sealed record FixtureCommandWrite(
        string RelativePath,
        string AssetId,
        string ContentHash,
        EditorAssetWriteDisposition Disposition);

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
