using helengine.demo_disc_scene_writer;
using helengine.files;
using Xunit;

namespace helengine.editor.tests.tools {
    /// <summary>
    /// Verifies the committed rendering scene writer generates user-side attract-mode source files and showcase scenes.
    /// </summary>
    public sealed class RenderingSceneWriterTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current rendering scene writer test.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes one isolated project root with the minimum authored assets tree required by the writer.
        /// </summary>
        public RenderingSceneWriterTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-rendering-scene-writer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
        }

        /// <summary>
        /// Deletes the temporary project root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures rendering-scene generation writes the user-side attract-mode components beneath the rendering codebase folder.
        /// </summary>
        [Fact]
        public void WriteAll_WhenRenderingShowcaseIsGenerated_WritesMotionSourceFilesUnderCodebaseRendering() {
            RenderingSceneWriter writer = new RenderingSceneWriter();

            writer.WriteAll(ProjectRootPath);

            string renderingCodeRootPath = Path.Combine(ProjectRootPath, "assets", "codebase", "rendering");
            Assert.True(File.Exists(Path.Combine(renderingCodeRootPath, "DirectionalShadowTowerSpinComponent.cs")));
            Assert.True(File.Exists(Path.Combine(renderingCodeRootPath, "DirectionalShadowOrbitComponent.cs")));
            Assert.True(File.Exists(Path.Combine(renderingCodeRootPath, "DirectionalShadowSunSweepComponent.cs")));
            Assert.True(File.Exists(Path.Combine(renderingCodeRootPath, "DirectionalShadowCameraOrbitComponent.cs")));
        }

        /// <summary>
        /// Ensures the generated directional-shadow plaza scene includes the expected authored light, camera, and attract-mode component structure.
        /// </summary>
        [Fact]
        public void WriteAll_WhenDirectionalShadowPlazaIsGenerated_AuthorsExpectedLightCameraAndMotionComponents() {
            RenderingSceneWriter writer = new RenderingSceneWriter();

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset = ReadSceneAsset("directional-shadow-plaza.helen");

            Assert.Equal("Scenes/rendering/directional-shadow-plaza.helen", sceneAsset.Id);
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.DirectionalLightComponent"));
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.CameraComponent"));
            Assert.Equal(3, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.DirectionalShadowTowerSpinComponent, gameplay"));
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.DirectionalShadowOrbitComponent, gameplay"));
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.DirectionalShadowSunSweepComponent, gameplay"));
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.DirectionalShadowCameraOrbitComponent, gameplay"));
            Assert.Contains(sceneAsset.AssetReferences, reference => reference.RelativePath == "Engine/Models/Plane");
            Assert.Contains(sceneAsset.AssetReferences, reference => reference.RelativePath == "Engine/Models/Cube");
            Assert.Contains(sceneAsset.AssetReferences, reference => reference.RelativePath == "Engine/Materials/Standard");
        }

        /// <summary>
        /// Reads one generated rendering scene asset from the isolated temp project.
        /// </summary>
        /// <param name="sceneFileName">Scene file name stored beneath the rendering scene folder.</param>
        /// <returns>Deserialized scene asset.</returns>
        SceneAsset ReadSceneAsset(string sceneFileName) {
            string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "rendering", sceneFileName);
            using FileStream stream = File.OpenRead(scenePath);
            return Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
        }

        /// <summary>
        /// Counts the number of serialized components with one specific type id across the supplied scene hierarchy.
        /// </summary>
        /// <param name="entities">Root or child entities to inspect.</param>
        /// <param name="componentTypeId">Stable serialized component type id to count.</param>
        /// <returns>Total number of matching components.</returns>
        int CountComponents(SceneEntityAsset[] entities, string componentTypeId) {
            int count = 0;
            for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++) {
                SceneEntityAsset entity = entities[entityIndex];
                for (int componentIndex = 0; componentIndex < entity.Components.Length; componentIndex++) {
                    if (string.Equals(entity.Components[componentIndex].ComponentTypeId, componentTypeId, StringComparison.Ordinal)) {
                        count++;
                    }
                }

                count += CountComponents(entity.Children, componentTypeId);
            }

            return count;
        }
    }
}
