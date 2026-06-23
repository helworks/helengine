using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Locks the sprite-rendering contract so entity transforms remain authoritative for sprite rotation and scale.
    /// </summary>
    public sealed class SpriteTransformContractSourceAuditTests {
        /// <summary>
        /// Ensures sprite drawables no longer expose a separate component-owned rotation channel.
        /// </summary>
        [Fact]
        public void SpriteContract_whenInspectingSpriteTypes_doesNotExposeSpriteOwnedRotation() {
            string spriteComponentSource = ReadSource("helengine.core", "components", "2d", "SpriteComponent.cs");
            string spriteDrawableSource = ReadSource("helengine.core", "model", "interfaces", "ISpriteDrawable2D.cs");

            Assert.DoesNotContain("float Rotation { get; set; }", spriteComponentSource);
            Assert.DoesNotContain("float Rotation { get; set; }", spriteDrawableSource);
        }

        /// <summary>
        /// Ensures the DirectX11 sprite renderer multiplies authored sprite size by entity scale and resolves visible rotation from entity orientation only.
        /// </summary>
        [Fact]
        public void SpriteContract_whenInspectingDirectX11Renderer_usesEntityScaleAndOrientationWithoutSpriteRotation() {
            string directX11RendererSource = ReadSource("helengine.directx11", "DirectX11Renderer2D.cs");

            Assert.Contains("drawable.Parent.Scale", directX11RendererSource);
            Assert.Contains("drawable.Parent.Orientation", directX11RendererSource);
            Assert.DoesNotContain("drawable.Rotation", directX11RendererSource);
        }

        /// <summary>
        /// Ensures the Vulkan sprite renderer follows the same transform contract as the DirectX11 path.
        /// </summary>
        [Fact]
        public void SpriteContract_whenInspectingVulkanRenderer_usesEntityScaleAndOrientationWithoutSpriteRotation() {
            string vulkanRendererSource = ReadSource("helengine.vulkan", "VulkanRenderer2D.cs");

            Assert.Contains("sprite.Parent.Scale", vulkanRendererSource);
            Assert.Contains("sprite.Parent.Orientation", vulkanRendererSource);
            Assert.DoesNotContain("sprite.Rotation", vulkanRendererSource);
        }

        /// <summary>
        /// Reads one source file directly from the engine tree so the tests can assert structural contracts without executing the renderer.
        /// </summary>
        /// <param name="projectName">Engine project folder that owns the target source file.</param>
        /// <param name="relativeSegments">Relative path segments beneath the project directory.</param>
        /// <returns>Full source text for the requested file.</returns>
        static string ReadSource(string projectName, params string[] relativeSegments) {
            string[] fullSegments = new string[relativeSegments.Length + 6];
            fullSegments[0] = AppContext.BaseDirectory;
            fullSegments[1] = "..";
            fullSegments[2] = "..";
            fullSegments[3] = "..";
            fullSegments[4] = "..";
            fullSegments[5] = projectName;
            for (int index = 0; index < relativeSegments.Length; index++) {
                fullSegments[index + 6] = relativeSegments[index];
            }

            string sourcePath = Path.GetFullPath(Path.Combine(fullSegments));
            return File.ReadAllText(sourcePath);
        }
    }
}
