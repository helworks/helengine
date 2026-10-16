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
        /// Draws the object using the active render manager.
        /// </summary>
        void Draw();
    }
}
