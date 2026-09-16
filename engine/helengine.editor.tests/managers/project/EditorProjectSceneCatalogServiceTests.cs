using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies project scene catalog ids are derived from scene asset names and can be resolved back to authored paths.
    /// </summary>
    public sealed class EditorProjectSceneCatalogServiceTests : IDisposable {
        /// <summary>
        /// Gets the isolated temporary project root used by the current test instance.
        /// </summary>
        string TempProjectRootPath { get; }

        /// <summary>
        /// Initializes one isolated temporary project root for scene catalog tests.
        /// </summary>
        public EditorProjectSceneCatalogServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-project-scene-catalog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Levels"));
        }

        /// <summary>
        /// Deletes the isolated temporary project root after the current test completes.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures scene ids are derived from the authored scene asset file names instead of project-relative paths.
        /// </summary>
        [Fact]
        public void GetSceneIds_WhenScenesExist_ReturnsSceneAssetNamesWithoutExtensions() {
            WriteScene("Scenes/MainMenuScene.helen");
            WriteScene("Levels/CubeTest.helen");

            EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);

            Assert.Equal(new[] { "CubeTest", "MainMenuScene" }, service.GetSceneIds());
        }

        /// <summary>
        /// Ensures a unique scene id resolves back to its project-relative authored scene path.
        /// </summary>
        [Fact]
        public void ResolveScenePath_WhenSceneIdMatchesOneScene_ReturnsProjectRelativePath() {
            WriteScene("Scenes/MainMenuScene.helen");
            WriteScene("Levels/CubeTest.helen");

            EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);

            Assert.Equal("Scenes/MainMenuScene.helen", service.ResolveScenePath("MainMenuScene"));
            Assert.Equal("Levels/CubeTest.helen", service.ResolveScenePath("CubeTest"));
        }

        /// <summary>
        /// Ensures duplicate scene ids fail fast instead of letting editor callers pick an arbitrary scene path.
        /// </summary>
        [Fact]
        public void ResolveScenePath_WhenSceneIdMatchesMultipleScenes_ThrowsInvalidOperationException() {
            WriteScene("Scenes/MainMenuScene.helen");
            WriteScene("Levels/MainMenuScene.helen");

            EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.ResolveScenePath("MainMenuScene"));
            Assert.Contains("MainMenuScene", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Preserves authored identities, generated references, duplicates, and caller order in one batch.
        /// </summary>
        [Fact]
        public void CreateSceneReferences_MixedBatch_PreservesOrderAndIdentity() {
            string alphaId = WriteSerializedScene("Scenes/Alpha.helen");
            string betaId = WriteSerializedScene("Levels/Beta.helen");
            EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);

            List<SceneAssetReference> references = service.CreateSceneReferences(new[] { "Beta", "generated-scene", "Alpha", "Beta" });

            Assert.Equal(4, references.Count);
            Assert.Equal(betaId, references[0].AssetId);
            Assert.Equal("Levels/Beta.helen", references[0].RelativePath);
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, references[1].SourceKind);
            Assert.Equal("generated-scene", references[1].AssetId);
            Assert.Equal(alphaId, references[2].AssetId);
            Assert.Equal(betaId, references[3].AssetId);
            Assert.StartsWith("sha256:", references[0].ContentHash);
        }

        /// <summary>
        /// Starts a fresh resolver for the next batch so an asset move remains visible.
        /// </summary>
        [Fact]
        public void CreateSceneReferences_AfterAssetMoves_UsesNewPathWithSameIdentity() {
            string assetId = WriteSerializedScene("Scenes/Alpha.helen");
            EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);
            Assert.Equal("Scenes/Alpha.helen", service.CreateSceneReferences(new[] { "Alpha" })[0].RelativePath);
            File.Move(Path.Combine(TempProjectRootPath, "assets", "Scenes", "Alpha.helen"), Path.Combine(TempProjectRootPath, "assets", "Levels", "Alpha.helen"));

            SceneAssetReference reference = service.CreateSceneReferences(new[] { "Alpha" })[0];

            Assert.Equal("Levels/Alpha.helen", reference.RelativePath);
            Assert.Equal(assetId, reference.AssetId);
        }

        /// <summary>
        /// Avoids asset-index initialization when the batch only contains generated scenes.
        /// </summary>
        [Fact]
        public void CreateSceneReferences_GeneratedOnly_DoesNotReadUnrelatedMalformedAsset() {
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "invalid.hblueprint"), "invalid native asset");
            EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);

            SceneAssetReference reference = Assert.Single(service.CreateSceneReferences(new[] { "generated-scene" }));

            Assert.Equal(SceneAssetReferenceSourceKind.Generated, reference.SourceKind);
            Assert.Empty(service.CreateSceneReferences(Array.Empty<string>()));
            Assert.Throws<ArgumentNullException>(() => service.CreateSceneReferences(null));
            Assert.Throws<ArgumentException>(() => service.CreateSceneReferences(new[] { " " }));
        }

        /// <summary>
        /// Writes a current native scene with a known authoring identity for reference-resolution tests.
        /// </summary>
        /// <param name="relativePath">Destination beneath the test project's assets directory.</param>
        /// <returns>The authoring identity embedded in the scene.</returns>
        string WriteSerializedScene(string relativePath) {
            string identity = Guid.NewGuid().ToString("N");
            SceneAsset asset = new SceneAsset { Id = Path.GetFileNameWithoutExtension(relativePath), AuthoringAssetId = identity };
            using FileStream stream = File.Create(Path.Combine(TempProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar)));
            global::helengine.files.EditorAssetBinarySerializer.Serialize(stream, asset);
            return identity;
        }

        /// <summary>
        /// Writes one empty scene file that the scene catalog can enumerate.
        /// </summary>
        /// <param name="sceneRelativePath">Project-relative scene asset path to create beneath `assets`.</param>
        void WriteScene(string sceneRelativePath) {
            string scenePath = Path.Combine(TempProjectRootPath, "assets", sceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            File.WriteAllText(scenePath, string.Empty);
        }
    }
}
