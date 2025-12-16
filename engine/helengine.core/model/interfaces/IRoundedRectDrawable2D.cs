namespace helengine {
    /// <summary>
    /// Describes a 2D rounded rectangle drawable.
    /// </summary>
    public interface IRoundedRectDrawable2D : IDrawable2D {
        /// <summary>
        /// Gets or sets the fill color applied to the rounded rectangle.
        /// </summary>
        byte4 FillColor { get; set; }

        /// <summary>
        /// Gets or sets the border color applied to the rounded rectangle.
        /// </summary>
        byte4 BorderColor { get; set; }

        /// <summary>
        /// Gets or sets the dimensions of the rectangle.
        /// </summary>
        int2 Size { get; set; }

        /// <summary>
        /// Gets or sets the radius of the rounded corners.
        /// </summary>
        float Radius { get; set; }

        /// <summary>
        /// Gets or sets the outline thickness.
        /// </summary>
        float BorderThickness { get; set; }

        /// <summary>
        /// Gets or sets the texture color modulation.
        /// </summary>
        byte4 Color { get; set; }

        /// <summary>
        /// Gets or sets the texture source rectangle.
        /// </summary>
        float4 SourceRect { get; set; }

        /// <summary>
        /// Gets or sets the rotation applied to the rectangle.
        /// </summary>
        float Rotation { get; set; }
    }
}
