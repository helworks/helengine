using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Works out where a take or media layer actually draws in the frame, the way the compositor presents it: the source
    /// fitted into the layer viewport (contained or covered, centered), cut to the viewport when the layer clips, plus
    /// the whole viewport when the layer paints a visible padding color, then moved and scaled by the static transform
    /// (position in viewport fractions, scale around the viewport center). Animated zoom and motion are not included.
    /// </summary>
    public static class VideoPictureBounds {
        /// <summary>
        /// Computes the frame rectangle one picture layer covers.
        /// </summary>
        /// <param name="layer">Edit layer of kind take or media.</param>
        /// <param name="media">Media the layer shows; when its size is unknown the whole viewport counts as covered.</param>
        /// <param name="frameWidth">Output frame width in pixels.</param>
        /// <param name="frameHeight">Output frame height in pixels.</param>
        /// <returns>Covered rectangle in output pixels.</returns>
        public static VideoGraphicRectangle Resolve(VideoLayer layer, VideoMedia media, double frameWidth, double frameHeight) {
            LayerViewport viewport = VideoLayoutPresets.Viewport(layer.Layout);
            double width = viewport.Width * frameWidth, height = viewport.Height * frameHeight;
            double left = 0, top = 0, right = width, bottom = height;
            if (media.Width > 0 && media.Height > 0) {
                PresentationMapping mapping = PresentationTransform.Resolve(media.Width, media.Height, width, height, layer.Fit, 1, 0.5, 0.5);
                left = mapping.ImageX;
                top = mapping.ImageY;
                right = mapping.ImageX + mapping.FittedWidth;
                bottom = mapping.ImageY + mapping.FittedHeight;
                if (layer.ClipToViewport) {
                    left = Math.Max(left, 0);
                    top = Math.Max(top, 0);
                    right = Math.Min(right, width);
                    bottom = Math.Min(bottom, height);
                }
                if (!string.IsNullOrEmpty(layer.PaddingColor) && MediaColor.Parse(layer.PaddingColor).Alpha > 0) {
                    left = Math.Min(left, 0);
                    top = Math.Min(top, 0);
                    right = Math.Max(right, width);
                    bottom = Math.Max(bottom, height);
                }
            }
            double scaleX = 1, scaleY = 1, shiftX = 0, shiftY = 0;
            if (layer.Transform != null) {
                scaleX = Math.Abs(layer.Transform.ScaleX);
                scaleY = Math.Abs(layer.Transform.ScaleY);
                shiftX = layer.Transform.PositionX * width;
                shiftY = layer.Transform.PositionY * height;
            }
            double centerX = viewport.X * frameWidth + width / 2 + shiftX, centerY = viewport.Y * frameHeight + height / 2 + shiftY;
            return new VideoGraphicRectangle(
                centerX + (left - width / 2) * scaleX,
                centerY + (top - height / 2) * scaleY,
                centerX + (right - width / 2) * scaleX,
                centerY + (bottom - height / 2) * scaleY);
        }
    }
}
