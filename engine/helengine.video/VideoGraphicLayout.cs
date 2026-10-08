using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Lays out the blocks of one graphic with measured text: every supported direction is fitted into the safe area
    /// with one uniform scale (re-measured at the scaled size, since glyph advances are not perfectly linear), then the
    /// direction is chosen — horizontal when it fits at full size, otherwise whichever direction keeps the text largest,
    /// preferring vertical on ties — and the blocks are centered in the safe area.
    /// </summary>
    public static class VideoGraphicLayout {
        /// <summary>
        /// Most fitting passes per direction.
        /// </summary>
        const int FitPasses = 6;

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
            if (horizontal != null && Fit(blocks, horizontal, style, measurer, area, baseSize) >= 1) {
                chosen = horizontal;
                chosenScale = 1;
            } else {
                foreach (GraphicTemplateLayoutAsset candidate in candidates.OrderBy(layout => layout.Direction == GraphicLayoutDirection.Vertical ? 0 : 1)) {
                    double scale = Fit(blocks, candidate, style, measurer, area, baseSize);
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
        /// Finds the largest uniform scale, at most one, at which the blocks fit the safe area in one direction.
        /// </summary>
        /// <param name="blocks">Blocks.</param>
        /// <param name="layout">Direction and gap.</param>
        /// <param name="style">Style used for measuring.</param>
        /// <param name="measurer">Text measurer.</param>
        /// <param name="area">Safe area.</param>
        /// <param name="baseSize">Item font size before fitting.</param>
        /// <returns>Fit scale.</returns>
        static double Fit(IReadOnlyList<VideoGraphicBlock> blocks, GraphicTemplateLayoutAsset layout, JsonElement style, IVideoTextMeasurer measurer, VideoGraphicSafeArea area, double baseSize) {
            double scale = 1;
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
        /// Centers the measured blocks in the safe area: horizontally on the frame center, vertically on the preferred
        /// center moved as little as needed to stay inside the free band.
        /// </summary>
        /// <param name="blocks">Measured blocks to position.</param>
        /// <param name="layout">Chosen direction and gap.</param>
        /// <param name="style">Style supplying outline and shadow.</param>
        /// <param name="area">Safe area.</param>
        /// <param name="size">Fitted item font size.</param>
        static void Place(IReadOnlyList<VideoGraphicBlock> blocks, GraphicTemplateLayoutAsset layout, JsonElement style, VideoGraphicSafeArea area, double size) {
            double width = Width(blocks, layout, style, size), height = Height(blocks, layout, style, size), ink = Ink(style, size), gap = layout.Gap * size;
            double centerX = area.FrameWidth / 2;
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
