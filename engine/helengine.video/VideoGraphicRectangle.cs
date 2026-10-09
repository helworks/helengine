namespace helengine.video {
    /// <summary>
    /// Axis-aligned rectangle in output pixels, used to describe where the pictures of a scene sit and which parts of a
    /// graphic's safe area they leave free.
    /// </summary>
    public sealed class VideoGraphicRectangle {
        /// <summary>
        /// Creates one rectangle from its edges.
        /// </summary>
        /// <param name="left">Left edge in pixels.</param>
        /// <param name="top">Top edge in pixels.</param>
        /// <param name="right">Right edge in pixels; not smaller than <paramref name="left"/>.</param>
        /// <param name="bottom">Bottom edge in pixels; not smaller than <paramref name="top"/>.</param>
        public VideoGraphicRectangle(double left, double top, double right, double bottom) {
            if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(right) || !double.IsFinite(bottom) || right < left || bottom < top) {
                throw new ArgumentException($"A rectangle needs finite edges with right >= left and bottom >= top (got {left}, {top}, {right}, {bottom}).");
            }
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>
        /// Gets the left edge in pixels.
        /// </summary>
        public double Left { get; }

        /// <summary>
        /// Gets the top edge in pixels.
        /// </summary>
        public double Top { get; }

        /// <summary>
        /// Gets the right edge in pixels.
        /// </summary>
        public double Right { get; }

        /// <summary>
        /// Gets the bottom edge in pixels.
        /// </summary>
        public double Bottom { get; }

        /// <summary>
        /// Gets the width in pixels.
        /// </summary>
        public double Width {
            get {
                return Right - Left;
            }
        }

        /// <summary>
        /// Gets the height in pixels.
        /// </summary>
        public double Height {
            get {
                return Bottom - Top;
            }
        }

        /// <summary>
        /// Reports whether this rectangle and another share an area larger than a hairline; touching edges do not count.
        /// </summary>
        /// <param name="other">Other rectangle.</param>
        /// <returns>True when the interiors overlap.</returns>
        public bool Overlaps(VideoGraphicRectangle other) {
            return Math.Min(Right, other.Right) - Math.Max(Left, other.Left) > 1e-6 && Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top) > 1e-6;
        }

        /// <summary>
        /// Reports whether another rectangle lies entirely inside this one.
        /// </summary>
        /// <param name="other">Other rectangle.</param>
        /// <returns>True when every edge of <paramref name="other"/> is inside or on this rectangle.</returns>
        public bool Contains(VideoGraphicRectangle other) {
            return other.Left >= Left - 1e-6 && other.Right <= Right + 1e-6 && other.Top >= Top - 1e-6 && other.Bottom <= Bottom + 1e-6;
        }

        /// <summary>
        /// Grows the rectangle by the same margin on every side.
        /// </summary>
        /// <param name="margin">Margin in pixels.</param>
        /// <returns>Grown rectangle.</returns>
        public VideoGraphicRectangle Inflate(double margin) {
            return new VideoGraphicRectangle(Left - margin, Top - margin, Right + margin, Bottom + margin);
        }
    }
}
