namespace helengine {
    /// <summary>
    /// Property of a template element that a track animates.
    /// </summary>
    public enum GraphicAnimatedProperty {
        /// <summary>
        /// Element opacity from zero to one.
        /// </summary>
        Opacity = 0,

        /// <summary>
        /// Uniform scale around the element's own center, where one is the laid-out size.
        /// </summary>
        Scale = 1,

        /// <summary>
        /// Horizontal offset from the laid-out position, in ems of the element's font size.
        /// </summary>
        OffsetX = 2,

        /// <summary>
        /// Vertical offset from the laid-out position, in ems of the element's font size; positive moves down.
        /// </summary>
        OffsetY = 3,

        /// <summary>
        /// Left-to-right reveal of a bar from zero (hidden) to one (full width).
        /// </summary>
        Reveal = 4
    }
}
