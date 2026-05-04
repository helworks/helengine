namespace helengine {
    /// <summary>
    /// Describes which corners of a rounded rectangle remain rounded.
    /// </summary>
    [Flags]
    public enum RoundedRectCorners {
        /// <summary>
        /// No corners are rounded.
        /// </summary>
        None = 0,

        /// <summary>
        /// The top-left corner is rounded.
        /// </summary>
        TopLeft = 1,

        /// <summary>
        /// The top-right corner is rounded.
        /// </summary>
        TopRight = 2,

        /// <summary>
        /// The bottom-left corner is rounded.
        /// </summary>
        BottomLeft = 4,

        /// <summary>
        /// The bottom-right corner is rounded.
        /// </summary>
        BottomRight = 8,

        /// <summary>
        /// All corners are rounded.
        /// </summary>
        All = TopLeft | TopRight | BottomLeft | BottomRight
    }
}
