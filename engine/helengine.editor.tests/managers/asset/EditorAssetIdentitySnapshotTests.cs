using System.Text.Json;
using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies the identity snapshot lets a later reconcile skip unchanged files while still seeing real changes.
    /// </summary>
    public sealed class EditorAssetIdentitySnapshotTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current test instance.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Creates one isolated project root with an assets folder.
        /// </summary>
        public EditorAssetIdentitySnapshotTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-identity-snapshot-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempRootPath, "assets", "Models"));
        }

        /// <summary>
        /// Deletes temporary test state.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a full reconcile writes a snapshot listing authored files with their ids and excluding sidecars.
        /// </summary>
        [Fact]
        public void Initialize_WhenNoSnapshotExists_WritesSnapshotAfterFullReconcile() {
            CreateAsset("Models/A.fbx");
            using EditorAssetIdentityIndex index = new EditorAssetIdentityIndex(TempRootPath);

            index.Initialize();

            string snapshotPath = Path.Combine(TempRootPath, "cache", "editor", EditorAssetIdentitySnapshotStore.FileName);
            Assert.True(File.Exists(snapshotPath));
            Assert.Equal(0, index.LastReconcileReusedSnapshotFileCount);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
            JsonElement files = document.RootElement.GetProperty("files");
            JsonElement file = Assert.Single(files.EnumerateArray());
            Assert.Equal("Models/A.fbx", file.GetProperty("relativePath").GetString());
            Assert.True(file.GetProperty("authored").GetBoolean());
            Assert.Equal(index.FindByPath("Models/A.fbx").AssetId, file.GetProperty("assetId").GetString());
        }

        /// <summary>
        /// Ensures unchanged stamps make the next reconcile trust the snapshot instead of reading the sidecar.
        /// </summary>
        [Fact]
        public void Initialize_WhenStampsAreUnchanged_ReusesSnapshotWithoutReadingSidecar() {
            string assetPath = CreateAsset("Models/A.fbx");
            string originalId;
            using (EditorAssetIdentityIndex first = new EditorAssetIdentityIndex(TempRootPath)) {
                first.Initialize();
                originalId = first.FindByPath("Models/A.fbx").AssetId;
            }

            // Rewrite the sidecar with a different id but identical length and last-write time. A reconcile
            // that trusts the snapshot cannot notice this; one that reads the sidecar would.
            string sidecarPath = assetPath + ".hmeta";
            DateTime originalWriteTime = File.GetLastWriteTimeUtc(sidecarPath);
            string tamperedId = new string('f', 32);
            string sidecarText = File.ReadAllText(sidecarPath);
            Assert.Contains(originalId, sidecarText);
            File.WriteAllText(sidecarPath, sidecarText.Replace(originalId, tamperedId));
            File.SetLastWriteTimeUtc(sidecarPath, originalWriteTime);

            using EditorAssetIdentityIndex second = new EditorAssetIdentityIndex(TempRootPath);
            second.Initialize();

            Assert.Equal(1, second.LastReconcileReusedSnapshotFileCount);
            Assert.Equal(originalId, second.FindByPath("Models/A.fbx").AssetId);
        }

        /// <summary>
        /// Ensures a changed sidecar stamp makes the reconcile read the new identity.
        /// </summary>
        [Fact]
        public void Initialize_WhenSidecarChanges_ReadsNewIdentity() {
            string assetPath = CreateAsset("Models/A.fbx");
            using (EditorAssetIdentityIndex first = new EditorAssetIdentityIndex(TempRootPath)) {
                first.Initialize();
            }

            string newId = new string('a', 32);
            new AssetIdentityMetadataService(TempRootPath).Save(assetPath, new AssetIdentityMetadataDocument {
                AssetId = newId,
                FormerAssetIds = new List<string>()
            });
            File.SetLastWriteTimeUtc(assetPath + ".hmeta", DateTime.UtcNow.AddSeconds(5));

            using EditorAssetIdentityIndex second = new EditorAssetIdentityIndex(TempRootPath);
            second.Initialize();

            Assert.Equal(0, second.LastReconcileReusedSnapshotFileCount);
            Assert.Equal(newId, second.FindByPath("Models/A.fbx").AssetId);
        }

        /// <summary>
        /// Ensures a corrupt snapshot is ignored, a full reconcile runs, and a valid snapshot replaces it.
        /// </summary>
        [Fact]
        public void Initialize_WhenSnapshotIsCorrupt_RunsFullReconcileAndRewritesSnapshot() {
            CreateAsset("Models/A.fbx");
            string originalId;
            using (EditorAssetIdentityIndex first = new EditorAssetIdentityIndex(TempRootPath)) {
                first.Initialize();
                originalId = first.FindByPath("Models/A.fbx").AssetId;
            }

            string snapshotPath = Path.Combine(TempRootPath, "cache", "editor", EditorAssetIdentitySnapshotStore.FileName);
            File.WriteAllText(snapshotPath, "{ not json");

            using EditorAssetIdentityIndex second = new EditorAssetIdentityIndex(TempRootPath);
            second.Initialize();

            Assert.Equal(0, second.LastReconcileReusedSnapshotFileCount);
            Assert.Equal(originalId, second.FindByPath("Models/A.fbx").AssetId);
            Assert.NotNull(new EditorAssetIdentitySnapshotStore(TempRootPath).Load());
        }

        /// <summary>
        /// Ensures added and removed files are reflected while unchanged files are still reused.
        /// </summary>
        [Fact]
        public void Initialize_WhenFilesAreAddedAndRemoved_ReflectsChangesAndReusesTheRest() {
            string keptPath = CreateAsset("Models/Kept.fbx");
            string removedPath = CreateAsset("Models/Removed.fbx");
            string keptId;
            using (EditorAssetIdentityIndex first = new EditorAssetIdentityIndex(TempRootPath)) {
                first.Initialize();
                keptId = first.FindByPath("Models/Kept.fbx").AssetId;
            }

            File.Delete(removedPath);
            File.Delete(removedPath + ".hmeta");
            CreateAsset("Models/Added.fbx");

            using EditorAssetIdentityIndex second = new EditorAssetIdentityIndex(TempRootPath);
            second.Initialize();

            Assert.Equal(1, second.LastReconcileReusedSnapshotFileCount);
            Assert.Equal(keptId, second.FindByPath("Models/Kept.fbx").AssetId);
            Assert.Null(second.FindByPath("Models/Removed.fbx"));
            Assert.NotNull(second.FindByPath("Models/Added.fbx"));
            Assert.True(File.Exists(Path.Combine(TempRootPath, "assets", "Models", "Added.fbx.hmeta")));
        }

        /// <summary>
        /// Creates one source file below the isolated assets root.
        /// </summary>
        /// <param name="relativePath">Path relative to assets.</param>
        /// <returns>Absolute source path.</returns>
        string CreateAsset(string relativePath) {
            string assetPath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllBytes(assetPath, new byte[] { 1, 2, 3 });
            return assetPath;
        }
    }
}
