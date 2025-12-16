namespace helengine {
    /// <summary>
    /// Describes a 2D rounded rectangle drawable.
    /// </summary>
    public interface IRoundedRectDrawable2D : IDrawable2D {
        /// <summary>
        /// Gets or sets the dimensions of the rectangle.
        /// </summary>
        int2 Size { get; set; }

        /// <summary>
        /// Gets or sets the radius of the rounded corners.
        /// </summary>
        byte CornerRadius { get; set; }

        /// <summary>
        /// Gets or sets the fill color.
        /// </summary>
        byte4 Color { get; set; }
    }
}
