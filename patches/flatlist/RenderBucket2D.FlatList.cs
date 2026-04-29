namespace helengine {
    /// <summary>
    /// Flat list of 2D drawables, preserved in render-order.
    /// </summary>
    public sealed class RenderBucket2D {
        /// <summary>
        /// Backing array of drawables.
        /// </summary>
        public IDrawable2D[] Items;

        /// <summary>
        /// Current number of entries.
        /// </summary>
        public int Count;

        /// <summary>
        /// Creates a list with an initial capacity.
        /// </summary>
        /// <param name="initialCapacity">Initial array size.</param>
        public RenderBucket2D(int initialCapacity = 32) {
            if (initialCapacity < 1) {
                initialCapacity = 1;
            }

            Items = new IDrawable2D[initialCapacity];
            Count = 0;
        }

        /// <summary>
        /// Clears all entries.
        /// </summary>
        public void Clear() {
            Count = 0;
        }

        /// <summary>
        /// Ensures the list can hold at least the desired number of items.
        /// </summary>
        /// <param name="desiredCount">Total number of items the list should hold.</param>
        public void EnsureCapacity(int desiredCount) {
            EnsureCapacity(desiredCount, false);
        }

        /// <summary>
        /// Ensures the list can hold at least the desired number of items.
        /// </summary>
        /// <param name="desiredCount">Total number of items the list should hold.</param>
        /// <param name="warnOnExpand">True to log a warning when the list expands.</param>
        public void EnsureCapacity(int desiredCount, bool warnOnExpand) {
            if (desiredCount <= Items.Length) {
                return;
            }

            int oldCap = Items.Length;
            int newCap = Items.Length;
            if (newCap < 1) {
                newCap = 1;
            }
            while (newCap < desiredCount) {
                newCap <<= 1;
            }

            Array.Resize(ref Items, newCap);
            if (warnOnExpand) {
                Logger.WriteWarning($"RenderList2D expanded from {oldCap} to {newCap}.");
            }
        }

        /// <summary>
        /// Adds a drawable in sorted order and returns its position.
        /// </summary>
        /// <param name="item">Drawable to add.</param>
        /// <param name="pos">Position assigned.</param>
        public void Add(IDrawable2D item, out int pos) {
            pos = InsertSorted(item);
        }

        /// <summary>
        /// Inserts a drawable in sorted render-order and returns its position.
        /// </summary>
        /// <param name="item">Drawable to insert.</param>
        /// <returns>Position assigned.</returns>
        public int InsertSorted(IDrawable2D item) {
            EnsureCapacity(Count + 1, true);

            byte order = item != null ? item.RenderOrder2D : (byte)0;
            int index = FindInsertIndex(order);

            if (index < Count) {
                Array.Copy(Items, index, Items, index + 1, Count - index);
            }

            Items[index] = item;
            Count++;
            return index;
        }

        /// <summary>
        /// Removes an item by swap-at and returns the swapped item if any.
        /// </summary>
        /// <param name="pos">Position to remove.</param>
        /// <returns>Swapped item or null if none.</returns>
        public IDrawable2D RemoveSwapAt(int pos) {
            int last = Count - 1;
            if (pos < 0 || pos > last) {
                return null;
            }

            IDrawable2D swapped = Items[last];
            if (pos != last) {
                Items[pos] = swapped;
            }
            Items[last] = default!;
            Count = last;
            return pos == last ? null : swapped;
        }

        /// <summary>
        /// Removes an item while preserving order of remaining entries.
        /// </summary>
        /// <param name="pos">Position to remove.</param>
        public void RemoveAt(int pos) {
            int last = Count - 1;
            if (pos < 0 || pos > last) {
                return;
            }

            if (pos < last) {
                Array.Copy(Items, pos + 1, Items, pos, last - pos);
            }
            Items[last] = default!;
            Count = last;
        }

        /// <summary>
        /// Finds the insertion index for a render order.
        /// </summary>
        /// <param name="renderOrder">Render order to insert.</param>
        /// <returns>Insertion index.</returns>
        int FindInsertIndex(byte renderOrder) {
            int low = 0;
            int high = Count;

            while (low < high) {
                int mid = (low + high) / 2;
                IDrawable2D midItem = Items[mid];
                byte midOrder = midItem != null ? midItem.RenderOrder2D : (byte)0;
                if (midOrder <= renderOrder) {
                    low = mid + 1;
                } else {
                    high = mid;
                }
            }

            return low;
        }
    }
}
