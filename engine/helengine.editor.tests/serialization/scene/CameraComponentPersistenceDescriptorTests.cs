using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the built-in camera component descriptor.
    /// </summary>
    public class CameraComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures camera persistence round-trips draw order, layer mask, viewport, and clear settings.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenCameraUsesCustomRenderSettings_RoundTripsTheComponent() {
            CameraComponentPersistenceDescriptor descriptor = new CameraComponentPersistenceDescriptor();
            CameraComponent cameraComponent = new CameraComponent {
                CameraDrawOrder = 17,
                LayerMask = EditorLayerMasks.SceneObjects,
                Viewport = new float4(12f, 24f, 640f, 360f),
                ClearSettings = new CameraClearSettings(
                    true,
                    new float4(0.25f, 0.5f, 0.75f, 1f),
                    true,
                    0.42f,
                    true,
                    9)
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(cameraComponent, 0, null);
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal((byte)17, loadedCamera.CameraDrawOrder);
            Assert.Equal(EditorLayerMasks.SceneObjects, loadedCamera.LayerMask);
            Assert.Equal(new float4(12f, 24f, 640f, 360f), loadedCamera.Viewport);
            Assert.True(loadedCamera.ClearSettings.ClearColorEnabled);
            Assert.Equal(new float4(0.25f, 0.5f, 0.75f, 1f), loadedCamera.ClearSettings.ClearColor);
            Assert.True(loadedCamera.ClearSettings.ClearDepthEnabled);
            Assert.Equal(0.42f, loadedCamera.ClearSettings.ClearDepth);
            Assert.True(loadedCamera.ClearSettings.ClearStencilEnabled);
            Assert.Equal((byte)9, loadedCamera.ClearSettings.ClearStencil);
        }

        /// <summary>
        /// Ensures serialization uses authored camera settings instead of the editor-suppressed runtime state.
        /// </summary>
        [Fact]
        public void Serialize_WhenCameraIsSuppressedInEditor_UsesAuthoredSettingsFromHiddenState() {
            EditorEntity entity = new EditorEntity {
                LayerMask = EditorLayerMasks.SceneObjects
            };
            CameraComponent cameraComponent = new CameraComponent {
                CameraDrawOrder = 4,
                LayerMask = EditorLayerMasks.SceneObjects,
                Viewport = new float4(0f, 0f, 1f, 1f),
                ClearSettings = new CameraClearSettings(true, new float4(0.1f, 0.2f, 0.3f, 1f), true, 1.0f, false, 0)
            };
            entity.AddComponent(cameraComponent);
            EditorSceneCameraSuppressionService.AttachAndSuppress(entity);

            CameraComponentPersistenceDescriptor descriptor = new CameraComponentPersistenceDescriptor();
            SceneComponentAssetRecord record = descriptor.SerializeComponent(cameraComponent, 0, null);
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal((byte)4, loadedCamera.CameraDrawOrder);
            Assert.Equal(EditorLayerMasks.SceneObjects, loadedCamera.LayerMask);
            Assert.True(loadedCamera.ClearSettings.ClearColorEnabled);
            Assert.True(loadedCamera.ClearSettings.ClearDepthEnabled);
            Assert.Equal(new float4(0.1f, 0.2f, 0.3f, 1f), loadedCamera.ClearSettings.ClearColor);
        }
    }
}
