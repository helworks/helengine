using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the component-add catalog used by the properties panel.
    /// </summary>
    public class EditorComponentAddCatalogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the catalog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the editor-only camera add action.
        /// </summary>
        public EditorComponentAddCatalogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-component-add-catalog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
            EditorCameraVisualResources.ResetForTests();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test content and clears shared caches after each test.
        /// </summary>
        public void Dispose() {
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
            EditorCameraVisualResources.ResetForTests();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the catalog hides the camera option once the entity already owns one camera component.
        /// </summary>
        [Fact]
        public void GetAvailableComponents_WhenEntityAlreadyHasCamera_HidesCameraDescriptor() {
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(new CameraComponent());

            IReadOnlyList<EditorComponentAddDescriptor> components = EditorComponentAddCatalog.GetAvailableComponents(entity);

            Assert.DoesNotContain(components, component => string.Equals(component.DisplayName, "Camera", StringComparison.Ordinal));
            Assert.Contains(components, component => string.Equals(component.DisplayName, "Mesh", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the hidden camera icon does not suppress the regular mesh option.
        /// </summary>
        [Fact]
        public void GetAvailableComponents_WhenEntityHasEditorCameraVisualComponent_StillIncludesMeshDescriptor() {
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(new EditorCameraVisualComponent());

            IReadOnlyList<EditorComponentAddDescriptor> components = EditorComponentAddCatalog.GetAvailableComponents(entity);

            Assert.Contains(components, component => string.Equals(component.DisplayName, "Mesh", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the camera add action attaches the authored camera and the editor-only suppression chrome.
        /// </summary>
        [Fact]
        public void AddAction_WhenCameraDescriptorIsInvoked_AttachesEditorCameraComponents() {
            EditorEntity entity = new EditorEntity();
            EditorComponentAddDescriptor cameraDescriptor = Assert.Single(EditorComponentAddCatalog.GetAvailableComponents(entity), component => string.Equals(component.DisplayName, "Camera", StringComparison.Ordinal));

            cameraDescriptor.AddAction(entity);

            CameraComponent camera = Assert.IsType<CameraComponent>(Assert.Single(entity.Components, component => component is CameraComponent));
            EditorSceneCameraSuppressionComponent suppression = Assert.IsType<EditorSceneCameraSuppressionComponent>(Assert.Single(entity.Components, component => component is EditorSceneCameraSuppressionComponent));
            EditorCameraVisualComponent visual = Assert.IsType<EditorCameraVisualComponent>(Assert.Single(entity.Components, component => component is EditorCameraVisualComponent));

            Assert.Equal((ushort)EditorLayerMasks.SceneObjects, camera.LayerMask);
            Assert.False(camera.ClearSettings.ClearColorEnabled);
            Assert.Equal(EditorLayerMasks.SceneObjects, suppression.LayerMask);
            Assert.NotNull(visual.Model);
            Assert.NotNull(visual.Material);
        }
    }
}
