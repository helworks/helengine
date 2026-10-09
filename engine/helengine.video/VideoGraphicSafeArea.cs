using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// The box a graphic is laid out in, in output pixels: a horizontal span (by default the style's wrapping width
    /// centered on the frame), the vertical band left free by the frame margins, the caption reserve and the scene's
    /// pictures, and the preferred vertical center from the overlay text style. For an overlay placed in an arrangement
    /// region the box is the region itself, cut to the caption-safe band.
    /// </summary>
    public sealed class VideoGraphicSafeArea {
        /// <summary>
        /// Top and bottom frame margins, as a fraction of the frame height.
        /// </summary>
        const double FrameMargin = 0.07;

        /// <summary>
        /// Smallest share of a region's height the caption-safe band must leave for the region to be cut to it; below it
        /// the region is used whole.
        /// </summary>
        const double RegionMinimumShare = 0.5;

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
    
        /// <summary>
        /// Computes the safe area of an overlay style: the style's wrapping width centered, frame margins top and bottom,
        /// and the estimated caption band (see <see cref="VideoCaptionBand"/>) kept free when captions are drawn below (or
        /// above) the style's preferred center.
        /// </summary>
        /// <param name="edit">Edit supplying the format, the caption track and the text styles.</param>
        /// <param name="style">Overlay text style snapshot.</param>
        /// <returns>Safe area in output pixels.</returns>
        public static VideoGraphicSafeArea ForStyle(VideoEdit edit, JsonElement style) {
            double width = edit.Format.Width, height = edit.Format.Height;
            double top = height * FrameMargin, bottom = height * (1 - FrameMargin);
            double centerY = VideoTextStyles.Number(style, "CenterY", VideoTextStyles.DefaultCenterY) * height;
            VideoCaptionBand captions = VideoCaptionBand.Estimate(edit);
            if (captions != null) {
                if (captions.Center * height >= centerY) {
                    bottom = Math.Min(bottom, captions.Top * height - VideoCaptionBand.Gap * height);
                } else {
                    top = Math.Max(top, captions.Bottom * height + VideoCaptionBand.Gap * height);
                }
            }
            if (bottom - top < height * 0.2) {
                top = height * FrameMargin;
                bottom = height * (1 - FrameMargin);
            }
            double safeWidth = width * Math.Clamp(VideoTextStyles.Number(style, "MaxWidth", VideoTextStyles.DefaultMaxWidth), 0.1, 1);
            return new VideoGraphicSafeArea(width, height, (width - safeWidth) / 2, safeWidth, top, bottom, centerY);
        }

        /// <summary>
        /// Computes the box of an overlay placed in an arrangement region: the region rectangle, cut to the caption-safe
        /// band of <see cref="ForStyle"/> when that still leaves most of the region, preferring the middle of the box.
        /// </summary>
        /// <param name="edit">Edit supplying the format, the caption track and the text styles.</param>
        /// <param name="style">Overlay text style snapshot.</param>
        /// <param name="region">Region rectangle in output pixels.</param>
        /// <returns>Region box in output pixels.</returns>
        public static VideoGraphicSafeArea ForRegion(VideoEdit edit, JsonElement style, VideoGraphicRectangle region) {
            VideoGraphicSafeArea safe = ForStyle(edit, style);
            double top = Math.Max(region.Top, safe.Top), bottom = Math.Min(region.Bottom, safe.Bottom);
            if (bottom - top < region.Height * RegionMinimumShare) {
                top = region.Top;
                bottom = region.Bottom;
            }
            return new VideoGraphicSafeArea(safe.FrameWidth, safe.FrameHeight, region.Left, region.Width, top, bottom, (top + bottom) / 2);
        }
    }
}
