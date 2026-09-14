using helengine.vulkan;
using System.Reflection;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies the Vulkan backend declares its own renderer capability profile instead of inheriting the renderer base-class default.
    /// </summary>
    public class VulkanRenderCapabilityProfileTests {
        /// <summary>
        /// Ensures VulkanRenderer3D overrides the capability profile hook rather than publishing whatever the base class happens to return.
        /// </summary>
        [Fact]
        public void VulkanRenderer3D_declares_its_own_capability_profile() {
            MethodInfo method = typeof(VulkanRenderer3D).GetMethod(nameof(VulkanRenderer3D.GetCapabilityProfile), BindingFlags.Instance | BindingFlags.Public);

            Assert.NotNull(method);
            Assert.Equal(typeof(VulkanRenderer3D), method.DeclaringType);
        }

        /// <summary>
        /// Ensures the published Vulkan profile matches what the backend implements today: a single forward pass, no deferred path, a non-HDR sRGB swapchain, no tangent frame for normal mapping, and no engine-uploaded light or shadow buffers.
        /// </summary>
        [Fact]
        public void Vulkan_capability_profile_reports_the_forward_only_unlit_backend() {
            RendererBackendCapabilityProfile profile = VulkanRenderCapabilityProfile.CreateDefault();

            Assert.True(profile.SupportsForwardRendering);
            Assert.False(profile.SupportsDeferredRendering);
            Assert.False(profile.SupportsHdr);
            Assert.False(profile.SupportsNormalMaps);
            Assert.Equal(0, profile.MaximumVisibleLights);
            Assert.Equal(0, profile.MaximumShadowedLights);
        }
    }
}
