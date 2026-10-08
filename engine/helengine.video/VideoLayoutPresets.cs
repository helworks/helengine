using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Expands layout presets into composition viewports, using the proportions Flux Studio previews have always used.
    /// </summary>
    public static class VideoLayoutPresets {
        /// <summary>
        /// Resolves a layer layout to a normalized viewport.
        /// </summary>
        /// <param name="layout">Layout, or null for the full frame.</param>
        /// <returns>Viewport in normalized frame coordinates.</returns>
        public static LayerViewport Viewport(VideoLayout layout) {
            if (layout?.Viewport != null) {
                return new LayerViewport { X = layout.Viewport.X, Y = layout.Viewport.Y, Width = layout.Viewport.Width, Height = layout.Viewport.Height };
            }
            if (layout?.Preset == "inset") {
                return new LayerViewport { X = 0.06, Y = 0.2, Width = 0.88, Height = 0.48 };
            }
            if (layout?.Preset == "side_by_side") {
                return new LayerViewport { X = 0.04, Y = 0.2, Width = 0.44, Height = 0.52 };
            }
            return new LayerViewport { X = 0, Y = 0, Width = 1, Height = 1 };
        }
    }
}
