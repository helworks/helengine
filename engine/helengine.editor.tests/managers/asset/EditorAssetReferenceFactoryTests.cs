using helengine.editor;

namespace helengine.editor.tests {
    public sealed class EditorAssetReferenceFactoryTests {
        [Fact]
        public void CreateFileReference_WhenSourceExists_EmbedsAssetIdAndContentHashAndCreatesSidecar() {
            string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-reference-factory-tests", Guid.NewGuid().ToString("N"));
            string sourcePath = Path.Combine(projectRootPath, "assets", "images", "checker.png");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });

            try {
                SceneAssetReference reference = EditorAssetReferenceFactory.CreateFileReference(projectRootPath, "images/checker.png", AssetEntryKind.Image);

                Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, reference.SourceKind);
                Assert.False(string.IsNullOrWhiteSpace(reference.AssetId));
                Assert.StartsWith("sha256:", reference.ContentHash, StringComparison.Ordinal);
                Assert.True(File.Exists(sourcePath + ".hmeta"));
            } finally {
                if (Directory.Exists(projectRootPath)) {
                    Directory.Delete(projectRootPath, true);
                }
            }
        }
    }
}
