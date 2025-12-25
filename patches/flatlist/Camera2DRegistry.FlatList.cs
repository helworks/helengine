namespace helengine {
    /// <summary>
    /// Registry holding a single flat 2D list and lookup map per camera.
    /// </summary>
    public sealed class Camera2DRegistry {
        /// <summary>
        /// Gets the buckets array (single entry for flat lists).
        /// </summary>
        public readonly RenderBucket2D[] Buckets;

        /// <summary>
        /// Gets the flat list bucket.
        /// </summary>
        public readonly RenderBucket2D Bucket;

        /// <summary>
        /// Gets the drawable-to-index map.
        /// </summary>
        public readonly Dictionary<IDrawable2D, Index2D> Map;

        /// <summary>
        /// Initializes the registry with list and map capacities.
        /// </summary>
        /// <param name="bucketCount">Ignored bucket count placeholder.</param>
        /// <param name="initialBucketCapacity">Initial list capacity.</param>
        /// <param name="mapCapacity">Initial map capacity.</param>
        public Camera2DRegistry(int bucketCount, int initialBucketCapacity = 32, int mapCapacity = 128) {
            Bucket = new RenderBucket2D(initialBucketCapacity);
            Buckets = new RenderBucket2D[1];
            Buckets[0] = Bucket;
            Map = new Dictionary<IDrawable2D, Index2D>(mapCapacity, ReferenceEqualityComparer<IDrawable2D>.Instance);
        }
    }
}
