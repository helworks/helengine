namespace helengine {
    /// <summary>
    /// Describes a 2D drawable that renders a sprite.
    /// </summary>
    public interface ISpriteDrawable2D : IDrawable2D {
        /// <summary>
        /// Gets or sets the sprite texture.
        /// </summary>
        RuntimeTexture Texture { get; set; }

        /// <summary>
        /// Gets or sets the rotation applied to the sprite.
        /// </summary>
        float Rotation { get; set; }

        /// <summary>
        /// Gets or sets the sprite color.
        /// </summary>
        byte4 Color { get; set; }

        /// <summary>
        /// Gets or sets the sprite source rectangle.
        /// </summary>
        float4 SourceRect { get; set; }

        /// <summary>
        /// Gets or sets the sprite dimensions.
        /// </summary>
        int2 Size { get; set; }
    }
}
