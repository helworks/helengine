namespace helengine {
    /// <summary>
    /// Describes a 2D drawable object.
    /// </summary>
    public interface IDrawable2D {
        /// <summary>
        /// Gets the parent entity that owns the drawable.
        /// </summary>
        Entity Parent { get; }

        /// <summary>
        /// Gets or sets the render order for 2D drawing.
        /// </summary>
        byte RenderOrder2D { get; set; }

        /// <summary>
        /// Draws the object using the active render manager.
        /// </summary>
        void Draw();
    }
}
