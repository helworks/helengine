namespace DemoDisc.menu {
    /// <summary>Fits the two authored headings and anchors their painted right edges to their shared overlay parent.</summary>
    internal static class DemoDiscTitleLayout {
        /// <summary>Reserves one framebuffer pixel at the right edge for rasterization.</summary>
        const double RightPixelMargin = 1d;

        /// <summary>Ignores float-rounding differences smaller than a ten-thousandth of a framebuffer pixel.</summary>
        const double WidthTolerance = 0.0001d;

        /// <summary>Finds a uniquely named heading once during menu binding without taking ownership of its component.</summary>
        /// <param name="entity">Loaded hierarchy root whose components remain owned by their entities.</param>
        /// <param name="content">Exact, case-sensitive authored heading text.</param>
        /// <returns>The borrowed matching text component, or null when it is absent.</returns>
        [NativeBorrowedReturn]
        public static TextComponent Find(Entity entity, string content) {
            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is TextComponent text && text.Text == content) {
                    return text;
                }
            }
            for (int index = 0; index < entity.Children.Count; index++) {
                TextComponent text = Find(entity.Children[index], content);
                if (text != null) {
                    return text;
                }
            }
            return null;
        }

        /// <summary>Reduces an overflowing heading, then aligns its visible glyphs and right effects one pixel inside the shared right anchor.</summary>
        /// <param name="text">Borrowed heading component; menu binding passes only HELENGINE and DEMO DISC.</param>
        public static void Fit(TextComponent text) {
            if (text == null || text.Font == null || text.FontScale <= 0f || text.Size.X <= RightPixelMargin) {
                return;
            }
            double width = TextLayoutAlignmentUtils.MeasureVisibleLineWidth(text.Text, text.Font,
                text.FontScale, text.Font.AtlasWidth);
            double rightEffect = Math.Max((double)text.OutlineScale, Math.Max(0d, (double)text.ShadowOffset.X));
            double drawnWidth = width + rightEffect;
            double availableWidth = text.Size.X - RightPixelMargin;
            if (drawnWidth > availableWidth + WidthTolerance) {
                float ratio = (float)(availableWidth / drawnWidth);
                text.FontScale = text.FontScale * ratio;
                text.OutlineScale = text.OutlineScale * ratio;
                text.ShadowOffset = new float2(text.ShadowOffset.X * ratio, text.ShadowOffset.Y * ratio);
            }
            AlignRight(text);
        }

        /// <summary>Uses one reducing ratio for both authored headings so their matching styles retain identical painted right margins after raster snapping.</summary>
        /// <param name="first">Borrowed HELENGINE heading, with the same authored font scale and right effects as the second heading.</param>
        /// <param name="second">Borrowed DEMO DISC heading sharing the first heading's right-anchor parent.</param>
        public static void FitPair(TextComponent first, TextComponent second) {
            if (first == null || second == null || first.Font == null || second.Font == null
                || first.FontScale <= 0f || second.FontScale <= 0f
                || first.Size.X <= RightPixelMargin || second.Size.X <= RightPixelMargin) {
                return;
            }
            float ratio = (float)Math.Min(MeasureFitRatio(first), MeasureFitRatio(second));
            if (ratio < 1f) {
                ScaleStyle(first, ratio);
                ScaleStyle(second, ratio);
            }
            AlignRight(first);
            AlignRight(second);
        }

        /// <summary>Returns a reducing ratio only when visible glyphs and right effects exceed the existing box margin.</summary>
        /// <param name="text">Validated borrowed heading with available font metrics and a positive text box.</param>
        /// <returns>One when no reduction is needed, otherwise the ratio required to fit inside the box.</returns>
        static double MeasureFitRatio(TextComponent text) {
            double width = TextLayoutAlignmentUtils.MeasureVisibleLineWidth(text.Text, text.Font, text.FontScale, text.Font.AtlasWidth);
            double rightEffect = Math.Max((double)text.OutlineScale, Math.Max(0d, (double)text.ShadowOffset.X));
            double drawnWidth = width + rightEffect;
            double availableWidth = text.Size.X - RightPixelMargin;
            if (drawnWidth <= availableWidth + WidthTolerance) {
                return 1d;
            }
            return availableWidth / drawnWidth;
        }

        /// <summary>Preserves one heading's font, outline and both shadow proportions while applying the shared reducing ratio.</summary>
        /// <param name="text">Borrowed heading whose owning entity remains responsible for its lifetime.</param>
        /// <param name="ratio">The common ratio, strictly below one, required by the wider of the two headings.</param>
        static void ScaleStyle(TextComponent text, float ratio) {
            text.FontScale = text.FontScale * ratio;
            text.OutlineScale = text.OutlineScale * ratio;
            text.ShadowOffset = new float2(text.ShadowOffset.X * ratio, text.ShadowOffset.Y * ratio);
        }

        /// <summary>Derives an absolute local X from the current viewport-scaled box instead of accumulating position adjustments.</summary>
        /// <param name="text">Borrowed heading whose entity is a child of the authored right-anchored platform-info overlay.</param>
        static void AlignRight(TextComponent text) {
            Entity entity = text.Parent;
            if (entity == null || entity.Parent == null) {
                throw new InvalidOperationException("DemoDisc headings require their authored shared right-anchor parent.");
            }
            double rightEffect = Math.Max((double)text.OutlineScale, Math.Max(0d, (double)text.ShadowOffset.X));
            float rightAlignedX = (float)(-text.Size.X - rightEffect - RightPixelMargin);
            float3 localPosition = entity.LocalPosition;
            if (Math.Abs((double)localPosition.X - rightAlignedX) > WidthTolerance) {
                entity.LocalPosition = new float3(rightAlignedX, localPosition.Y, localPosition.Z);
            }
            if (text.Alignment != TextAlignment.Right) {
                text.Alignment = TextAlignment.Right;
            }
        }
    }
}
