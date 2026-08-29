using System.Security.Cryptography;
using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Exercises the public project-authoring boundary with a small, demodisc-shaped
/// generator.  The fixture intentionally creates its project in a temporary root
/// so no checkout content or ambient project path participates in the proof.
/// </summary>
public sealed class DemoDiscAuthoringDeterminismTests : IDisposable {
    readonly string ProjectRootPath;
    readonly Core CoreValue;
    readonly TestGeneratedAssetGraph GeneratedAssetGraph;

    /// <summary>
    /// Creates one current-format generated-project fixture in an isolated root.
    /// </summary>
    public DemoDiscAuthoringDeterminismTests() {
        ProjectRootPath = Path.Combine(
            Path.GetTempPath(),
            "helengine-demodisc-authoring-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
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
    }

    /// <summary>
    /// Ensures two complete public-session generation passes publish the same
    /// files, bytes, timestamps, references, and no-op write dispositions.
    /// </summary>
    [Fact]
    public void PublicGeneration_TwoIdenticalPassesHaveExactProjectSnapshotAndNoOpWrites() {
        GenerationPass first = RunGenerationPass();
        GenerationPass second = RunGenerationPass();

        Assert.Contains(first.Results, result => result.Disposition == EditorAssetWriteDisposition.Created);
        Assert.NotEmpty(first.Snapshot);
        Assert.Contains(first.Snapshot.Keys, path => path.Equals("assets/models/generated.hasset", StringComparison.Ordinal));
        Assert.Contains(first.Snapshot.Keys, path => path.Equals("assets/materials/generated.hasset", StringComparison.Ordinal));
        Assert.All(second.Results, result => Assert.Equal(EditorAssetWriteDisposition.Unchanged, result.Disposition));
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
        AssertRepair(first.RepairRecords, EditorAssetRepairKind.PathHealing, "models/after.obj", AssetReferenceResolutionTier.AssetId);
        AssertRepair(first.RepairRecords, EditorAssetRepairKind.CanonicalReferenceRefresh, "models/after.obj", AssetReferenceResolutionTier.AssetId);
        Assert.All(first.RepairRecords, AssertCompleteRepairRecord);
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
        Assert.NotEqual(string.Empty, first.Resolution.CanonicalReference.AssetId);
        Assert.True(File.Exists(sourcePath + ".hmeta"));
        Assert.Contains(
            first.RepairRecords,
            repair => repair.Kind == EditorAssetRepairKind.MissingExternalMetadataCreation
                && repair.RelativePath == "models/metadata.obj");
        Assert.Contains(
            first.RepairRecords,
            repair => repair.Kind == EditorAssetRepairKind.SavedIdAdoption
                && repair.RelativePath == "models/metadata.obj");
        Assert.All(first.RepairRecords, AssertCompleteRepairRecord);
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

    GenerationPass RunGenerationPass() {
        List<EditorAssetWriteResult> results = new List<EditorAssetWriteResult>();
        SceneAssetReference modelReference;
        SceneAssetReference materialReference;
        AssetReferenceResolution modelResolution;
        IReadOnlyList<EditorAssetRepairRecord> repairRecords;
        using (IEditorProjectAuthoringSession session = GeneratedAssetGraph.CreateAuthoringSession(ProjectRootPath)) {
            using (EditorAuthoringTransaction transaction = session.BeginTransaction()) {
                results.Add(transaction.WriteAsset("models/generated.hasset", CreateModel("Generated", 1f)));
                results.Add(transaction.WriteAsset("models/generated-copy.hasset", CreateModel("GeneratedCopy", 2f)));
                results.Add(transaction.WriteMaterial("materials/generated.hasset", CreateMaterial("GeneratedMaterial")));
                modelReference = transaction.CreateReference("models/generated.hasset", AssetEntryKind.Model);
                materialReference = transaction.CreateReference("materials/generated.hasset", AssetEntryKind.Material);
                transaction.Commit();
            }

            modelResolution = session.ResolveReference(modelReference, AssetEntryKind.Model);
            repairRecords = session.RepairReport.Snapshot;
        }

        return new GenerationPass(
            CaptureProjectSnapshot(ProjectRootPath),
            results,
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

    sealed record GenerationPass(
        IReadOnlyDictionary<string, AuthoredFileSnapshot> Snapshot,
        IReadOnlyList<EditorAssetWriteResult> Results,
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
}
