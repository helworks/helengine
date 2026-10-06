namespace helengine.core.tests {
    /// <summary>Records the actual submission order emitted by a 3D render queue.</summary>
    public sealed class RecordingDrawableVisitor3D : IRenderVisitor3D {
        /// <summary>Drawables in the order received from the renderer queue.</summary>
        public List<IDrawable3D> Drawables { get; } = new List<IDrawable3D>();

        /// <summary>Appends one visited drawable without changing its scene state.</summary>
        /// <param name="drawable">Drawable supplied by the queue.</param>
        public void Visit(IDrawable3D drawable) {
            Drawables.Add(drawable);
        }
    }
}
