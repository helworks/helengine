namespace helengine {
    /// <summary>
    /// Describes a 2D drawable that renders a sprite.
    /// </summary>
    public interface ISpriteDrawable2D : IDrawable2D {
        /// <summary>
        /// Gets or sets the sprite color.
        /// </summary>
        byte4 Color { get; set; }
    }
}
