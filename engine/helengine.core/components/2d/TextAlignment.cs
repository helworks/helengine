namespace helengine {
    /// <summary>
    /// Defines how one text component positions glyphs horizontally inside its authored layout box.
    /// </summary>
    public enum TextAlignment {
        /// <summary>
        /// Places glyphs starting at the left edge of the authored layout box.
        /// </summary>
        Left,

        /// <summary>
        /// Centers glyphs horizontally inside the authored layout box.
        /// </summary>
        Center,

        /// <summary>
        /// Places glyphs so their visible right edge lands on the right edge of the authored layout box.
        /// </summary>
        Right
    }
}
