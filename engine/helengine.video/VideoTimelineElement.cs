using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// One bound slot of an overlay timeline while it is compiled: what it shows (text, image or video, rectangle), its
    /// motion, its draw order and the measurements its layout needs.
    /// </summary>
    public sealed class VideoTimelineElement {
        /// <summary>
        /// Gets or sets the slot name.
        /// </summary>
        public string Slot { get; set; } = "";

        /// <summary>
        /// Gets or sets the slot kind: <c>text</c>, <c>media</c> or <c>rect</c>.
        /// </summary>
        public string Kind { get; set; } = "";

        /// <summary>
        /// Gets or sets the draw order: the slot's position in the timeline, so earlier slots are drawn behind later ones.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Gets or sets the binding the slot is filled from.
        /// </summary>
        public VideoTimelineBinding Binding { get; set; }

        /// <summary>
        /// Gets or sets what the timeline does to the slot.
        /// </summary>
        public VideoTimelineSlotMotion Motion { get; set; }

        /// <summary>
        /// Gets or sets the text style snapshot of a text or rect element (without its final font size).
        /// </summary>
        public JsonElement Style { get; set; }

        /// <summary>
        /// Gets or sets the measured width of a text at <see cref="VideoTimelineCompiler.ReferenceFontSize"/>, in pixels.
        /// </summary>
        public double TextWidth { get; set; }

        /// <summary>
        /// Gets or sets the measured height of a text at <see cref="VideoTimelineCompiler.ReferenceFontSize"/>, in pixels.
        /// </summary>
        public double TextHeight { get; set; }

        /// <summary>
        /// Gets or sets the edit image or video of a media element.
        /// </summary>
        public VideoMedia Media { get; set; }

        /// <summary>
        /// Gets or sets the text element whose width a rect element takes, or null.
        /// </summary>
        public VideoTimelineElement Match { get; set; }

        /// <summary>
        /// Gets the size fraction of a text or media element: its binding size or the kind default.
        /// </summary>
        public double SizeFraction {
            get {
                return Binding.Size ?? (Kind == "media" ? VideoTimelineBinding.DefaultMediaSize : VideoTimelineBinding.DefaultTextSize);
            }
        }

        /// <summary>
        /// Computes the element's width at scale 1 in a box, before the fit scale.
        /// </summary>
        /// <param name="box">Overlay box.</param>
        /// <returns>Width in pixels.</returns>
        public double RestWidth(VideoGraphicSafeArea box) {
            if (Kind == "text") {
                return TextWidth * FontSize(box) / VideoTimelineCompiler.ReferenceFontSize;
            } else if (Kind == "media") {
                return RestHeight(box) * (Media.Width > 0 && Media.Height > 0 ? (double)Media.Width / Media.Height : 1);
            } else if (Match != null) {
                return Match.RestWidth(box);
            }
            return (Binding.Rect.Width ?? VideoTimelineRect.DefaultWidth) * box.Width;
        }

        /// <summary>
        /// Computes the element's height at scale 1 in a box, before the fit scale.
        /// </summary>
        /// <param name="box">Overlay box.</param>
        /// <returns>Height in pixels.</returns>
        public double RestHeight(VideoGraphicSafeArea box) {
            if (Kind == "text") {
                return TextHeight * FontSize(box) / VideoTimelineCompiler.ReferenceFontSize;
            } else if (Kind == "media") {
                return SizeFraction * box.Height;
            }
            return (Binding.Rect.Height ?? VideoTimelineRect.DefaultHeight) * box.Height;
        }

        /// <summary>
        /// Computes the font size of a text element in a box, before the fit scale.
        /// </summary>
        /// <param name="box">Overlay box.</param>
        /// <returns>Font size in pixels.</returns>
        public double FontSize(VideoGraphicSafeArea box) {
            return SizeFraction * box.Height;
        }
    }
}
