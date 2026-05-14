namespace helengine {
    /// <summary>
    /// Ordered list of 3D drawables used for rendering.
    /// </summary>
    public sealed class RenderList3D : IRenderQueue3D {
        /// <summary>
        /// Backing list of drawables.
        /// </summary>
        readonly List<IDrawable3D> items;

        /// <summary>
        /// Initializes a new render list with the specified capacity.
        /// </summary>
        /// <param name="initialCapacity">Initial list capacity.</param>
        public RenderList3D(int initialCapacity) {
            if (initialCapacity < 0) {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            items = new List<IDrawable3D>(initialCapacity);
        }

        /// <summary>
        /// Gets the number of drawables in the list.
        /// </summary>
        public int Count { get { return items.Count; } }

        /// <summary>
        /// Gets the current backing-list capacity reserved by the queue.
        /// </summary>
        public int Capacity { get { return items.Capacity; } }

        /// <summary>
        /// Gets the drawable at the specified index.
        /// </summary>
        /// <param name="index">Zero-based index.</param>
        public IDrawable3D this[int index] { get { return items[index]; } }

        /// <summary>
        /// Adds a drawable while keeping the list ordered by render order.
        /// </summary>
        /// <param name="drawable">Drawable to add.</param>
        public void Add(IDrawable3D drawable) {
            int insertIndex = FindInsertIndex(drawable != null ? drawable.RenderOrder3D : (byte)0);
            items.Insert(insertIndex, drawable);
        }

        /// <summary>
        /// Removes the first occurrence of a drawable by reference.
        /// </summary>
        /// <param name="drawable">Drawable to remove.</param>
        /// <returns>True if the drawable was removed.</returns>
        public bool Remove(IDrawable3D drawable) {
            int index = FindIndexByReference(drawable);
            if (index < 0) {
                return false;
            }

            items.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Removes all drawables from the list.
        /// </summary>
        public void Clear() {
            items.Clear();
        }

        /// <summary>
        /// Ensures the list can hold the desired number of entries.
        /// </summary>
        /// <param name="desiredCount">Desired total count.</param>
        public void EnsureCapacity(int desiredCount) {
            EnsureCapacity(desiredCount, false);
        }

        /// <summary>
        /// Ensures the list can hold the desired number of entries.
        /// </summary>
        /// <param name="desiredCount">Desired total count.</param>
        /// <param name="warnOnExpand">True to log when capacity grows.</param>
        public void EnsureCapacity(int desiredCount, bool warnOnExpand) {
            if (desiredCount <= items.Capacity) {
                return;
            }

            int oldCap = items.Capacity;
            items.Capacity = desiredCount;
            if (warnOnExpand) {
                Logger.WriteWarning($"RenderList3D expanded from {oldCap} to {items.Capacity}.");
            }
        }

        /// <summary>
        /// Visits drawables in render order.
        /// </summary>
        /// <param name="visitor">Visitor that processes each drawable.</param>
        public void VisitOrdered(IRenderVisitor3D visitor) {
            if (visitor == null) {
                throw new ArgumentNullException(nameof(visitor));
            }

            for (int i = 0; i < items.Count; i++) {
                visitor.Visit(items[i]);
            }
        }

        /// <summary>
        /// Finds the index where the given render order should be inserted.
        /// </summary>
        /// <param name="renderOrder">Render order to insert.</param>
        /// <returns>Insertion index.</returns>
        int FindInsertIndex(byte renderOrder) {
            for (int i = 0; i < items.Count; i++) {
                IDrawable3D current = items[i];
                byte currentOrder = current != null ? current.RenderOrder3D : (byte)0;
                if (renderOrder < currentOrder) {
                    return i;
                }
            }

            return items.Count;
        }

        /// <summary>
        /// Finds the index of a drawable using reference equality.
        /// </summary>
        /// <param name="drawable">Drawable to locate.</param>
        /// <returns>Index or -1 when not found.</returns>
        int FindIndexByReference(IDrawable3D drawable) {
            for (int i = 0; i < items.Count; i++) {
                if (ReferenceEquals(items[i], drawable)) {
                    return i;
                }
            }

            return -1;
        }
    }
}
