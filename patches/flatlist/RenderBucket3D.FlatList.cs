namespace helengine {
    /// <summary>
    /// Flat list of 3D drawables, preserved in render-order.
    /// </summary>
    public sealed class RenderBucket3D {
        /// <summary>
        /// Backing array of drawables.
        /// </summary>
        public IDrawable3D[] Items;

        /// <summary>
        /// Current number of entries.
        /// </summary>
        public int Count;

        /// <summary>
        /// Creates a list with an initial capacity.
        /// </summary>
        /// <param name="initialCapacity">Initial array size.</param>
        public RenderBucket3D(int initialCapacity = 32) {
            if (initialCapacity < 1) {
                initialCapacity = 1;
            }

            Items = new IDrawable3D[initialCapacity];
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
                Logger.WriteWarning($"RenderList3D expanded from {oldCap} to {newCap}.");
            }
        }

        /// <summary>
        /// Adds a drawable in sorted order and returns its position.
        /// </summary>
        /// <param name="item">Drawable to add.</param>
        /// <param name="pos">Position assigned.</param>
        public void Add(IDrawable3D item, out int pos) {
            pos = InsertSorted(item);
        }

        /// <summary>
        /// Inserts a drawable in sorted render-order and returns its position.
        /// </summary>
        /// <param name="item">Drawable to insert.</param>
        /// <returns>Position assigned.</returns>
        public int InsertSorted(IDrawable3D item) {
            EnsureCapacity(Count + 1, true);

            int index = FindInsertIndex(item);
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
        public IDrawable3D RemoveSwapAt(int pos) {
            int last = Count - 1;
            if (pos < 0 || pos > last) {
                return null;
            }

            IDrawable3D swapped = Items[last];
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
        /// Finds the insertion index for a drawable.
        /// </summary>
        /// <param name="item">Drawable to insert.</param>
        /// <returns>Insertion index.</returns>
        int FindInsertIndex(IDrawable3D item) {
            int low = 0;
            int high = Count;

            while (low < high) {
                int mid = (low + high) / 2;
                IDrawable3D midItem = Items[mid];
                int comparison = CompareDrawables(midItem, item);
                if (comparison <= 0) {
                    low = mid + 1;
                } else {
                    high = mid;
                }
            }

            return low;
        }

        /// <summary>
        /// Compares two drawables for render ordering.
        /// </summary>
        /// <param name="a">First drawable.</param>
        /// <param name="b">Second drawable.</param>
        /// <returns>Comparison result.</returns>
        int CompareDrawables(IDrawable3D a, IDrawable3D b) {
            if (a == null && b == null) {
                return 0;
            }

            if (a == null) {
                return -1;
            }

            if (b == null) {
                return 1;
            }

            int orderCompare = a.RenderOrder3D.CompareTo(b.RenderOrder3D);
            if (orderCompare != 0) {
                return orderCompare;
            }

            int variantCompare = a.Variant.CompareTo(b.Variant);
            if (variantCompare != 0) {
                return variantCompare;
            }

            return 0;
        }
    }
}
