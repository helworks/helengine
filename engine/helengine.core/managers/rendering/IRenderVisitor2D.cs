namespace helengine {
    /// <summary>
    /// Visits 2D drawables in render order.
    /// </summary>
    public interface IRenderVisitor2D {
        /// <summary>
        /// Processes a drawable encountered during traversal.
        /// </summary>
        /// <param name="drawable">Drawable to process.</param>
        void Visit(IDrawable2D drawable);
    }
}
