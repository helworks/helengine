namespace helengine {
    /// <summary>
    /// Snaps viewport-scaled text effect offsets to whole target pixels for text that renders its cooked glyphs at native size.
    /// </summary>
    /// <remarks>
    /// A native-pixel glyph lands on whole pixels, but its outline and shadow copies were scaled by the reference canvas.
    /// On a 640x448 target with a 1280x720 canvas an authored outline of 2 becomes 1.244 pixels, and rasterizers that
    /// quantize sub-pixel positions (the PS2 GS rounds to 1/16 and covers by ceiling) then draw the left and top copies
    /// one pixel away and the right and bottom copies two pixels away, so the outline looks lopsided. Whole-pixel
    /// offsets keep every copy symmetric around the glyph.
    /// </remarks>
    public static class TextNativePixelEffectUtils {
        /// <summary>
        /// Snaps a scaled outline radius to whole pixels, keeping any enabled outline at least one pixel wide.
        /// </summary>
        /// <param name="scaledOutlineScale">Outline radius after viewport scaling; zero or less means the outline is disabled.</param>
        /// <param name="mode">Native-pixel mode whose rounding rule applies; must not be disabled.</param>
        /// <returns>The whole-pixel outline radius, or the original value when the outline is disabled.</returns>
        public static float SnapOutlineScale(float scaledOutlineScale, TextNativePixelMode mode) {
            if (scaledOutlineScale <= 0f) {
                return scaledOutlineScale;
            }

            return (float)SnapMagnitude(scaledOutlineScale, mode);
        }

        /// <summary>
        /// Snaps a scaled shadow offset to whole pixels per axis, keeping every nonzero axis at least one pixel away.
        /// </summary>
        /// <param name="scaledShadowOffset">Shadow offset after viewport scaling.</param>
        /// <param name="mode">Native-pixel mode whose rounding rule applies; must not be disabled.</param>
        /// <returns>The whole-pixel shadow offset with each axis keeping its authored direction.</returns>
        public static float2 SnapShadowOffset(float2 scaledShadowOffset, TextNativePixelMode mode) {
            return new float2(SnapShadowAxis(scaledShadowOffset.X, mode), SnapShadowAxis(scaledShadowOffset.Y, mode));
        }

        /// <summary>
        /// Snaps one shadow axis to whole pixels without collapsing a nonzero offset onto the glyph.
        /// </summary>
        /// <param name="scaledAxis">One scaled shadow axis.</param>
        /// <param name="mode">Native-pixel mode whose rounding rule applies.</param>
        /// <returns>Zero for a zero axis, otherwise a signed whole-pixel distance of at least one.</returns>
        static float SnapShadowAxis(float scaledAxis, TextNativePixelMode mode) {
            if (scaledAxis == 0f) {
                return 0f;
            }

            return (float)(Math.Sign(scaledAxis) * SnapMagnitude(Math.Abs((double)scaledAxis), mode));
        }

        /// <summary>
        /// Rounds a positive pixel distance with the mode's rule, clamping the result to at least one pixel.
        /// </summary>
        /// <param name="magnitude">Positive scaled distance in target pixels.</param>
        /// <param name="mode">Native-pixel mode whose rounding rule applies.</param>
        /// <returns>The whole-pixel distance, never below one.</returns>
        static double SnapMagnitude(double magnitude, TextNativePixelMode mode) {
            double snapped;
            if (mode == TextNativePixelMode.RoundNearest) {
                snapped = Math.Round(magnitude, MidpointRounding.AwayFromZero);
            } else if (mode == TextNativePixelMode.RoundDown) {
                snapped = Math.Floor(magnitude);
            } else if (mode == TextNativePixelMode.RoundUp) {
                snapped = Math.Ceiling(magnitude);
            } else {
                throw new ArgumentOutOfRangeException(nameof(mode), "Effect snapping requires a native-pixel mode.");
            }

            return Math.Max(1d, snapped);
        }
    }
}
