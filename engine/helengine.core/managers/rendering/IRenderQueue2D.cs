namespace helengine {
    /// <summary>
    /// Represents an ordered queue of 2D drawables for rendering.
    /// </summary>
    public interface IRenderQueue2D {
        /// <summary>
        /// Gets the number of drawables in the queue.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Adds a drawable to the queue.
        /// </summary>
        /// <param name="drawable">Drawable to add.</param>
        void Add(IDrawable2D drawable);

        /// <summary>
        /// Removes a drawable from the queue.
        /// </summary>
        /// <param name="drawable">Drawable to remove.</param>
        /// <returns>True when the drawable was removed.</returns>
        bool Remove(IDrawable2D drawable);

        /// <summary>
        /// Clears all drawables from the queue.
        /// </summary>
        void Clear();

        /// <summary>
        /// Ensures the queue can hold the desired number of items.
        /// </summary>
        /// <param name="desiredCount">Desired total count.</param>
        void EnsureCapacity(int desiredCount);

        /// <summary>
        /// Visits drawables in render order.
        /// </summary>
        /// <param name="visitor">Visitor that processes each drawable.</param>
        void VisitOrdered(IRenderVisitor2D visitor);
    }
}
