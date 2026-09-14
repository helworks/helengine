namespace helengine.vulkan {
    /// <summary>
    /// Applies the shared 2D clip stack to the Vulkan dynamic scissor state, converting logical clip rectangles into physical pixels with the current surface scale.
    /// </summary>
    public class VulkanClipScissorStack : ClipScissorStack2D {
        /// <summary>
        /// Renderer that records the scissor commands into the active command buffer.
        /// </summary>
        readonly VulkanRenderer2D Owner;

        /// <summary>
        /// Camera scissor rectangle in physical pixels, applied whenever the clip stack empties.
        /// </summary>
        int CameraScissorX;

        /// <summary>
        /// Camera scissor top edge in physical pixels.
        /// </summary>
        int CameraScissorY;

        /// <summary>
        /// Camera scissor width in physical pixels.
        /// </summary>
        int CameraScissorWidth;

        /// <summary>
        /// Camera scissor height in physical pixels.
        /// </summary>
        int CameraScissorHeight;

        /// <summary>
        /// Camera viewport in logical coordinates, used to clip resolved clip rectangles before scaling.
        /// </summary>
        float4 ClipViewportRect;

        /// <summary>
        /// Horizontal logical-to-physical pixel scale of the current surface.
        /// </summary>
        double ClipPixelScaleX;

        /// <summary>
        /// Vertical logical-to-physical pixel scale of the current surface.
        /// </summary>
        double ClipPixelScaleY;

        /// <summary>
        /// Initializes the stack against the renderer that records its scissor commands.
        /// </summary>
        /// <param name="owner">Renderer that records the scissor commands.</param>
        public VulkanClipScissorStack(VulkanRenderer2D owner) {
            if (owner == null) {
                throw new ArgumentNullException(nameof(owner));
            }

            Owner = owner;
        }

        /// <summary>
        /// Records the camera scissor rectangle that unclipped drawables fall back to. The rectangle is recorded only; the caller issues the initial viewport and scissor commands.
        /// </summary>
        /// <param name="x">Left edge in physical pixels.</param>
        /// <param name="y">Top edge in physical pixels.</param>
        /// <param name="width">Width in physical pixels.</param>
        /// <param name="height">Height in physical pixels.</param>
        public void SetCameraScissor(int x, int y, int width, int height) {
            CameraScissorX = x;
            CameraScissorY = y;
            CameraScissorWidth = width;
            CameraScissorHeight = height;
        }

        /// <summary>
        /// Records the logical camera viewport and the surface pixel scale used to convert clip rectangles into physical scissor rectangles.
        /// </summary>
        /// <param name="viewportRect">Camera viewport in logical coordinates.</param>
        /// <param name="pixelScaleX">Horizontal logical-to-physical pixel scale.</param>
        /// <param name="pixelScaleY">Vertical logical-to-physical pixel scale.</param>
        public void SetClipViewport(float4 viewportRect, double pixelScaleX, double pixelScaleY) {
            ClipViewportRect = viewportRect;
            ClipPixelScaleX = pixelScaleX;
            ClipPixelScaleY = pixelScaleY;
        }

        /// <summary>
        /// Restores the camera viewport scissor after the clip stack empties.
        /// </summary>
        protected override void ApplyCameraScissor() {
            Owner.SetScissorRect(CameraScissorX, CameraScissorY, CameraScissorWidth, CameraScissorHeight);
        }

        /// <summary>
        /// Applies one resolved clip rectangle to the active Vulkan scissor state.
        /// </summary>
        /// <param name="clipRect">Logical clip rectangle resolved for the current drawable.</param>
        protected override void ApplyClipScissor(float4 clipRect) {
            float4 effectiveRect = Intersect(ClipViewportRect, clipRect);

            int scissorX = (int)Math.Round(effectiveRect.X * ClipPixelScaleX);
            int scissorY = (int)Math.Round(effectiveRect.Y * ClipPixelScaleY);
            int scissorWidth = Math.Max(0, (int)Math.Round(effectiveRect.Z * ClipPixelScaleX));
            int scissorHeight = Math.Max(0, (int)Math.Round(effectiveRect.W * ClipPixelScaleY));
            Owner.SetScissorRect(scissorX, scissorY, scissorWidth, scissorHeight);
        }
    }
}
