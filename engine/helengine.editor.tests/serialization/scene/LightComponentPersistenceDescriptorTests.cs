using helengine.editor;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for authored light component descriptors.
    /// </summary>
    public class LightComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures directional light persistence round-trips the shared authored light fields.
        /// </summary>
        [Fact]
        public void DirectionalLightDescriptor_WhenRoundTripped_PreservesSharedFields() {
            DirectionalLightComponentPersistenceDescriptor descriptor = new DirectionalLightComponentPersistenceDescriptor();
            DirectionalLightComponent lightComponent = new DirectionalLightComponent {
                Color = new float4(0.25f, 0.5f, 0.75f, 1f),
                Intensity = 3.5f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 0.65f,
                ShadowDistance = 72f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            DirectionalLightComponent loadedLight = Assert.IsType<DirectionalLightComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(lightComponent.Color, loadedLight.Color);
            Assert.Equal(lightComponent.Intensity, loadedLight.Intensity);
            Assert.Equal(lightComponent.ShadowsEnabled, loadedLight.ShadowsEnabled);
            Assert.Equal(lightComponent.ShadowMapMode, loadedLight.ShadowMapMode);
            Assert.Equal(lightComponent.ShadowStrength, loadedLight.ShadowStrength);
            Assert.Equal(lightComponent.ShadowDistance, loadedLight.ShadowDistance);
        }

        /// <summary>
        /// Ensures ambient light persistence round-trips the shared authored light fields.
        /// </summary>
        [Fact]
        public void AmbientLightDescriptor_WhenRoundTripped_PreservesSharedFields() {
            AmbientLightComponentPersistenceDescriptor descriptor = new AmbientLightComponentPersistenceDescriptor();
            AmbientLightComponent lightComponent = new AmbientLightComponent {
                Color = new float4(0.1f, 0.2f, 0.35f, 1f),
                Intensity = 1.75f,
                ShadowsEnabled = false,
                ShadowMapMode = ShadowMapMode.Disabled,
                ShadowStrength = 0.25f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            AmbientLightComponent loadedLight = Assert.IsType<AmbientLightComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(lightComponent.Color, loadedLight.Color);
            Assert.Equal(lightComponent.Intensity, loadedLight.Intensity);
            Assert.Equal(lightComponent.ShadowsEnabled, loadedLight.ShadowsEnabled);
            Assert.Equal(lightComponent.ShadowMapMode, loadedLight.ShadowMapMode);
            Assert.Equal(lightComponent.ShadowStrength, loadedLight.ShadowStrength);
        }

        /// <summary>
        /// Ensures point light persistence round-trips shared fields plus range.
        /// </summary>
        [Fact]
        public void PointLightDescriptor_WhenRoundTripped_PreservesRangeAndSharedFields() {
            PointLightComponentPersistenceDescriptor descriptor = new PointLightComponentPersistenceDescriptor();
            PointLightComponent lightComponent = new PointLightComponent {
                Color = new float4(1f, 0.8f, 0.6f, 1f),
                Intensity = 4.25f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Auto,
                ShadowStrength = 0.9f,
                Range = 18f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            PointLightComponent loadedLight = Assert.IsType<PointLightComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(lightComponent.Color, loadedLight.Color);
            Assert.Equal(lightComponent.Intensity, loadedLight.Intensity);
            Assert.Equal(lightComponent.ShadowsEnabled, loadedLight.ShadowsEnabled);
            Assert.Equal(lightComponent.ShadowMapMode, loadedLight.ShadowMapMode);
            Assert.Equal(lightComponent.ShadowStrength, loadedLight.ShadowStrength);
            Assert.Equal(lightComponent.Range, loadedLight.Range);
        }

        /// <summary>
        /// Ensures spot light persistence round-trips shared fields plus cone settings.
        /// </summary>
        [Fact]
        public void SpotLightDescriptor_WhenRoundTripped_PreservesConeSettingsAndSharedFields() {
            SpotLightComponentPersistenceDescriptor descriptor = new SpotLightComponentPersistenceDescriptor();
            SpotLightComponent lightComponent = new SpotLightComponent {
                Color = new float4(0.9f, 0.9f, 1f, 1f),
                Intensity = 2.75f,
                ShadowsEnabled = false,
                ShadowMapMode = ShadowMapMode.Disabled,
                ShadowStrength = 0.5f,
                Range = 22f,
                InnerConeAngleDegrees = 17f,
                OuterConeAngleDegrees = 31f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            SpotLightComponent loadedLight = Assert.IsType<SpotLightComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(lightComponent.Color, loadedLight.Color);
            Assert.Equal(lightComponent.Intensity, loadedLight.Intensity);
            Assert.Equal(lightComponent.ShadowsEnabled, loadedLight.ShadowsEnabled);
            Assert.Equal(lightComponent.ShadowMapMode, loadedLight.ShadowMapMode);
            Assert.Equal(lightComponent.ShadowStrength, loadedLight.ShadowStrength);
            Assert.Equal(lightComponent.Range, loadedLight.Range);
            Assert.Equal(lightComponent.InnerConeAngleDegrees, loadedLight.InnerConeAngleDegrees);
            Assert.Equal(lightComponent.OuterConeAngleDegrees, loadedLight.OuterConeAngleDegrees);
        }

        /// <summary>
        /// Ensures unknown tagged fields do not block directional light deserialization in editor scenes.
        /// </summary>
        [Fact]
        public void DirectionalLightDescriptor_WhenTaggedPayloadContainsUnknownField_IgnoresTheField() {
            DirectionalLightComponentPersistenceDescriptor descriptor = new DirectionalLightComponentPersistenceDescriptor();
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("Color", fieldWriter => fieldWriter.WriteFloat4(new float4(0.25f, 0.5f, 0.75f, 1f)));
            writer.WriteField("Intensity", fieldWriter => fieldWriter.WriteSingle(3.5f));
            writer.WriteField("ShadowsEnabled", fieldWriter => fieldWriter.WriteByte(1));
            writer.WriteField("ShadowMapMode", fieldWriter => fieldWriter.WriteByte((byte)ShadowMapMode.Forced));
            writer.WriteField("ShadowStrength", fieldWriter => fieldWriter.WriteSingle(0.65f));
            writer.WriteField("ShadowDistance", fieldWriter => fieldWriter.WriteSingle(72f));
            writer.WriteField("FutureField", fieldWriter => fieldWriter.WriteString("ignored"));
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = descriptor.ComponentTypeId,
                ComponentIndex = 0,
                Payload = writer.BuildPayload()
            };

            DirectionalLightComponent loadedLight = Assert.IsType<DirectionalLightComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(new float4(0.25f, 0.5f, 0.75f, 1f), loadedLight.Color);
            Assert.Equal(3.5f, loadedLight.Intensity);
            Assert.True(loadedLight.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Forced, loadedLight.ShadowMapMode);
            Assert.Equal(0.65f, loadedLight.ShadowStrength);
            Assert.Equal(72f, loadedLight.ShadowDistance);
        }
    }
}
