using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.physics {
    /// <summary>
    /// Verifies the cube-only physics validation scene generator.
    /// </summary>
    public sealed class PhysicsValidationSceneFactoryTests : IDisposable {
        /// <summary>
        /// Temporary project root used for generated physics scene outputs.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root for physics scene generation tests.
        /// </summary>
        public PhysicsValidationSceneFactoryTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-physics-validation-scene-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));
        }

        /// <summary>
        /// Removes temporary generated project data after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the catalog exports only the cube stack scene.
        /// </summary>
        [Fact]
        public void GetSceneIds_ReturnsOnlyDynamicStackBoxesScene() {
            string[] sceneIds = PhysicsValidationSceneCatalog.GetSceneIds();

            Assert.Equal(new[] {
                PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId
            }, sceneIds);
        }

        /// <summary>
        /// Ensures stale validation scene ids fail clearly instead of generating unsupported physics content.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_WithLegacySceneId_ThrowsNotSupportedException() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            Assert.Throws<InvalidOperationException>(() => factory.CreateSceneAsset(PhysicsValidationSceneCatalog.CharacterSlopeSceneId));
        }

        /// <summary>
        /// Ensures the remaining validation scene contains only rigid bodies and box colliders.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_WithDynamicStackScene_UsesOnlyCubePhysicsComponents() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            SceneAsset sceneAsset = factory.CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId);

            Assert.Equal(PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId, sceneAsset.Id);
            Assert.DoesNotContain(FlattenComponents(sceneAsset.RootEntities), component => component.ComponentTypeId.Contains("Sphere", StringComparison.Ordinal));
            Assert.DoesNotContain(FlattenComponents(sceneAsset.RootEntities), component => component.ComponentTypeId.Contains("Capsule", StringComparison.Ordinal));
            Assert.DoesNotContain(FlattenComponents(sceneAsset.RootEntities), component => component.ComponentTypeId.Contains("StaticMeshCollider", StringComparison.Ordinal));
            Assert.DoesNotContain(FlattenComponents(sceneAsset.RootEntities), component => component.ComponentTypeId.Contains("CharacterController", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures writing physics scenes emits only the remaining stack scene file.
        /// </summary>
        [Fact]
        public void WriteScenes_WritesOnlyDynamicStackBoxesScene() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            factory.WriteScenes(TempProjectRootPath);

            string expectedPath = GetSceneFullPath(TempProjectRootPath, PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId);
            Assert.True(File.Exists(expectedPath));
            Assert.Single(Directory.GetFiles(Path.Combine(TempProjectRootPath, "assets", "scenes", "physics"), "*.helen"));
        }

        /// <summary>
        /// Flattens component records from a scene entity hierarchy.
        /// </summary>
        /// <param name="entities">Root entities to inspect.</param>
        /// <returns>Flattened component records.</returns>
        static List<SceneComponentAssetRecord> FlattenComponents(IReadOnlyList<SceneEntityAsset> entities) {
            List<SceneComponentAssetRecord> components = new List<SceneComponentAssetRecord>();
            AppendComponents(entities, components);
            return components;
        }

        /// <summary>
        /// Appends component records from a scene entity hierarchy into one output list.
        /// </summary>
        /// <param name="entities">Entities to inspect.</param>
        /// <param name="components">Output component list.</param>
        static void AppendComponents(IReadOnlyList<SceneEntityAsset> entities, List<SceneComponentAssetRecord> components) {
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                SceneEntityAsset entity = entities[entityIndex];
                if (entity.Components != null) {
                    for (int componentIndex = 0; componentIndex < entity.Components.Length; componentIndex++) {
                        components.Add(entity.Components[componentIndex]);
                    }
                }
                if (entity.Children != null) {
                    AppendComponents(entity.Children, components);
                }
            }
        }

        /// <summary>
        /// Resolves the full generated scene path for one scene id.
        /// </summary>
        /// <param name="projectRootPath">Temporary project root path.</param>
        /// <param name="sceneId">Scene id to resolve.</param>
        /// <returns>Absolute generated scene path.</returns>
        static string GetSceneFullPath(string projectRootPath, string sceneId) {
            return Path.Combine(projectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
