namespace helengine {
    /// <summary>
    /// Index mapping for 3D render buckets including variant and bin.
    /// </summary>
    public struct Index3D {
        /// <summary>
        /// Render pipeline variant.
        /// </summary>
        public int Variant;

        /// <summary>
        /// Render order bucket.
        /// </summary>
        public int Bucket;

        /// <summary>
        /// State bin index.
        /// </summary>
        public int Bin;

        /// <summary>
        /// Position within the bucket.
        /// </summary>
        public int Pos;

        /// <summary>
        /// Initializes a new 3D index.
        /// </summary>
        public Index3D(int variant, int bucket, int bin, int pos) { Variant = variant; Bucket = bucket; Bin = bin; Pos = pos; }
    }

    /// <summary>
    /// Dense array bucket for 3D drawables.
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
        /// Creates a bucket with an initial capacity.
        /// </summary>
        public RenderBucket3D(int initialCapacity = 32) {
            if (initialCapacity < 1) initialCapacity = 1;
            Items = new IDrawable3D[initialCapacity];
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
        public void Add(IDrawable3D item, out int pos) {
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
        public IDrawable3D? RemoveSwapAt(int pos) {
            int last = Count - 1;
            if (pos < 0 || pos > last) return null;
            IDrawable3D swapped = Items[last];
            if (pos != last) {
                Items[pos] = swapped;
            }
            Items[last] = default!;
            Count = last;
            return pos == last ? null : swapped;
        }
    }

    /// <summary>
    /// Registry holding per-camera 3D buckets and lookup maps.
    /// </summary>
    public sealed class Camera3DRegistry {
        /// <summary>
        /// Buckets by variant, order bucket, and bin.
        /// </summary>
        public readonly RenderBucket3D[][][] Buckets; // [variant][bucket][bin]

        /// <summary>
        /// Drawable-to-index map.
        /// </summary>
        public readonly Dictionary<IDrawable3D, Index3D> Map;

        /// <summary>
        /// Gets the number of bins per bucket.
        /// </summary>
        public readonly int BinsPerBucket;

        /// <summary>
        /// Initializes the registry with configurable sizes.
        /// </summary>
        public Camera3DRegistry(int variants, int bucketsPerVariant, int binsPerBucket, int initialBucketCapacity = 32, int mapCapacity = 256) {
            BinsPerBucket = binsPerBucket;
            Buckets = new RenderBucket3D[variants][][];
            for (int v = 0; v < variants; v++) {
                Buckets[v] = new RenderBucket3D[bucketsPerVariant][];
                for (int b = 0; b < bucketsPerVariant; b++) {
                    Buckets[v][b] = new RenderBucket3D[binsPerBucket];
                    for (int bin = 0; bin < binsPerBucket; bin++) {
                        Buckets[v][b][bin] = new RenderBucket3D(initialBucketCapacity);
                    }
                }
            }
            Map = new Dictionary<IDrawable3D, Index3D>(mapCapacity, ReferenceEqualityComparer<IDrawable3D>.Instance);
        }
    }
}
