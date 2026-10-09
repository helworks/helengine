using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Lays out the blocks of one graphic with measured text: every supported direction is fitted into the safe area
    /// with one uniform scale (re-measured at the scaled size, since glyph advances are not perfectly linear), then the
    /// direction is chosen — horizontal when the row fits with at most a mild shrink (15% on tall frames, 30% on wide
    /// ones), otherwise whichever direction keeps the text largest, preferring vertical on ties — and the blocks are
    /// centered in the safe area. Choosing which free part of the frame to use is <see cref="VideoGraphicFreeArea"/>'s job.
    /// </summary>
    public static class VideoGraphicLayout {
        /// <summary>
        /// Most fitting passes per direction.
        /// </summary>
        const int FitPasses = 6;

        /// <summary>
        /// Smallest fit scale at which a tall frame still prefers one row: a phrase reads better slightly smaller on one
        /// line than broken into a column of single words.
        /// </summary>
        const double PortraitRowScale = 0.85;

        /// <summary>
        /// Smallest fit scale at which a wide frame still prefers one row, since a column would use most of its height.
        /// </summary>
        const double LandscapeRowScale = 0.7;

        /// <summary>
        /// Arranges the blocks and writes their measured sizes and centers.
        /// </summary>
        /// <param name="blocks">Blocks in reading order.</param>
        /// <param name="template">Template supplying the supported layouts and their gaps.</param>
        /// <param name="requested">auto, vertical or horizontal.</param>
        /// <param name="style">Overlay text style snapshot used for measuring.</param>
        /// <param name="measurer">Text measurer.</param>
        /// <param name="area">Safe area in output pixels.</param>
        /// <returns>Chosen direction, fit scale and fitted item font size.</returns>
        public static VideoGraphicArrangement Arrange(IReadOnlyList<VideoGraphicBlock> blocks, GraphicTemplateAsset template, string requested, JsonElement style, IVideoTextMeasurer measurer, VideoGraphicSafeArea area) {
            return Arrange(blocks, template, requested, style, measurer, area, 1);
        }

        /// <summary>
        /// Arranges the blocks and writes their measured sizes and centers, letting the text grow up to a maximum scale
        /// when the area has room for it (used for graphics placed in an arrangement region). When growth is allowed a row
        /// must keep the text within the usual mild shrink of what the best column reaches, not of the style size.
        /// </summary>
        /// <param name="blocks">Blocks in reading order.</param>
        /// <param name="template">Template supplying the supported layouts and their gaps.</param>
        /// <param name="requested">auto, vertical or horizontal.</param>
        /// <param name="style">Overlay text style snapshot used for measuring.</param>
        /// <param name="measurer">Text measurer.</param>
        /// <param name="area">Safe area in output pixels.</param>
        /// <param name="maximumScale">Largest fit scale, one or more; one keeps the style font size as the ceiling.</param>
        /// <returns>Chosen direction, fit scale and fitted item font size.</returns>
        public static VideoGraphicArrangement Arrange(IReadOnlyList<VideoGraphicBlock> blocks, GraphicTemplateAsset template, string requested, JsonElement style, IVideoTextMeasurer measurer, VideoGraphicSafeArea area, double maximumScale) {
            if (!double.IsFinite(maximumScale) || maximumScale < 1) {
                throw new ArgumentOutOfRangeException(nameof(maximumScale), "The maximum scale must be one or more.");
            }
            if (blocks.Count == 0) {
                throw new ArgumentException("A graphic needs at least one block.", nameof(blocks));
            }
            double baseSize = VideoTextStyles.Number(style, "FontSize", VideoTextStyles.DefaultFontSize);
            List<GraphicTemplateLayoutAsset> candidates = template.Layouts.Where(layout => requested == "auto" || Name(layout.Direction) == requested).ToList();
            if (candidates.Count == 0) {
                throw new InvalidDataException($"Graphic template '{template.TemplateId}' has no {requested} layout.");
            }
            GraphicTemplateLayoutAsset chosen = null;
            double chosenScale = 0;
            GraphicTemplateLayoutAsset horizontal = candidates.FirstOrDefault(layout => layout.Direction == GraphicLayoutDirection.Horizontal);
            double horizontalScale = horizontal == null ? 0 : Fit(blocks, horizontal, style, measurer, area, baseSize, maximumScale);
            double reference = 1;
            if (maximumScale > 1) {
                GraphicTemplateLayoutAsset vertical = candidates.FirstOrDefault(layout => layout.Direction == GraphicLayoutDirection.Vertical);
                reference = vertical == null ? 1 : Math.Max(1, Fit(blocks, vertical, style, measurer, area, baseSize, maximumScale));
            }
            if (horizontal != null && horizontalScale >= reference * (area.FrameWidth >= area.FrameHeight ? LandscapeRowScale : PortraitRowScale)) {
                chosen = horizontal;
                chosenScale = horizontalScale;
            } else {
                foreach (GraphicTemplateLayoutAsset candidate in candidates.OrderBy(layout => layout.Direction == GraphicLayoutDirection.Vertical ? 0 : 1)) {
                    double scale = Fit(blocks, candidate, style, measurer, area, baseSize, maximumScale);
                    if (chosen == null || scale > chosenScale + 1e-6) {
                        chosen = candidate;
                        chosenScale = scale;
                    }
                }
            }
            Measure(blocks, style, measurer, baseSize * chosenScale);
            Place(blocks, chosen, style, area, baseSize * chosenScale);
            return new VideoGraphicArrangement(chosen.Direction, chosenScale, baseSize * chosenScale);
        }

        /// <summary>
        /// Names a direction the way edits and the capability catalog spell it.
        /// </summary>
        /// <param name="direction">Direction.</param>
        /// <returns><c>vertical</c> or <c>horizontal</c>.</returns>
        public static string Name(GraphicLayoutDirection direction) {
            return direction == GraphicLayoutDirection.Vertical ? "vertical" : "horizontal";
        }

        /// <summary>
        /// Finds the largest uniform scale, at most the maximum scale, at which the blocks fit the safe area in one direction.
        /// </summary>
        /// <param name="blocks">Blocks.</param>
        /// <param name="layout">Direction and gap.</param>
        /// <param name="style">Style used for measuring.</param>
        /// <param name="measurer">Text measurer.</param>
        /// <param name="area">Safe area.</param>
        /// <param name="baseSize">Item font size before fitting.</param>
        /// <param name="maximumScale">Largest scale tried first.</param>
        /// <returns>Fit scale.</returns>
        static double Fit(IReadOnlyList<VideoGraphicBlock> blocks, GraphicTemplateLayoutAsset layout, JsonElement style, IVideoTextMeasurer measurer, VideoGraphicSafeArea area, double baseSize, double maximumScale) {
            double scale = maximumScale;
            for (int pass = 0; pass < FitPasses; pass++) {
                Measure(blocks, style, measurer, baseSize * scale);
                double ratio = Math.Min(area.Width / Width(blocks, layout, style, baseSize * scale), area.Height / Height(blocks, layout, style, baseSize * scale));
                if (ratio >= 1) {
                    return scale;
                }
                scale *= ratio * 0.995;
            }
            return scale;
        }

        /// <summary>
        /// Measures every block at a fitted item font size.
        /// </summary>
        /// <param name="blocks">Blocks to update.</param>
        /// <param name="style">Style used for measuring.</param>
        /// <param name="measurer">Text measurer.</param>
        /// <param name="size">Item font size.</param>
        static void Measure(IReadOnlyList<VideoGraphicBlock> blocks, JsonElement style, IVideoTextMeasurer measurer, double size) {
            foreach (VideoGraphicBlock block in blocks) {
                VideoTextExtent extent = measurer.Measure(style, block.Text, size * block.FontScale);
                block.Width = extent.Width;
                block.Height = extent.Height;
            }
        }

        /// <summary>
        /// Width of the whole arrangement including the outline and shadow that extend past the glyph advances.
        /// </summary>
        /// <param name="blocks">Measured blocks.</param>
        /// <param name="layout">Direction and gap.</param>
        /// <param name="style">Style supplying outline and shadow.</param>
        /// <param name="size">Item font size.</param>
        /// <returns>Width in pixels.</returns>
        static double Width(IReadOnlyList<VideoGraphicBlock> blocks, GraphicTemplateLayoutAsset layout, JsonElement style, double size) {
            double content = layout.Direction == GraphicLayoutDirection.Horizontal ? blocks.Sum(block => block.Width) + layout.Gap * size * (blocks.Count - 1) : blocks.Max(block => block.Width);
            return content + 2 * Ink(style, size);
        }

        /// <summary>
        /// Height of the whole arrangement including outline and shadow.
        /// </summary>
        /// <param name="blocks">Measured blocks.</param>
        /// <param name="layout">Direction and gap.</param>
        /// <param name="style">Style supplying outline and shadow.</param>
        /// <param name="size">Item font size.</param>
        /// <returns>Height in pixels.</returns>
        static double Height(IReadOnlyList<VideoGraphicBlock> blocks, GraphicTemplateLayoutAsset layout, JsonElement style, double size) {
            double content = layout.Direction == GraphicLayoutDirection.Vertical ? blocks.Sum(block => block.Height) + layout.Gap * size * (blocks.Count - 1) : blocks.Max(block => block.Height);
            return content + 2 * Ink(style, size);
        }

        /// <summary>
        /// Extra ink around glyphs: half the outline plus the shadow offset, scaled with the font size.
        /// </summary>
        /// <param name="style">Style.</param>
        /// <param name="size">Fitted item font size.</param>
        /// <returns>Margin in pixels.</returns>
        static double Ink(JsonElement style, double size) {
            double ratio = size / VideoTextStyles.Number(style, "FontSize", VideoTextStyles.DefaultFontSize);
            return (VideoTextStyles.Number(style, "OutlineWidth", VideoTextStyles.DefaultOutlineWidth) / 2 + VideoTextStyles.Number(style, "ShadowOffset", VideoTextStyles.DefaultShadowOffset)) * ratio;
        }

        /// <summary>
        /// Centers the measured blocks in the safe area: horizontally on the area center, vertically on the preferred
        /// center moved as little as needed to stay inside the free band.
        /// </summary>
        /// <param name="blocks">Measured blocks to position.</param>
        /// <param name="layout">Chosen direction and gap.</param>
        /// <param name="style">Style supplying outline and shadow.</param>
        /// <param name="area">Safe area.</param>
        /// <param name="size">Fitted item font size.</param>
        static void Place(IReadOnlyList<VideoGraphicBlock> blocks, GraphicTemplateLayoutAsset layout, JsonElement style, VideoGraphicSafeArea area, double size) {
            double width = Width(blocks, layout, style, size), height = Height(blocks, layout, style, size), ink = Ink(style, size), gap = layout.Gap * size;
            double centerX = area.CenterX;
            double centerY = height >= area.Height ? (area.Top + area.Bottom) / 2 : Math.Clamp(area.CenterY, area.Top + height / 2, area.Bottom - height / 2);
            if (layout.Direction == GraphicLayoutDirection.Vertical) {
                double cursor = centerY - height / 2 + ink;
                foreach (VideoGraphicBlock block in blocks) {
                    block.CenterX = centerX;
                    block.CenterY = cursor + block.Height / 2;
                    cursor += block.Height + gap;
                }
            } else {
                double cursor = centerX - width / 2 + ink;
                foreach (VideoGraphicBlock block in blocks) {
                    block.CenterX = cursor + block.Width / 2;
                    block.CenterY = centerY;
                    cursor += block.Width + gap;
                }
            }
        }
    }
}
