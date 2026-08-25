using helengine.editor.tests.testing;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies blueprint instance creation through the editor scene creation service.
    /// </summary>
    public sealed class EditorSceneCreationServiceBlueprintTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the lightweight core harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required to construct authored editor entities.
        /// </summary>
        public EditorSceneCreationServiceBlueprintTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-creation-blueprint-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            EditorCore core = new EditorCore(new Project {
                Name = "Blueprint Creation",
                Path = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
        }

        /// <summary>
        /// Deletes the temporary content root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures blueprint instance creation produces one authored scene entity carrying the instance component and asset path.
        /// </summary>
        [Fact]
        public void CreateBlueprintInstance_CreatesSceneOwnedRootWithInstanceComponent() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();

            SceneAssetReference reference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateCurrentFileSystem("blueprints/games/split_play/GoldenCoin.hblueprint");
            EditorEntity entity = creationService.CreateBlueprintInstance("GoldenCoin", reference);

            Assert.Equal("GoldenCoin", entity.Name);
            Assert.True(entity.IsSceneOwned);
            BlueprintInstanceComponent instanceComponent = FindInstanceComponent(entity);
            Assert.NotNull(instanceComponent);
            Assert.Same(reference, instanceComponent.BlueprintAssetReference);
        }

        /// <summary>
        /// Ensures blueprint instance creation validates its inputs.
        /// </summary>
        [Fact]
        public void CreateBlueprintInstance_WhenAssetPathIsMissing_Throws() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();

            Assert.Throws<ArgumentException>(() => creationService.CreateBlueprintInstance("GoldenCoin", null));
            Assert.Throws<ArgumentException>(() => creationService.CreateBlueprintInstance(" ", global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateCurrentFileSystem("blueprints/x.hblueprint")));
        }

        /// <summary>
        /// Finds the blueprint instance component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached blueprint instance component, or null when absent.</returns>
        static BlueprintInstanceComponent FindInstanceComponent(EditorEntity entity) {
            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is BlueprintInstanceComponent instanceComponent) {
                    return instanceComponent;
                }
            }

            return null;
        }
    }
}
