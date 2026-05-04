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

        /// <summary>
        /// Ensures the shadow planner preserves shadow-enabled light priority and splits atlas versus point resources.
        /// </summary>
        [Fact]
        public void PlanResources_WhenShadowedLightsExceedBudget_SelectsShadowLightsAndBuildsExpectedResources() {
            DirectX11ShadowResourcePlanner planner = new DirectX11ShadowResourcePlanner();
            DirectionalLightComponent firstLight = new DirectionalLightComponent();
            PointLightComponent secondLight = new PointLightComponent();
            secondLight.ShadowsEnabled = true;
            SpotLightComponent thirdLight = new SpotLightComponent();
            thirdLight.ShadowsEnabled = false;
            RenderFrameLightSubmission[] lights = [
                new RenderFrameLightSubmission(firstLight, 20),
                new RenderFrameLightSubmission(secondLight, 18),
                new RenderFrameLightSubmission(thirdLight, 16)
            ];

            DirectX11ShadowResourceSet resourceSet = planner.PlanResources(lights, 2);

            Assert.Equal(2, resourceSet.SelectedShadowLights.Count);
            Assert.Same(firstLight, resourceSet.SelectedShadowLights[0].Light);
            Assert.Same(secondLight, resourceSet.SelectedShadowLights[1].Light);
            Assert.Single(resourceSet.AtlasAllocations);
            Assert.Single(resourceSet.PointShadowResources);
            Assert.Same(firstLight, resourceSet.AtlasAllocations[0].Light.Light);
            Assert.Same(secondLight, resourceSet.PointShadowResources[0].Light.Light);
        }
    }
}
