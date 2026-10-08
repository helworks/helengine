namespace helengine {
    /// <summary>
    /// Direction in which a graphic template stacks its items and separators.
    /// </summary>
    public enum GraphicLayoutDirection {
        /// <summary>
        /// One item per line, centered horizontally; the natural choice for tall 9:16 frames and long terms.
        /// </summary>
        Vertical = 0,

        /// <summary>
        /// Every item on one centered line, used when the measured row fits the safe area.
        /// </summary>
        Horizontal = 1
    }
}
