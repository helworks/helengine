namespace helengine.video {
    /// <summary>
    /// The box a graphic is laid out in, in output pixels: a horizontal span (by default the style's wrapping width
    /// centered on the frame), the vertical band left free by the frame margins, the caption reserve and the scene's
    /// pictures, and the preferred vertical center from the overlay text style.
    /// </summary>
    public sealed class VideoGraphicSafeArea {
        /// <summary>
        /// Creates one safe area.
        /// </summary>
        /// <param name="frameWidth">Output frame width.</param>
        /// <param name="frameHeight">Output frame height.</param>
        /// <param name="left">Left edge of the box.</param>
        /// <param name="width">Widest the graphic may be.</param>
        /// <param name="top">Top edge of the free band.</param>
        /// <param name="bottom">Bottom edge of the free band.</param>
        /// <param name="centerY">Preferred vertical center.</param>
        public VideoGraphicSafeArea(double frameWidth, double frameHeight, double left, double width, double top, double bottom, double centerY) {
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            Left = left;
            Width = width;
            Top = top;
            Bottom = bottom;
            CenterY = centerY;
        }

        /// <summary>
        /// Gets the output frame width.
        /// </summary>
        public double FrameWidth { get; }

        /// <summary>
        /// Gets the output frame height.
        /// </summary>
        public double FrameHeight { get; }

        /// <summary>
        /// Gets the left edge of the box.
        /// </summary>
        public double Left { get; }

        /// <summary>
        /// Gets the widest the graphic may be.
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Gets the top edge of the free vertical band.
        /// </summary>
        public double Top { get; }

        /// <summary>
        /// Gets the bottom edge of the free vertical band.
        /// </summary>
        public double Bottom { get; }

        /// <summary>
        /// Gets the preferred vertical center of the graphic.
        /// </summary>
        public double CenterY { get; }

        /// <summary>
        /// Gets the right edge of the box.
        /// </summary>
        public double Right {
            get {
                return Left + Width;
            }
        }

        /// <summary>
        /// Gets the horizontal center the graphic is centered on.
        /// </summary>
        public double CenterX {
            get {
                return Left + Width / 2;
            }
        }

        /// <summary>
        /// Gets the height of the free vertical band.
        /// </summary>
        public double Height {
            get {
                return Bottom - Top;
            }
        }

        /// <summary>
        /// Gets the box as a rectangle.
        /// </summary>
        public VideoGraphicRectangle Bounds {
            get {
                return new VideoGraphicRectangle(Left, Top, Right, Bottom);
            }
        }
    }
}
