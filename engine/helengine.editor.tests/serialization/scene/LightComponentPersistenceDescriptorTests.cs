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
    }
}
