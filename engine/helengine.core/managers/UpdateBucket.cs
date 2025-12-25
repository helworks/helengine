namespace helengine {
    /// <summary>
    /// Stores updateable items in a dense array for fast iteration and swap removal.
    /// </summary>
    public sealed class UpdateBucket {
        /// <summary>
        /// Backing array of updateable items.
        /// </summary>
        public IUpdateable[] Items;

        /// <summary>
        /// Current number of items in the bucket.
        /// </summary>
        public int Count;

        /// <summary>
        /// Creates a bucket with an initial capacity.
        /// </summary>
        /// <param name="initialCapacity">Starting capacity for the bucket.</param>
        public UpdateBucket(int initialCapacity = 32) {
            if (initialCapacity < 1) {
                initialCapacity = 1;
            }

            Items = new IUpdateable[initialCapacity];
            Count = 0;
        }

        /// <summary>
        /// Ensures the bucket can hold at least the desired number of items.
        /// </summary>
        /// <param name="desiredCount">Total number of items the bucket should hold.</param>
        public void EnsureCapacity(int desiredCount) {
            EnsureCapacity(desiredCount, false);
        }

        /// <summary>
        /// Ensures the bucket can hold at least the desired number of items.
        /// </summary>
        /// <param name="desiredCount">Total number of items the bucket should hold.</param>
        /// <param name="warnOnExpand">True to log a warning when the bucket expands.</param>
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
                Logger.WriteWarning($"UpdateBucket expanded from {oldCap} to {newCap}.");
            }
        }

        /// <summary>
        /// Adds an updateable to the bucket.
        /// </summary>
        /// <param name="item">Updateable to add.</param>
        public void Add(IUpdateable item) {
            EnsureCapacity(Count + 1, true);
            Items[Count++] = item;
        }

        /// <summary>
        /// Removes an item by swapping in the last item.
        /// </summary>
        /// <param name="pos">Position to remove.</param>
        /// <returns>Swapped item or null when no swap occurred.</returns>
        public IUpdateable RemoveSwapAt(int pos) {
            int last = Count - 1;
            if (pos < 0 || pos > last) {
                return null;
            }

            IUpdateable swapped = Items[last];
            if (pos != last) {
                Items[pos] = swapped;
            }
            Items[last] = default!;
            Count = last;
            return pos == last ? null : swapped;
        }

        /// <summary>
        /// Removes the specified updateable from the bucket.
        /// </summary>
        /// <param name="item">Updateable to remove.</param>
        /// <returns>True when the item was removed.</returns>
        public bool Remove(IUpdateable item) {
            int pos = IndexOf(item);
            if (pos < 0) {
                return false;
            }

            RemoveSwapAt(pos);
            return true;
        }

        /// <summary>
        /// Finds the index of an updateable in the bucket.
        /// </summary>
        /// <param name="item">Updateable to locate.</param>
        /// <returns>Index of the item or -1 when not found.</returns>
        public int IndexOf(IUpdateable item) {
            if (item == null) {
                return -1;
            }

            for (int i = 0; i < Count; i++) {
                if (ReferenceEquals(Items[i], item)) {
                    return i;
                }
            }

            return -1;
        }
    }
}
