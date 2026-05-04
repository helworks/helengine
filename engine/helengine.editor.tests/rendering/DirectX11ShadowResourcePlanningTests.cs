using helengine.directx11;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies the DirectX11 shadow-resource planning shells used by the forward renderer.
    /// </summary>
    public class DirectX11ShadowResourcePlanningTests {
        /// <summary>
        /// Ensures the atlas planner creates one atlas allocation for each shadowed non-point light.
        /// </summary>
        [Fact]
        public void PlanAllocations_WhenDirectionalAndSpotLightsExist_ReturnsAtlasAllocations() {
            DirectX11ShadowMapAtlas atlas = new DirectX11ShadowMapAtlas(2048, 2048);
            RenderFrameLightSubmission[] lights = [
                new RenderFrameLightSubmission(new DirectionalLightComponent(), 10),
                new RenderFrameLightSubmission(new SpotLightComponent(), 8),
                new RenderFrameLightSubmission(new PointLightComponent(), 6)
            ];

            DirectX11ShadowAtlasAllocation[] allocations = atlas.PlanAllocations(lights);

            Assert.Equal(2, allocations.Length);
            Assert.All(allocations, allocation => Assert.Equal(ShadowResourceKind.Atlas, allocation.ResourceKind));
        }

        /// <summary>
        /// Ensures point-light shadow resources publish cube-shadow semantics instead of atlas semantics.
        /// </summary>
        [Fact]
        public void Constructor_WhenCreatedForPointLight_UsesCubeShadowKind() {
            DirectX11PointShadowResource resource = new DirectX11PointShadowResource(
                new RenderFrameLightSubmission(new PointLightComponent(), 4),
                512);

            Assert.Equal(ShadowResourceKind.Cube, resource.ResourceKind);
            Assert.Equal(512, resource.Resolution);
            Assert.Equal(LightType.Point, resource.Light.LightType);
        }
    }
}
