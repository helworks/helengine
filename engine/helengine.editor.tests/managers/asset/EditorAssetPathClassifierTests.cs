using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies the asset path classifier probes .hasset files without raising exceptions for non-HELE payloads.
    /// </summary>
    public sealed class EditorAssetPathClassifierTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current test instance.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Creates one isolated temporary project root with an assets folder.
        /// </summary>
        public EditorAssetPathClassifierTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-path-classifier-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempRootPath, "assets"));
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
        /// Ensures a .hasset file that does not start with the HELE magic is classified without any exception being thrown and caught.
        /// </summary>
        [Fact]
        public void IsAuthoredAsset_WhenHassetLacksHeleHeader_DoesNotRaiseExceptions() {
            string hassetPath = Path.Combine(TempRootPath, "assets", "legacy.hasset");
            File.WriteAllText(hassetPath, "{ \"legacy\": true }");
            EditorAssetPathClassifier classifier = new EditorAssetPathClassifier(TempRootPath);
            int invalidOperationCount = 0;
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, args) => {
                if (args.Exception is InvalidOperationException) {
                    invalidOperationCount++;
                }
            };

            AppDomain.CurrentDomain.FirstChanceException += handler;
            bool authored;
            AssetEntryKind kind;
            try {
                authored = classifier.IsAuthoredAsset(hassetPath);
                kind = classifier.Classify(hassetPath);
            } finally {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.False(authored);
            Assert.Equal(AssetEntryKind.File, kind);
            Assert.Equal(0, invalidOperationCount);
        }

        /// <summary>
        /// Ensures identity metadata sidecars are rejected as authored assets without being read or raising exceptions.
        /// </summary>
        [Fact]
        public void IsAuthoredAsset_WhenPathIsMetadataSidecar_ReturnsFalseWithoutRaisingExceptions() {
            string metadataPath = Path.Combine(TempRootPath, "assets", "hero.png.hmeta");
            File.WriteAllText(metadataPath, "{ \"version\": 1, \"assetId\": \"abc\" }");
            EditorAssetPathClassifier classifier = new EditorAssetPathClassifier(TempRootPath);
            int firstChanceCount = 0;
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, args) => firstChanceCount++;

            AppDomain.CurrentDomain.FirstChanceException += handler;
            bool authored;
            try {
                authored = classifier.IsAuthoredAsset(metadataPath);
            } finally {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.False(authored);
            Assert.True(classifier.ShouldHide(metadataPath));
            Assert.Equal(0, firstChanceCount);
        }
    }
}
