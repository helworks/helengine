using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies authored light component defaults exposed to rendering systems.
    /// </summary>
    public class LightComponentTests {
        /// <summary>
        /// Ensures directional lights default to the authored shadow-capable directional-light profile.
        /// </summary>
        [Fact]
        public void DirectionalLightComponent_WhenCreated_UsesShadowCapableDirectionalDefaults() {
            DirectionalLightComponent lightComponent = new DirectionalLightComponent();

            Assert.Equal(LightType.Directional, lightComponent.LightType);
            Assert.True(lightComponent.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Auto, lightComponent.ShadowMapMode);
        }
    }
}
