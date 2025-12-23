namespace helengine {
    /// <summary>
    /// Index mapping for 2D render buckets.
    /// </summary>
    public struct Index2D {
        /// <summary>
        /// Bucket index.
        /// </summary>
        public int Bucket;

        /// <summary>
        /// Position inside the bucket.
        /// </summary>
        public int Pos;

        /// <summary>
        /// Initializes a new 2D index.
        /// </summary>
        public Index2D(int bucket, int pos) { Bucket = bucket; Pos = pos; }
    }

    /// <summary>
    /// Dense array bucket for 2D drawables.
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
        /// Creates a bucket with an initial capacity.
        /// </summary>
        public RenderBucket2D(int initialCapacity = 32) {
            if (initialCapacity < 1) initialCapacity = 1;
            Items = new IDrawable2D[initialCapacity];
            Count = 0;
        }

        /// <summary>
        /// Clears all entries.
        /// </summary>
        public void Clear() { Count = 0; }

        /// <summary>
        /// Adds a drawable and returns its position.
        /// </summary>
        /// <param name="item">Drawable to add.</param>
        /// <param name="pos">Position assigned.</param>
        public void Add(IDrawable2D item, out int pos) {
            if (Count == Items.Length) {
                int newCap = Items.Length << 1;
                Array.Resize(ref Items, newCap);
            }
            pos = Count;
            Items[Count++] = item;
        }

        /// <summary>
        /// Removes an item by swap-at and returns the swapped item if any.
        /// </summary>
        /// <param name="pos">Position to remove.</param>
        /// <returns>Swapped item or null if none.</returns>
        public IDrawable2D RemoveSwapAt(int pos) {
            int last = Count - 1;
            if (pos < 0 || pos > last) return null;
            IDrawable2D swapped = Items[last];
            if (pos != last) {
                Items[pos] = swapped;
            }
            Items[last] = default!;
            Count = last;
            return pos == last ? null : swapped;
        }

        /// <summary>
        /// Removes an item while preserving the order of remaining entries.
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
    }

    /// <summary>
    /// Registry holding per-camera 2D buckets and lookup maps.
    /// </summary>
    public sealed class Camera2DRegistry {
        /// <summary>
        /// Gets the bucket arrays.
        /// </summary>
        public readonly RenderBucket2D[] Buckets;

        /// <summary>
        /// Gets the drawable-to-index map.
        /// </summary>
        public readonly Dictionary<IDrawable2D, Index2D> Map;

        /// <summary>
        /// Initializes the registry with bucket and map capacities.
        /// </summary>
        public Camera2DRegistry(int bucketCount, int initialBucketCapacity = 32, int mapCapacity = 128) {
            Buckets = new RenderBucket2D[bucketCount];
            for (int i = 0; i < bucketCount; i++) {
                Buckets[i] = new RenderBucket2D(initialBucketCapacity);
            }
            Map = new Dictionary<IDrawable2D, Index2D>(mapCapacity, ReferenceEqualityComparer<IDrawable2D>.Instance);
        }
    }

    /// <summary>
    /// Reference equality comparer to avoid overriding Equals on drawables.
    /// </summary>
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class {
        /// <summary>
        /// Shared singleton instance.
        /// </summary>
        public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();
        /// <summary>
        /// Prevents external instantiation to enforce singleton usage.
        /// </summary>
        private ReferenceEqualityComparer() { }
        /// <summary>
        /// Compares two references for identity equality.
        /// </summary>
        /// <param name="x">First object to compare.</param>
        /// <param name="y">Second object to compare.</param>
        /// <returns><c>true</c> if both references point to the same instance; otherwise, <c>false</c>.</returns>
        public bool Equals(T x, T y) => ReferenceEquals(x, y);
        /// <summary>
        /// Gets a hash code based on the object's reference.
        /// </summary>
        /// <param name="obj">Object to hash.</param>
        /// <returns>Reference-based hash code.</returns>
        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
