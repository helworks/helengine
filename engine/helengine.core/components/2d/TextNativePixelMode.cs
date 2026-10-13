namespace helengine {
    /// <summary>
    /// Chooses whether a text component draws its cooked font at native pixel size and, when it does, how viewport-scaled
    /// outline and shadow offsets snap to whole target pixels. Stored in one byte so it costs no more than the boolean it replaced.
    /// </summary>
    public enum TextNativePixelMode : byte {
        /// <summary>
        /// Glyphs scale with the reference canvas and outline or shadow offsets keep their fractional scaled values.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Glyphs render at native size and effect offsets round to the nearest whole pixel, never below one.
        /// </summary>
        RoundNearest = 1,

        /// <summary>
        /// Glyphs render at native size and effect offsets round down to a whole pixel, never below one.
        /// </summary>
        RoundDown = 2,

        /// <summary>
        /// Glyphs render at native size and effect offsets round up to the next whole pixel.
        /// </summary>
        RoundUp = 3
    }
}
