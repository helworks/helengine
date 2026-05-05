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
            InitializeCore();
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
            InitializeCore();
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

        /// <summary>
        /// Ensures render settings are persisted together with the rest of the camera payload.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenCameraUsesCustomRenderIntent_RoundTripsRenderSettings() {
            InitializeCore();
            CameraComponentPersistenceDescriptor descriptor = new CameraComponentPersistenceDescriptor();
            CameraComponent cameraComponent = new CameraComponent();
            cameraComponent.RenderSettings.DepthPrepassMode = DepthPrepassMode.Always;
            cameraComponent.RenderSettings.ShadowDistance = 75f;
            cameraComponent.RenderSettings.PostProcessTier = PostProcessTier.High;

            SceneComponentAssetRecord record = descriptor.SerializeComponent(cameraComponent, 0, null);
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal(DepthPrepassMode.Always, loadedCamera.RenderSettings.DepthPrepassMode);
            Assert.Equal(75f, loadedCamera.RenderSettings.ShadowDistance);
            Assert.Equal(PostProcessTier.High, loadedCamera.RenderSettings.PostProcessTier);
        }

        /// <summary>
        /// Ensures missing tagged fields leave render settings at their component defaults during editor scene load.
        /// </summary>
        [Fact]
        public void Deserialize_WhenTaggedPayloadOmitsRenderSettings_KeepsDefaultRenderSettings() {
            InitializeCore();
            CameraComponentPersistenceDescriptor descriptor = new CameraComponentPersistenceDescriptor();
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("CameraDrawOrder", fieldWriter => fieldWriter.WriteByte(9));
            writer.WriteField("LayerMask", fieldWriter => fieldWriter.WriteUInt16(EditorLayerMasks.SceneObjects));
            writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(new float4(10f, 20f, 30f, 40f)));
            writer.WriteField("ClearSettings", fieldWriter => {
                fieldWriter.WriteByte(1);
                fieldWriter.WriteFloat4(new float4(0.2f, 0.3f, 0.4f, 1f));
                fieldWriter.WriteByte(1);
                fieldWriter.WriteSingle(0.5f);
                fieldWriter.WriteByte(0);
                fieldWriter.WriteByte(0);
            });
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = descriptor.ComponentTypeId,
                ComponentIndex = 0,
                Payload = writer.BuildPayload()
            };

            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));
            CameraComponent defaultCamera = new CameraComponent();

            Assert.Equal((byte)9, loadedCamera.CameraDrawOrder);
            Assert.Equal(EditorLayerMasks.SceneObjects, loadedCamera.LayerMask);
            Assert.Equal(new float4(10f, 20f, 30f, 40f), loadedCamera.Viewport);
            Assert.Equal(defaultCamera.RenderSettings.DepthPrepassMode, loadedCamera.RenderSettings.DepthPrepassMode);
            Assert.Equal(defaultCamera.RenderSettings.ShadowDistance, loadedCamera.RenderSettings.ShadowDistance);
            Assert.Equal(defaultCamera.RenderSettings.PostProcessTier, loadedCamera.RenderSettings.PostProcessTier);
        }

        /// <summary>
        /// Initializes a core instance so camera components can allocate their render queues during the test.
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
