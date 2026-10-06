namespace helengine {
    /// <summary>
    /// Ordered list of 2D drawables used for rendering.
    /// </summary>
    public sealed class RenderList2D : IRenderQueue2D, IDisposable {
        /// <summary>
        /// Backing list of drawables.
        /// </summary>
        readonly List<IDrawable2D> Items;

        /// <summary>
        /// Monotonically increasing revision used by render backends to invalidate cached queue output.
        /// </summary>
        int VersionValue;

        /// <summary>
        /// Initializes a new render list with the specified capacity.
        /// </summary>
        /// <param name="initialCapacity">Initial list capacity.</param>
        public RenderList2D(int initialCapacity) {
            if (initialCapacity < 0) {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            Items = new List<IDrawable2D>(initialCapacity);
            VersionValue = 1;
        }

        /// <summary>
        /// Gets the number of drawables in the list.
        /// </summary>
        public int Count { get { return Items.Count; } }

        /// <summary>
        /// Gets the current backing-list capacity reserved by the queue.
        /// </summary>
        public int Capacity { get { return Items.Capacity; } }

        /// <summary>
        /// Gets the current membership and ordering revision of this render queue.
        /// </summary>
        public int Version { get { SortByCurrentDepth(); return VersionValue; } }

        /// <summary>
        /// Gets the drawable at the specified index.
        /// </summary>
        /// <param name="index">Zero-based index.</param>
        public IDrawable2D this[int index] { get { SortByCurrentDepth(); return Items[index]; } }

        /// <summary>Reads an item after the caller has refreshed ordering through Version, avoiding repeated sorts in a batch.</summary>
        /// <param name="index">Index in the already-prepared queue.</param>
        /// <returns>Drawable at that ordered index.</returns>
        internal IDrawable2D GetPreparedDrawable(int index) {
            return Items[index];
        }

        /// <summary>
        /// Adds a drawable and defers depth ordering until the queue is consumed.
        /// </summary>
        /// <param name="drawable">Drawable to add.</param>
        public void Add(IDrawable2D drawable) {
            if (ContainsReference(drawable)) {
                return;
            }

            Items.Add(drawable);
            AdvanceVersion();
        }

        /// <summary>
        /// Removes every occurrence of a drawable by reference so duplicate queue entries cannot survive one unregister request.
        /// </summary>
        /// <param name="drawable">Drawable to remove.</param>
        /// <returns>True if the drawable was removed.</returns>
        public bool Remove(IDrawable2D drawable) {
            bool removed = false;
            for (int index = Items.Count - 1; index >= 0; index--) {
                if (!ReferenceEquals(Items[index], drawable)) {
                    continue;
                }

                Items.RemoveAt(index);
                removed = true;
            }

            if (removed) {
                AdvanceVersion();
            }

            return removed;
        }

        /// <summary>
        /// Removes all drawables from the list.
        /// </summary>
        public void Clear() {
            if (Items.Count <= 0) {
                return;
            }

            Items.Clear();
            AdvanceVersion();
        }

        /// <summary>
        /// Releases the native backing list used by this per-camera render queue.
        /// </summary>
        public void Dispose() {
            Items.Clear();
            NativeOwnership.Delete(Items);
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
            if (desiredCount <= Items.Capacity) {
                return;
            }

            int oldCap = Items.Capacity;
            Items.Capacity = desiredCount;
            if (warnOnExpand) {
                Logger.WriteWarning($"RenderList2D expanded from {oldCap} to {Items.Capacity}.");
            }
        }

        /// <summary>
        /// Visits drawables in render order.
        /// </summary>
        /// <param name="visitor">Visitor that processes each drawable.</param>
        public void VisitOrdered(IRenderVisitor2D visitor) {
            if (visitor == null) {
                throw new ArgumentNullException(nameof(visitor));
            }

            SortByCurrentDepth();
            for (int i = 0; i < Items.Count; i++) {
                visitor.Visit(Items[i]);
            }
        }

        /// <summary>
        /// Refreshes stable depth/hierarchy order before consumption, including transform-only changes.
        /// </summary>
        void SortByCurrentDepth() {
            bool changed = false;
            for (int index = 1; index < Items.Count; index++) {
                IDrawable2D candidate = Items[index];
                int insertionIndex = index;
                while (insertionIndex > 0 && RenderDepthOrder2D.CompareDrawables(candidate, Items[insertionIndex - 1]) < 0) {
                    Items[insertionIndex] = Items[insertionIndex - 1];
                    insertionIndex--;
                }
                if (insertionIndex != index) {
                    Items[insertionIndex] = candidate;
                    changed = true;
                }
            }
            if (changed) {
                AdvanceVersion();
            }
        }

        /// <summary>
        /// Determines whether the queue already contains the provided drawable reference.
        /// </summary>
        /// <param name="drawable">Drawable to locate.</param>
        /// <returns>True when the exact drawable instance is already queued; otherwise false.</returns>
        bool ContainsReference(IDrawable2D drawable) {
            for (int index = 0; index < Items.Count; index++) {
                if (ReferenceEquals(Items[index], drawable)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Advances the queue revision while retaining a positive value after integer rollover.
        /// </summary>
        void AdvanceVersion() {
            if (VersionValue == int.MaxValue) {
                VersionValue = 1;
                return;
            }

            VersionValue++;
        }

    }
}
