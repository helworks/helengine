namespace helengine {
    /// <summary>Applies resolved factors directly, without allocating or retaining original UI values.</summary>
    public static class ViewportLayoutApplier {
        /// <summary>Transforms one local position; callers use a zero origin for non-root entities.</summary>
        /// <param name="authoredPosition">Position before layout processing.</param>
        /// <param name="metrics">Resolved factors and optional root displacement.</param>
        /// <returns>Local position in prepared layout space.</returns>
        public static float3 ResolvePosition(float3 authoredPosition, ViewportLayoutMetrics metrics) {
            return new float3((float)(authoredPosition.X * metrics.ScaleX + metrics.Origin.X),
                (float)(authoredPosition.Y * metrics.ScaleY + metrics.Origin.Y), authoredPosition.Z);
        }

        /// <summary>Scales the fields owned by one real participant, including detached cook participants.</summary>
        /// <param name="component">Live or detached UI component.</param>
        /// <param name="metrics">Shared layout factors.</param>
        public static void ApplyComponent(Component component, ViewportLayoutMetrics metrics) {
            if (component is LayoutComponent anchors) {
                float4 distances = anchors.AnchorDistances;
                anchors.AnchorDistances = new float4((float)(distances.X * metrics.ScaleX), (float)(distances.Y * metrics.ScaleX),
                    (float)(distances.Z * metrics.ScaleY), (float)(distances.W * metrics.ScaleY));
            } else if (component is RoundedRectComponent rectangle) {
                rectangle.Size = ScaleSize(rectangle.Size, metrics.ScaleX, metrics.ScaleY);
                rectangle.Radius = (float)(rectangle.Radius * metrics.GlyphScale);
                rectangle.BorderThickness = (float)(rectangle.BorderThickness * metrics.GlyphScale);
            } else if (component is TextComponent text) {
                text.Size = ScaleSize(text.Size, metrics.ScaleX, metrics.ScaleY);
                text.FontScale = text.NativeFontPixels == TextNativePixelMode.Disabled ? (float)(text.FontScale * metrics.GlyphScale) : 1f;
                float outline = (float)(text.OutlineScale * metrics.GlyphScale);
                float2 shadow = new float2((float)(text.ShadowOffset.X * metrics.GlyphScale), (float)(text.ShadowOffset.Y * metrics.GlyphScale));
                if (text.NativeFontPixels != TextNativePixelMode.Disabled) {
                    outline = TextNativePixelEffectUtils.SnapOutlineScale(outline, text.NativeFontPixels);
                    shadow = TextNativePixelEffectUtils.SnapShadowOffset(shadow, text.NativeFontPixels);
                }
                text.OutlineScale = outline;
                text.ShadowOffset = shadow;
            } else if (component is SpriteComponent sprite) {
                sprite.Size = ScaleSize(sprite.Size, metrics.ScaleX, metrics.ScaleY);
            } else if (component is ClipRectComponent clip) {
                clip.Size = ScaleSize(clip.Size, metrics.ScaleX, metrics.ScaleY);
            } else if (component is InteractableComponent interactable) {
                interactable.Size = ScaleSize(interactable.Size, metrics.ScaleX, metrics.ScaleY);
            } else if (component is ScrollComponent scroll) {
                scroll.Size = ScaleSize(scroll.Size, metrics.ScaleX, metrics.ScaleY);
                scroll.ItemExtent = ScaleInteger(scroll.ItemExtent, metrics.GlyphScale);
            } else if (component is FPSComponent fps) {
                fps.FontScale = fps.NativeFontPixels == TextNativePixelMode.Disabled ? (float)(fps.FontScale * metrics.GlyphScale) : 1f;
                fps.Padding = ScaleSize(fps.Padding, metrics.ScaleX, metrics.ScaleY);
            }
        }

        /// <summary>Preserves zero and the established minimum-one rule for nonzero authored dimensions.</summary>
        /// <param name="value">Authored dimension.</param>
        /// <param name="scale">Resolved multiplier.</param>
        /// <returns>Prepared integer dimension.</returns>
        public static int ScaleInteger(int value, double scale) {
            return value == 0 ? 0 : Math.Max(1, (int)Math.Round(value * scale));
        }

        /// <summary>Applies independent axis factors using existing integer rounding.</summary>
        /// <param name="size">Authored component size.</param>
        /// <param name="scaleX">Horizontal multiplier.</param>
        /// <param name="scaleY">Vertical multiplier.</param>
        /// <returns>Prepared size.</returns>
        public static int2 ScaleSize(int2 size, double scaleX, double scaleY) {
            return new int2(ScaleInteger(size.X, scaleX), ScaleInteger(size.Y, scaleY));
        }
    }
}
