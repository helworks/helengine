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

        /// <summary>
        /// Resolves a layer layout inside an arrangement region: the layout preset is applied to the region as if the region
        /// were the frame, while an explicit layout viewport stays in frame coordinates and wins over the region.
        /// </summary>
        /// <param name="layout">Layout, or null for the whole region.</param>
        /// <param name="region">Region viewport in normalized frame coordinates.</param>
        /// <returns>Viewport in normalized frame coordinates.</returns>
        public static LayerViewport Viewport(VideoLayout layout, LayerViewport region) {
            LayerViewport local = Viewport(layout);
            if (layout?.Viewport != null) {
                return local;
            }
            return new LayerViewport { X = region.X + local.X * region.Width, Y = region.Y + local.Y * region.Height, Width = local.Width * region.Width, Height = local.Height * region.Height };
        }

        /// <summary>
        /// Reports whether a layer is clipped to its viewport: when it asks to be, and always when it fills an arrangement
        /// region, so a covering or zoomed picture never spills into the neighbouring region.
        /// </summary>
        /// <param name="layer">Layer.</param>
        /// <returns>True when content outside the viewport is cut.</returns>
        public static bool ClipsToViewport(VideoLayer layer) {
            return layer.ClipToViewport || layer.Region != null;
        }

        /// <summary>
        /// Resolves the viewport of one scene layer: inside its arrangement region when it claims one, otherwise from its
        /// layout alone.
        /// </summary>
        /// <param name="edit">Edit supplying the format and captions the region is resolved for.</param>
        /// <param name="scene">Scene owning the layer and declaring the arrangement.</param>
        /// <param name="layer">Layer.</param>
        /// <returns>Viewport in normalized frame coordinates.</returns>
        public static LayerViewport Viewport(VideoEdit edit, VideoScene scene, VideoLayer layer) {
            if (layer.Region == null) {
                return Viewport(layer.Layout);
            }
            return Viewport(layer.Layout, VideoArrangementPresets.Region(edit, scene, layer.Region).Viewport());
        }
    }
}
