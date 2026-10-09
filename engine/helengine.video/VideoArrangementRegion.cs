using helengine.media;

namespace helengine.video {
    /// <summary>
    /// One arrangement region resolved for an output frame: its normalized rectangle and its size in output pixels, which
    /// products use to generate media at the aspect of the region it will fill.
    /// </summary>
    public sealed class VideoArrangementRegion {
        /// <summary>
        /// Creates one resolved region.
        /// </summary>
        /// <param name="name">Region name.</param>
        /// <param name="role">picture, graphic or background.</param>
        /// <param name="x">Left edge, 0 to 1.</param>
        /// <param name="y">Top edge, 0 to 1.</param>
        /// <param name="width">Width, 0 to 1.</param>
        /// <param name="height">Height, 0 to 1.</param>
        /// <param name="frameWidth">Output frame width in pixels.</param>
        /// <param name="frameHeight">Output frame height in pixels.</param>
        public VideoArrangementRegion(string name, string role, double x, double y, double width, double height, int frameWidth, int frameHeight) {
            Name = name;
            Role = role;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            PixelWidth = (int)Math.Round(width * frameWidth);
            PixelHeight = (int)Math.Round(height * frameHeight);
        }

        /// <summary>
        /// Gets the region name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets what the region holds: picture, graphic or background.
        /// </summary>
        public string Role { get; }

        /// <summary>
        /// Gets the normalized left edge.
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Gets the normalized top edge.
        /// </summary>
        public double Y { get; }

        /// <summary>
        /// Gets the normalized width.
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Gets the normalized height.
        /// </summary>
        public double Height { get; }

        /// <summary>
        /// Gets the region width in output pixels.
        /// </summary>
        public int PixelWidth { get; }

        /// <summary>
        /// Gets the region height in output pixels.
        /// </summary>
        public int PixelHeight { get; }

        /// <summary>
        /// Converts the region into a composition viewport.
        /// </summary>
        /// <returns>Normalized viewport.</returns>
        public LayerViewport Viewport() {
            return new LayerViewport { X = X, Y = Y, Width = Width, Height = Height };
        }

        /// <summary>
        /// Converts the region into an output-pixel rectangle.
        /// </summary>
        /// <param name="frameWidth">Output frame width in pixels.</param>
        /// <param name="frameHeight">Output frame height in pixels.</param>
        /// <returns>Rectangle in output pixels.</returns>
        public VideoGraphicRectangle Rectangle(double frameWidth, double frameHeight) {
            return new VideoGraphicRectangle(X * frameWidth, Y * frameHeight, (X + Width) * frameWidth, (Y + Height) * frameHeight);
        }
    }
}
