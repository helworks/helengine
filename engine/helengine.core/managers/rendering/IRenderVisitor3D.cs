namespace helengine {
    /// <summary>
    /// Visits 3D drawables in render order.
    /// </summary>
    public interface IRenderVisitor3D {
        /// <summary>
        /// Processes a drawable encountered during traversal.
        /// </summary>
        /// <param name="drawable">Drawable to process.</param>
        void Visit(IDrawable3D drawable);
    }
}
