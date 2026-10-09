using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Estimates the horizontal band of the frame the edit's captions occupy, before any caption is laid out: the caption
    /// style's center, font size and wrapping width, and the caption track's words per cue and per line. A line holds no
    /// more words than the wrapping width allows at an average word width, so large caption fonts on narrow frames reserve
    /// the extra lines they will wrap into. The estimate errs on the generous side; graphics and arrangement regions stay
    /// out of it.
    /// </summary>
    public sealed class VideoCaptionBand {
        /// <summary>
        /// Gap kept between the caption band and anything placed beside it (graphics, arrangement regions), as a fraction
        /// of the frame height.
        /// </summary>
        public const double Gap = 0.015;

        /// <summary>
        /// Average characters per word assumed when estimating how many words fit a line.
        /// </summary>
        const double AverageWordCharacters = 6.5;

        /// <summary>
        /// Average character advance in ems assumed for caption fonts.
        /// </summary>
        const double AverageAdvance = 0.6;

        /// <summary>
        /// Width of the space between words in ems.
        /// </summary>
        const double SpaceAdvance = 0.3;

        /// <summary>
        /// Most caption lines reserved; the renderer shrinks longer cues to fit.
        /// </summary>
        const int MaximumLines = 4;

        /// <summary>
        /// Creates one band.
        /// </summary>
        /// <param name="top">Top edge as a fraction of the frame height.</param>
        /// <param name="bottom">Bottom edge as a fraction of the frame height.</param>
        /// <param name="center">Caption center as a fraction of the frame height.</param>
        public VideoCaptionBand(double top, double bottom, double center) {
            Top = top;
            Bottom = bottom;
            Center = center;
        }

        /// <summary>
        /// Gets the top edge of the band as a fraction of the frame height.
        /// </summary>
        public double Top { get; }

        /// <summary>
        /// Gets the bottom edge of the band as a fraction of the frame height.
        /// </summary>
        public double Bottom { get; }

        /// <summary>
        /// Gets the caption center as a fraction of the frame height.
        /// </summary>
        public double Center { get; }

        /// <summary>
        /// Estimates the band of an edit's captions.
        /// </summary>
        /// <param name="edit">Edit supplying the format, the caption track and the text styles.</param>
        /// <returns>Caption band, or null when the edit draws no captions or their style is not defined.</returns>
        public static VideoCaptionBand Estimate(VideoEdit edit) {
            VideoCaptionTrack captions = edit.Tracks?.Captions;
            if (captions == null || !edit.TextStyles.TryGetValue(captions.Style ?? "", out JsonElement style) || style.ValueKind != JsonValueKind.Object) {
                return null;
            }
            double width = edit.Format.Width, height = edit.Format.Height;
            double fontSize = VideoTextStyles.Number(style, "FontSize", VideoTextStyles.DefaultFontSize);
            double wrapWidth = width * Math.Clamp(VideoTextStyles.Number(style, "MaxWidth", VideoTextStyles.DefaultMaxWidth), 0.1, 1);
            double word = AverageWordCharacters * AverageAdvance * fontSize, space = SpaceAdvance * fontSize;
            int fitting = Math.Max(1, (int)Math.Floor((wrapWidth + space) / (word + space)));
            int perLine = Math.Max(1, Math.Min(captions.WordsPerLine, fitting));
            double lines = Math.Clamp(Math.Ceiling((double)Math.Max(1, captions.WordsPerCue) / perLine), 1, MaximumLines);
            double center = VideoTextStyles.Number(style, "CenterY", VideoTextStyles.DefaultCenterY);
            double half = lines * fontSize * VideoTextStyles.LineHeight / 2 / height;
            return new VideoCaptionBand(center - half, center + half, center);
        }
    }
}
