using SharpDX.Mathematics.Interop;

namespace helengine.directx11 {
    /// <summary>
    /// Resolves whether one DirectX11 camera clear should target the full render target or only the camera viewport region.
    /// </summary>
    internal static class DirectX11CameraClearRegionResolver {
        /// <summary>
        /// Returns whether one camera rendering to the backbuffer must clear only its viewport rectangle.
        /// </summary>
        /// <param name="renderTarget">Explicit camera render target, or null when rendering to the backbuffer.</param>
        /// <param name="surface">Swap-chain surface receiving backbuffer rendering.</param>
        /// <param name="viewport">Camera viewport in pixels.</param>
        /// <returns>True when the color clear must be restricted to the viewport rectangle.</returns>
        public static bool RequiresViewportScopedBackBufferColorClear(RenderTarget renderTarget, DirectX11SwapChainSurface surface, float4 viewport) {
            if (surface == null) {
                throw new ArgumentNullException(nameof(surface));
            }

            if (renderTarget != null) {
                return false;
            }

            return !ViewportMatchesTarget(viewport, surface.Width, surface.Height);
        }

        /// <summary>
        /// Resolves one clamped integer rectangle for viewport-scoped clear operations.
        /// </summary>
        /// <param name="viewport">Camera viewport in pixels.</param>
        /// <param name="targetWidth">Target width in pixels.</param>
        /// <param name="targetHeight">Target height in pixels.</param>
        /// <returns>Clamped viewport rectangle suitable for DirectX11 clear operations.</returns>
        public static RawRectangle ResolveViewportRectangle(float4 viewport, int targetWidth, int targetHeight) {
            if (targetWidth <= 0) {
                throw new ArgumentOutOfRangeException(nameof(targetWidth), "Target width must be positive.");
            }
            if (targetHeight <= 0) {
                throw new ArgumentOutOfRangeException(nameof(targetHeight), "Target height must be positive.");
            }

            int left = ClampToRange((int)Math.Floor(viewport.X), 0, targetWidth);
            int top = ClampToRange((int)Math.Floor(viewport.Y), 0, targetHeight);
            int right = ClampToRange((int)Math.Ceiling(viewport.X + viewport.Z), left, targetWidth);
            int bottom = ClampToRange((int)Math.Ceiling(viewport.Y + viewport.W), top, targetHeight);
            return new RawRectangle(left, top, right, bottom);
        }

        /// <summary>
        /// Returns whether one viewport fully covers one target.
        /// </summary>
        /// <param name="viewport">Camera viewport in pixels.</param>
        /// <param name="targetWidth">Target width in pixels.</param>
        /// <param name="targetHeight">Target height in pixels.</param>
        /// <returns>True when the viewport covers the full target bounds.</returns>
        static bool ViewportMatchesTarget(float4 viewport, int targetWidth, int targetHeight) {
            RawRectangle rectangle = ResolveViewportRectangle(viewport, targetWidth, targetHeight);
            return rectangle.Left == 0 &&
                   rectangle.Top == 0 &&
                   rectangle.Right == targetWidth &&
                   rectangle.Bottom == targetHeight;
        }

        /// <summary>
        /// Clamps one integer to an inclusive range.
        /// </summary>
        /// <param name="value">Value to clamp.</param>
        /// <param name="min">Inclusive minimum.</param>
        /// <param name="max">Inclusive maximum.</param>
        /// <returns>Clamped value.</returns>
        static int ClampToRange(int value, int min, int max) {
            if (value < min) {
                return min;
            }
            if (value > max) {
                return max;
            }

            return value;
        }
    }
}
