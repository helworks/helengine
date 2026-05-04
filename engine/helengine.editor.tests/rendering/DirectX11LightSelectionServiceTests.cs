using helengine.directx11;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies DirectX11 light-budget selection from extracted frame lights.
    /// </summary>
    public class DirectX11LightSelectionServiceTests {
        /// <summary>
        /// Ensures the service keeps the highest-importance lights when the visible-light budget is exceeded.
        /// </summary>
        [Fact]
        public void SelectVisibleLights_WhenBudgetIsExceeded_PrefersHighestImportanceLights() {
            DirectX11LightSelectionService service = new DirectX11LightSelectionService();

            RenderFrameLightSubmission[] selectedLights = service.SelectVisibleLights(
                [
                    new RenderFrameLightSubmission(new DirectionalLightComponent(), 1),
                    new RenderFrameLightSubmission(new DirectionalLightComponent(), 5),
                    new RenderFrameLightSubmission(new DirectionalLightComponent(), 10)
                ],
                2);

            Assert.Equal([10, 5], [selectedLights[0].Importance, selectedLights[1].Importance]);
        }
    }
}
