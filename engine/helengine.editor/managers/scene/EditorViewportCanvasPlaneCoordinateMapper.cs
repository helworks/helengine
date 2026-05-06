namespace helengine.editor {
    /// <summary>
    /// Converts between world-plane coordinates and simulated canvas pixel coordinates for the viewport 2D preview plane.
    /// </summary>
    public static class EditorViewportCanvasPlaneCoordinateMapper {
        /// <summary>
        /// Maps one point expressed in plane-local world units into simulated canvas pixel coordinates.
        /// </summary>
        /// <param name="worldPoint">Point expressed in plane-local world units.</param>
        /// <param name="settings">Viewport canvas preview settings that define the pixel-to-world ratio.</param>
        /// <returns>Canvas pixel coordinate derived from the supplied world point.</returns>
        public static int2 MapWorldToCanvas(float3 worldPoint, EditorViewportCanvasPreviewSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            int canvasHeight = Math.Max(1, settings.CanvasHeight);
            int canvasX = (int)Math.Round(worldPoint.X * settings.PixelsPerWorldUnit);
            int canvasY = canvasHeight - (int)Math.Round(worldPoint.Y * settings.PixelsPerWorldUnit);
            return new int2(
                canvasX,
                canvasY);
        }
    }
}
