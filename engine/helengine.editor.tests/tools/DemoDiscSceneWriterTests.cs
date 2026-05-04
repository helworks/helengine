using helengine.demo_disc_scene_writer;
using helengine.editor.tests.testing;
using helengine.files;
using Xunit;

namespace helengine.editor.tests.tools {
    /// <summary>
    /// Verifies the demo-disc scene writer emits authored scene payloads the editor can deserialize.
    /// </summary>
    public class DemoDiscSceneWriterTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the scene-writer regression test.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes one isolated project root for the scene-writer test.
        /// </summary>
        public DemoDiscSceneWriterTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-demo-disc-writer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "user_settings"));
            File.WriteAllText(Path.Combine(ProjectRootPath, "project.heproj"), """
{
  "projectFormatVersion": 1,
  "name": "Neon City",
  "version": "0.1.0",
  "requiredEngineVersion": "0.0.0",
  "supportedPlatforms": [
    "windows"
  ],
  "created": "2026-05-04T00:00:00Z",
  "lastOpened": "2026-05-04T00:00:00Z",
  "description": "Temporary test project."
}
""");
            File.WriteAllText(Path.Combine(ProjectRootPath, "user_settings", "build_config.json"), """
{
  "platforms": [
    {
      "platformId": "windows",
      "selectedSceneIds": [],
      "sceneOrders": [],
      "outputDirectoryPath": "",
      "debugBuild": false,
      "selectedBuildProfileId": "",
      "selectedGraphicsProfileId": "",
      "selectedBuildOptionValues": {},
      "selectedGraphicsOptionValues": {},
      "selectedCodegenProfileId": "",
      "selectedStorageProfileId": "",
      "selectedMediaProfileId": "",
      "selectedCodegenOptionValues": {},
      "selectedCodeModuleIds": []
    }
  ],
  "queueItems": []
}
""");
        }

        /// <summary>
        /// Deletes the temporary project root after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the generated menu scene contains an authored camera payload the editor can deserialize.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_ProducesCameraPayloadTheEditorCanDeserialize() {
            InitializeCore();
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset;
            string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
            using (FileStream stream = File.OpenRead(scenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            }

            SceneEntityAsset cameraEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscCamera");
            SceneComponentAssetRecord cameraRecord = Assert.Single(cameraEntity.Components, component => component.ComponentTypeId == "helengine.CameraComponent");
            CameraComponentPersistenceDescriptor descriptor = new CameraComponentPersistenceDescriptor();

            CameraComponent cameraComponent = Assert.IsType<CameraComponent>(
                descriptor.DeserializeComponent(cameraRecord, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal((byte)0, cameraComponent.CameraDrawOrder);
            Assert.Equal((ushort)1, cameraComponent.LayerMask);
            Assert.Equal(new float4(0f, 0f, 1280f, 720f), cameraComponent.Viewport);
        }

        /// <summary>
        /// Ensures the generated menu scene persists a provider type name that matches the editor script assembly name.
        /// </summary>
        [Fact]
        public void WriteAll_WhenMenuSceneIsGenerated_UsesTheProjectAssemblyNameForTheProviderType() {
            DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

            writer.WriteAll(ProjectRootPath);

            SceneAsset sceneAsset;
            string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
            using (FileStream stream = File.OpenRead(scenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            }

            SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneComponentAssetRecord menuRecord = Assert.Single(menuEntity.Components, component => component.ComponentTypeId == MenuHostComponent.SerializedComponentTypeId);
            MenuHostComponentPersistenceDescriptor descriptor = new MenuHostComponentPersistenceDescriptor();

            MenuHostComponent menuHostComponent = Assert.IsType<MenuHostComponent>(
                descriptor.DeserializeComponent(menuRecord, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal("city.menu.DemoDiscMenuDefinitionProvider, Neon_City", menuHostComponent.ProviderTypeName);
        }

        /// <summary>
        /// Initializes a core instance so camera components can allocate their render queues during deserialization.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }
    }
}
