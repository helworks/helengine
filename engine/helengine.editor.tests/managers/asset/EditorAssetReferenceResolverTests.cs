using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies ordered editor asset reference recovery and canonicalization.
/// </summary>
public sealed class EditorAssetReferenceResolverTests : IDisposable {
    /// <summary>
    /// Temporary project root used by resolver tests.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes one isolated resolver project.
    /// </summary>
    public EditorAssetReferenceResolverTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-resolver-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(TempRootPath, "assets", "Models"));
    }

    /// <summary>
    /// Removes the isolated resolver project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures a current UUID wins even when the saved path and hash are stale.
    /// </summary>
    [Fact]
    public void Resolve_CurrentAssetIdWinsOverStalePathAndHash() {
        string assetPath = CreateAsset("Models/Current.fbx", new byte[] { 1, 2, 3 });
        AssetIdentityMetadataService metadata = new AssetIdentityMetadataService();
        metadata.Save(assetPath, new AssetIdentityMetadataDocument {
            AssetId = "00112233445566778899aabbccddeeff",
            FormerAssetIds = new List<string>()
        });
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Models/Missing.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Model);

        Assert.Equal(AssetReferenceResolutionTier.AssetId, result.Tier);
        Assert.Equal(assetPath, result.FullPath);
        Assert.Equal("Models/Current.fbx", result.CanonicalReference.RelativePath);
        Assert.Equal("00112233445566778899aabbccddeeff", result.CanonicalReference.AssetId);
        Assert.True(result.ReferenceChanged);
    }

    /// <summary>
    /// Ensures path, hash, and complete canonical reference repairs are all reported once.
    /// </summary>
    [Fact]
    public void Resolve_WhenPathAndHashAreStale_ReportsEachCanonicalRepair() {
        string assetPath = CreateAsset("Models/Reported.fbx", new byte[] { 1, 2, 3 });
        AssetIdentityMetadataService metadata = new AssetIdentityMetadataService();
        const string assetId = "00112233445566778899aabbccddeeff";
        metadata.Save(assetPath, new AssetIdentityMetadataDocument { AssetId = assetId });
        EditorAssetRepairReport report = new EditorAssetRepairReport();
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, repairReport: report);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            assetId,
            "Models/Missing.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Model);

        Assert.True(result.ReferenceChanged);
        Assert.Contains(report.Records, item => item.Kind == EditorAssetRepairKind.PathHealing);
        Assert.Contains(report.Records, item => item.Kind == EditorAssetRepairKind.HashHealing);
        Assert.Contains(report.Records, item => item.Kind == EditorAssetRepairKind.CanonicalReferenceRefresh);
        Assert.Equal(result.CandidateEvidence.ToEvidenceString(), report.Records[^1].Evidence);
    }

    /// <summary>
    /// Ensures resolving an already canonical reference does not append a no-op repair.
    /// </summary>
    [Fact]
    public void Resolve_WhenReferenceIsCanonical_DoesNotReportRepair() {
        string assetPath = CreateAsset("Models/Canonical.fbx", new byte[] { 9, 8, 7 });
        EditorAssetRepairReport setupReport = new EditorAssetRepairReport();
        using EditorAssetReferenceResolver setupResolver = new EditorAssetReferenceResolver(TempRootPath, repairReport: setupReport);
        SceneAssetReference reference = setupResolver.CreateFileReference(assetPath, AssetEntryKind.Model);
        EditorAssetRepairReport report = new EditorAssetRepairReport();
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, repairReport: report);

        resolver.Resolve(reference, AssetEntryKind.Model);

        Assert.Empty(report.Records);
    }

    /// <summary>
    /// Ensures a missing sidecar adopts an unclaimed saved UUID during path recovery.
    /// </summary>
    [Fact]
    public void Resolve_ExistingPathWithoutMetadata_AdoptsUnclaimedSavedAssetId() {
        string assetPath = CreateAsset("Models/Adopt.fbx", new byte[] { 4, 5, 6 });
        EditorAssetRepairReport report = new EditorAssetRepairReport();
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, repairReport: report);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Models/Adopt.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Model);

        Assert.Equal(AssetReferenceResolutionTier.Path, result.Tier);
        Assert.Equal("00112233445566778899aabbccddeeff", result.CanonicalReference.AssetId);
        Assert.True(result.MetadataChanged);
        Assert.True(File.Exists(assetPath + ".hmeta"));
        EditorAssetRepairRecord adoption = Assert.Single(report.Records, repair => repair.Kind == EditorAssetRepairKind.SavedIdAdoption);
        Assert.NotEqual(string.Empty, adoption.PreviousAssetId);
        Assert.Equal(result.CanonicalReference.AssetId, adoption.CurrentAssetId);
    }

    /// <summary>
    /// Ensures a saved ID that is already a former alias is resolved through the ID tier instead of being adopted by a path.
    /// </summary>
    [Fact]
    public void Resolve_WhenSavedIdMatchesFormerAlias_DoesNotAdoptItAtAnotherPath() {
        string ownerPath = CreateAsset("Models/Owner.fbx", new byte[] { 1, 2, 3 });
        string targetPath = CreateAsset("Models/Target.fbx", new byte[] { 4, 5, 6 });
        AssetIdentityMetadataService metadata = new AssetIdentityMetadataService();
        const string currentId = "00112233445566778899aabbccddeeff";
        const string formerId = "ffeeddccbbaa99887766554433221100";
        metadata.Save(ownerPath, new AssetIdentityMetadataDocument {
            AssetId = currentId,
            FormerAssetIds = new List<string> { formerId }
        });
        EditorAssetRepairReport report = new EditorAssetRepairReport();
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, repairReport: report);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            formerId,
            "Models/Target.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Model);

        Assert.Equal(ownerPath, result.FullPath);
        Assert.Equal(currentId, result.CanonicalReference.AssetId);
        Assert.DoesNotContain(report.Records, repair => repair.Kind == EditorAssetRepairKind.SavedIdAdoption);
        Assert.NotEqual(formerId, new AssetIdentityMetadataService().Load(targetPath).AssetId);
    }

    /// <summary>
    /// Ensures one missing-metadata path can adopt only once within a resolver lifetime.
    /// </summary>
    [Fact]
    public void Resolve_WhenTwoReferencesCompeteForMissingPath_AdoptsOnlyTheFirstSavedId() {
        string targetPath = CreateAsset("Models/Competing.fbx", new byte[] { 4, 5, 6 });
        EditorAssetRepairReport report = new EditorAssetRepairReport();
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, repairReport: report);
        const string firstId = "00112233445566778899aabbccddeeff";
        const string secondId = "ffeeddccbbaa99887766554433221100";
        SceneAssetReference first = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(firstId, "Models/Competing.fbx", "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
        SceneAssetReference second = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(secondId, "Models/Competing.fbx", "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution firstResult = resolver.Resolve(first, AssetEntryKind.Model);
        AssetReferenceResolution secondResult = resolver.Resolve(second, AssetEntryKind.Model);

        Assert.Equal(firstId, firstResult.CanonicalReference.AssetId);
        Assert.Equal(firstId, secondResult.CanonicalReference.AssetId);
        Assert.Equal(firstId, new AssetIdentityMetadataService().Load(targetPath).AssetId);
        Assert.Single(report.Records, repair => repair.Kind == EditorAssetRepairKind.SavedIdAdoption);
    }

    /// <summary>
    /// Ensures reference repairs identify the active binary document when one is available.
    /// </summary>
    [Fact]
    public void Resolve_WhenBinaryReadContextHasDocument_RecordsOwningDocument() {
        string assetPath = CreateAsset("Models/Context.fbx", new byte[] { 1, 2, 3 });
        AssetIdentityMetadataService metadata = new AssetIdentityMetadataService();
        const string assetId = "00112233445566778899aabbccddeeff";
        metadata.Save(assetPath, new AssetIdentityMetadataDocument { AssetId = assetId });
        EditorAssetRepairReport report = new EditorAssetRepairReport();
        string previousPath = EngineBinaryReadContext.CurrentAssetPath;
        EngineBinaryReadContext.CurrentAssetPath = Path.Combine(TempRootPath, "assets", "Scenes", "Owning.helen");
        try {
            using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, repairReport: report);
            SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                assetId,
                "Models/Missing.fbx",
                "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

            resolver.Resolve(reference, AssetEntryKind.Model);

            Assert.Contains(report.Records, repair =>
                repair.Kind == EditorAssetRepairKind.PathHealing &&
                string.Equals(repair.OwningDocument, EngineBinaryReadContext.CurrentAssetPath, StringComparison.Ordinal));
        } finally {
            EngineBinaryReadContext.CurrentAssetPath = previousPath;
        }
    }

    /// <summary>
    /// Ensures a missing sidecar at the saved path cannot override a UUID already owned by another asset.
    /// </summary>
    [Fact]
    public void Resolve_WhenSavedPathMetadataIsMissing_ExistingAssetIdOwnerStillWins() {
        string idOwnerPath = CreateAsset("Models/A.fbx", new byte[] { 1, 2, 3 });
        string savedPath = CreateAsset("Models/B.fbx", new byte[] { 4, 5, 6 });
        AssetIdentityMetadataService metadata = new AssetIdentityMetadataService();
        metadata.Save(idOwnerPath, new AssetIdentityMetadataDocument {
            AssetId = "00112233445566778899aabbccddeeff",
            FormerAssetIds = new List<string>()
        });
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Models/B.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Model);

        Assert.Equal(AssetReferenceResolutionTier.AssetId, result.Tier);
        Assert.Equal(idOwnerPath, result.FullPath);
        Assert.NotEqual(savedPath, result.FullPath);
    }

    /// <summary>
    /// Ensures hash fallback selects the ordinally smallest compatible candidate.
    /// </summary>
    [Fact]
    public void Resolve_WhenOnlyHashMatches_SelectsOrdinalCompatiblePath() {
        string firstPath = CreateAsset("Models/A.fbx", new byte[] { 9, 9, 9 });
        string secondPath = CreateAsset("Models/B.fbx", new byte[] { 9, 9, 9 });
        EditorAssetReferenceResolver setupResolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference firstReference = setupResolver.CreateFileReference(firstPath, AssetEntryKind.Model);
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference unresolvedReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "ffeeddccbbaa99887766554433221100",
            "Models/Missing.fbx",
            firstReference.ContentHash);

        AssetReferenceResolution result = resolver.Resolve(unresolvedReference, AssetEntryKind.Model);

        Assert.Equal(AssetReferenceResolutionTier.ContentHash, result.Tier);
        Assert.Equal("Models/A.fbx", result.CanonicalReference.RelativePath);
        Assert.NotEqual(firstPath, secondPath);
    }

    /// <summary>
    /// Ensures a current identity beats a copied former alias even when the former alias matches the saved path.
    /// </summary>
    [Fact]
    public void Resolve_WhenCurrentAndFormerIdentityCandidatesCompete_PrefersCurrentIdentity() {
        string firstPath = CreateAsset("Models/A.fbx", new byte[] { 1, 2, 3 });
        string secondPath = CreateAsset("Models/B.fbx", new byte[] { 4, 5, 6 });
        AssetIdentityMetadataService metadata = new AssetIdentityMetadataService();
        const string copiedAssetId = "00112233445566778899aabbccddeeff";
        metadata.Save(firstPath, new AssetIdentityMetadataDocument { AssetId = copiedAssetId });
        metadata.Save(secondPath, new AssetIdentityMetadataDocument { AssetId = copiedAssetId });

        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            copiedAssetId,
            "Models/B.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Model);

        Assert.Equal(AssetReferenceResolutionTier.AssetId, result.Tier);
        Assert.Equal("Models/A.fbx", result.CanonicalReference.RelativePath);
        Assert.True(result.CandidateEvidence.IsCurrentId);
    }

    /// <summary>
    /// Ensures a matching saved content hash selects a compatible native candidate.
    /// </summary>
    [Fact]
    public void Resolve_WhenHashMatchesNativeCandidate_PrefersSavedHash() {
        CreateNativeAnimation("Animations/A.hanim", "aabbccddeeff00112233445566778899", 1f);
        string secondPath = CreateNativeAnimation("Animations/B.hanim", "aabbccddeeff00112233445566778899", 2f);
        using EditorAssetReferenceResolver setupResolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference secondReference = setupResolver.CreateFileReference(secondPath, AssetEntryKind.File);
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference unresolvedReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "ffeeddccbbaa99887766554433221100",
            "Animations/Missing.hanim",
            secondReference.ContentHash);

        AssetReferenceResolution result = resolver.Resolve(unresolvedReference, AssetEntryKind.File);

        Assert.Equal(AssetReferenceResolutionTier.ContentHash, result.Tier);
        Assert.Equal("Animations/B.hanim", result.CanonicalReference.RelativePath);
        Assert.True(result.CandidateEvidence.MatchesSavedHash);
    }

    /// <summary>
    /// Ensures a recorded owner beats lexical path order when all stronger identity evidence ties.
    /// </summary>
    [Fact]
    public void Resolve_WhenRecordedOwnerAndLexicalPathCompete_PrefersRecordedOwner() {
        string recordedOwnerPath = CreateNativeAnimation("Animations/Z.hanim", "aabbccddeeff00112233445566778899", 1f);
        using EditorAssetIdentityIndex index = new EditorAssetIdentityIndex(TempRootPath);
        index.Initialize();
        CreateNativeAnimation("Animations/A.hanim", "aabbccddeeff00112233445566778899", 1f);
        index.ReconcileExternalChanges();
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, index);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "aabbccddeeff00112233445566778899",
            "Animations/Missing.hanim",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.File);

        Assert.Equal(recordedOwnerPath, result.FullPath);
        Assert.True(result.CandidateEvidence.IsRecordedOwner);
    }

    /// <summary>
    /// Ensures the final duplicate tie-break is normalized relative path ordinal order.
    /// </summary>
    [Fact]
    public void Resolve_WhenDuplicateCandidatesHaveEqualEvidence_UsesOrdinalPath() {
        CreateNativeAnimation("Animations/A.hanim", "aabbccddeeff00112233445566778899", 1f);
        string secondPath = CreateNativeAnimation("Animations/B.hanim", "aabbccddeeff00112233445566778899", 1f);
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "aabbccddeeff00112233445566778899",
            "Animations/Missing.hanim",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.File);

        Assert.NotEqual(secondPath, result.FullPath);
        Assert.Equal("Animations/A.hanim", result.CanonicalReference.RelativePath);
        Assert.Equal("Animations/A.hanim", result.CandidateEvidence.RelativePath);
    }

    /// <summary>
    /// Ensures unresolved diagnostics contain all supplied identity fields and attempted tiers.
    /// </summary>
    [Fact]
    public void Resolve_WhenNoCandidateExists_ThrowsCompleteDiagnostic() {
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Models/Missing.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(reference, AssetEntryKind.Model));

        Assert.Contains("Model", exception.Message, StringComparison.Ordinal);
        Assert.Contains(reference.AssetId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(reference.RelativePath, exception.Message, StringComparison.Ordinal);
        Assert.Contains(reference.ContentHash, exception.Message, StringComparison.Ordinal);
        Assert.Contains("AssetId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Path", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ContentHash", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures a native material reference recovers after its file moves by the embedded authored identity.
    /// </summary>
    [Fact]
    public void Resolve_NativeHelmatRecoversMovedMaterialByEmbeddedAssetId() {
        string sourcePath = CreateNativeHelmat("Materials/Source.helmat", "00112233445566778899aabbccddeeff");
        EditorAssetReferenceResolver setupResolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = setupResolver.CreateFileReference(sourcePath, AssetEntryKind.Material);
        string destinationPath = Path.Combine(TempRootPath, "assets", "Materials", "Moved.helmat");
        File.Move(sourcePath, destinationPath);
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Material);

        Assert.Equal(AssetReferenceResolutionTier.AssetId, result.Tier);
        Assert.Equal(destinationPath, result.FullPath);
        Assert.Equal("00112233445566778899aabbccddeeff", result.CanonicalReference.AssetId);
        Assert.Equal("Materials/Moved.helmat", result.CanonicalReference.RelativePath);
        Assert.False(File.Exists(destinationPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures a replacement native material with a different identity is recovered by its identity-excluded content hash.
    /// </summary>
    [Fact]
    public void Resolve_NativeHelmatReplacementRecoversByContentHash() {
        string sourcePath = CreateNativeHelmat("Materials/Source.helmat", "3344556677889900aabbccddeeff1122");
        EditorAssetReferenceResolver setupResolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = setupResolver.CreateFileReference(sourcePath, AssetEntryKind.Material);
        File.Delete(sourcePath);
        string replacementPath = CreateNativeHelmat("Materials/Replacement.helmat", "44556677889900aabbccddeeff112233");
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Material);

        Assert.Equal(AssetReferenceResolutionTier.ContentHash, result.Tier);
        Assert.Equal(replacementPath, result.FullPath);
        Assert.Equal("44556677889900aabbccddeeff112233", result.CanonicalReference.AssetId);
        Assert.Equal("Materials/Replacement.helmat", result.CanonicalReference.RelativePath);
        Assert.False(File.Exists(replacementPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures resolver operations reuse one initialized index without implicit full rescans.
    /// </summary>
    [Fact]
    public void Resolve_MultipleReferences_ReusesInitializedIndexWithoutRescanning() {
        string firstPath = CreateAsset("Models/A.fbx", new byte[] { 1, 2, 3 });
        string secondPath = CreateAsset("Models/B.fbx", new byte[] { 4, 5, 6 });
        CountingAssetFileCatalog catalog = new CountingAssetFileCatalog();
        EditorAssetIdentityIndex index = new EditorAssetIdentityIndex(TempRootPath, null, null, null, catalog);
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, index);

        resolver.CreateFileReference(firstPath, AssetEntryKind.Model);
        resolver.CreateFileReference(secondPath, AssetEntryKind.Model);

        Assert.Equal(1, catalog.EnumerationCount);
    }

    /// <summary>
    /// Ensures a resolver flushes a cache it creates and repeated disposal is harmless.
    /// </summary>
    [Fact]
    public void Dispose_WhenResolverOwnsHashCache_FlushesExactlyOnce() {
        string assetPath = CreateAsset("Models/Owned.fbx", new byte[] { 7, 8, 9 });
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);

        resolver.CreateFileReference(assetPath, AssetEntryKind.Model);
        string cachePath = Path.Combine(TempRootPath, "cache", "editor", "asset-identity-index.json");
        Assert.False(File.Exists(cachePath));

        resolver.Dispose();
        resolver.Dispose();

        Assert.True(File.Exists(cachePath));
        string persisted = File.ReadAllText(cachePath);
        resolver = null;
        Assert.Contains("Models/Owned.fbx", persisted, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures a resolver borrowing a caller cache does not flush or release it.
    /// </summary>
    [Fact]
    public void Dispose_WhenResolverBorrowsHashCache_LeavesCacheLifetimeWithCaller() {
        string assetPath = CreateAsset("Models/Borrowed.fbx", new byte[] { 2, 4, 6 });
        string cachePath = Path.Combine(TempRootPath, "cache", "editor", "asset-identity-index.json");
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath);
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, hashCache: cache);

        resolver.CreateFileReference(assetPath, AssetEntryKind.Model);
        resolver.Dispose();

        Assert.False(File.Exists(cachePath));
        cache.Dispose();
        Assert.True(File.Exists(cachePath));
        cache.Dispose();
    }

    /// <summary>
    /// Ensures a resolver rejects external files before creating metadata through reference creation.
    /// </summary>
    [Fact]
    public void CreateFileReference_WhenPathIsOutsideAssetsRoot_RejectsWithoutCreatingMetadata() {
        string outsidePath = Path.Combine(TempRootPath, "outside-reference.fbx");
        File.WriteAllBytes(outsidePath, new byte[] { 3, 2, 1 });
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);

        Assert.Throws<InvalidOperationException>(() => resolver.CreateFileReference(outsidePath, AssetEntryKind.Model));

        Assert.False(File.Exists(outsidePath + ".hmeta"));
    }

    /// <summary>
    /// Ensures a link beneath assets cannot redirect reference creation outside the project.
    /// </summary>
    [Fact]
    public void CreateFileReference_WhenPathTraversesReparsePoint_RejectsWithoutCreatingMetadata() {
        string outsideRoot = Path.Combine(Path.GetTempPath(), "helengine-resolver-outside-" + Guid.NewGuid().ToString("N"));
        string linkPath = Path.Combine(TempRootPath, "assets", "Linked");
        Directory.CreateDirectory(outsideRoot);
        string outsideAsset = Path.Combine(outsideRoot, "Escaped.fbx");
        File.WriteAllBytes(outsideAsset, new byte[] { 1, 2, 3 });
        try {
            try {
                Directory.CreateSymbolicLink(linkPath, outsideRoot);
            } catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is PlatformNotSupportedException) {
                return;
            }

            using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
            Assert.Throws<InvalidOperationException>(() => resolver.CreateFileReference(
                Path.Combine(linkPath, "Escaped.fbx"), AssetEntryKind.Model));
            Assert.False(File.Exists(outsideAsset + ".hmeta"));
        } finally {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
            if (Directory.Exists(outsideRoot)) {
                Directory.Delete(outsideRoot, true);
            }
        }
    }

    /// <summary>
    /// Ensures initial indexing rejects a linked authored path before consuming or creating external metadata.
    /// </summary>
    [Fact]
    public void Initialize_WhenCatalogContainsReparsePath_RejectsWithoutExternalMetadataMutation() {
        string outsideRoot = Path.Combine(Path.GetTempPath(), "helengine-index-outside-" + Guid.NewGuid().ToString("N"));
        string linkPath = Path.Combine(TempRootPath, "assets", "Linked");
        Directory.CreateDirectory(outsideRoot);
        string outsideAsset = Path.Combine(outsideRoot, "Escaped.fbx");
        File.WriteAllBytes(outsideAsset, new byte[] { 1, 2, 3 });
        try {
            try {
                Directory.CreateSymbolicLink(linkPath, outsideRoot);
            } catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is PlatformNotSupportedException) {
                return;
            }

            Assert.Throws<InvalidOperationException>(() => new EditorAssetIdentityIndex(TempRootPath).Initialize());
            Assert.False(File.Exists(outsideAsset + ".hmeta"));
        } finally {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
            if (Directory.Exists(outsideRoot)) {
                Directory.Delete(outsideRoot, true);
            }
        }
    }

    /// <summary>
    /// Ensures a saved reference path cannot escape assets during recovery.
    /// </summary>
    [Fact]
    public void Resolve_WhenSavedPathEscapesAssetsRoot_RejectsBeforeMetadataAccess() {
        string outsidePath = Path.Combine(TempRootPath, "outside.fbx");
        File.WriteAllBytes(outsidePath, new byte[] { 1, 2, 3 });
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "../outside.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(reference, AssetEntryKind.Model));
        Assert.False(File.Exists(outsidePath + ".hmeta"));
    }

    /// <summary>
    /// Ensures a disposed resolver rejects every resolution-scope and reference operation.
    /// </summary>
    [Fact]
    public void Dispose_WhenRepeated_RejectsResolutionOperationsAfterRelease() {
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        resolver.Dispose();
        resolver.Dispose();

        Assert.Throws<ObjectDisposedException>(() => resolver.Resolve(null, AssetEntryKind.Model));
        Assert.Throws<ObjectDisposedException>(() => resolver.BeginResolutionScope());
        Assert.Throws<ObjectDisposedException>(() => resolver.EndResolutionScope());
        Assert.Throws<ObjectDisposedException>(() => resolver.CreateFileReference(Path.Combine(TempRootPath, "assets", "Models", "Missing.fbx"), AssetEntryKind.Model));
    }

    /// <summary>
    /// Creates one source file below the isolated assets root.
    /// </summary>
    /// <param name="relativePath">Path relative to assets.</param>
    /// <param name="bytes">Source bytes.</param>
    /// <returns>Absolute source path.</returns>
    string CreateAsset(string relativePath, byte[] bytes) {
        string assetPath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        File.WriteAllBytes(assetPath, bytes);
        return assetPath;
    }

    /// <summary>
    /// Writes one current native material common-settings document with embedded authored identity.
    /// </summary>
    string CreateNativeHelmat(string relativePath, string assetId) {
        string assetPath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument {
            AuthoringAssetId = assetId
        };
        document.Importer.ImporterId = "helengine.material";
        document.Importer.AssetId = "Materials/Native";
        using FileStream stream = File.Create(assetPath);
        MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(stream, document);
        return assetPath;
    }

    /// <summary>
    /// Writes one current native animation fixture with an embedded identity.
    /// </summary>
    string CreateNativeAnimation(string relativePath, string assetId, float duration) {
        string assetPath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        using FileStream stream = File.Create(assetPath);
        AssetSerializer.Serialize(stream, new AnimationClipAsset {
            Id = relativePath,
            AuthoringAssetId = assetId,
            FormerAuthoringAssetIds = Array.Empty<string>(),
            Duration = duration,
            PositionTracks = Array.Empty<PositionKeyframeTrackAsset>(),
            PositionOffsetTracks = Array.Empty<PositionOffsetKeyframeTrackAsset>(),
            ScaleTracks = Array.Empty<ScaleKeyframeTrackAsset>(),
            RotationTracks = Array.Empty<RotationKeyframeTrackAsset>(),
            PlatformOverrides = Array.Empty<AnimationClipPlatformOverrideAsset>()
        });
        return assetPath;
    }

    /// <summary>
    /// Counts authored-file enumerations while delegating enumeration to the real filesystem.
    /// </summary>
    sealed class CountingAssetFileCatalog : IEditorAssetFileCatalog {
        /// <summary>
        /// Gets the number of full authored-file enumerations requested by the resolver index.
        /// </summary>
        public int EnumerationCount { get; private set; }

        /// <summary>
        /// Enumerates all files beneath the requested assets root.
        /// </summary>
        /// <param name="assetsRootPath">Absolute assets root path.</param>
        /// <returns>Filesystem paths beneath the assets root.</returns>
        public IEnumerable<string> EnumerateFiles(string assetsRootPath) {
            EnumerationCount++;
            return Directory.EnumerateFiles(assetsRootPath, "*", SearchOption.AllDirectories);
        }
    }
}
