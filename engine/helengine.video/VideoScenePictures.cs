namespace helengine.video {
    /// <summary>
    /// Collects the frame rectangles a scene's pictures cover, which overlay graphics and timelines without a region keep
    /// clear of: every take and visual media layer as the compositor presents it (see <see cref="VideoPictureBounds"/>),
    /// or the take filling the frame when a take scene declares no layers. Text layers and audio media do not count.
    /// </summary>
    public static class VideoScenePictures {
        /// <summary>
        /// Collects the picture rectangles of one scene.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <returns>Picture rectangles in output pixels.</returns>
        public static List<VideoGraphicRectangle> Collect(VideoCompileState state, VideoSceneSpan span) {
            double width = state.Edit.Format.Width, height = state.Edit.Format.Height;
            List<VideoGraphicRectangle> pictures = new List<VideoGraphicRectangle>();
            VideoScene scene = span.Scene;
            if (scene.Take != null && scene.Layers.Count == 0) {
                pictures.Add(VideoPictureBounds.Resolve(new VideoLayer { Id = "take", Kind = "take", Fit = "cover" }, state.Media[scene.Take.Media], width, height));
            }
            foreach (VideoLayer layer in scene.Layers) {
                VideoMedia media = null;
                if (layer.Kind == "take" && scene.Take != null) {
                    media = state.Media[scene.Take.Media];
                } else if (layer.Kind == "media") {
                    media = state.Media[layer.Media];
                }
                if (media != null && media.Kind != "audio") {
                    pictures.Add(VideoPictureBounds.Resolve(layer, VideoLayoutPresets.Viewport(state.Edit, scene, layer), media, width, height));
                }
            }
            return pictures;
        }
    }
}
