namespace helengine {
    /// <summary>
    /// Registry holding a single flat 3D list and lookup map per camera.
    /// </summary>
    public sealed class Camera3DRegistry {
        /// <summary>
        /// Buckets by variant, order bucket, and bin (single entry for flat lists).
        /// </summary>
        public readonly RenderBucket3D[][][] Buckets;

        /// <summary>
        /// Gets the flat list bucket.
        /// </summary>
        public readonly RenderBucket3D Bucket;

        /// <summary>
        /// Drawable-to-index map.
        /// </summary>
        public readonly Dictionary<IDrawable3D, Index3D> Map;

        /// <summary>
        /// Gets the number of bins per bucket (always one for flat lists).
        /// </summary>
        public readonly int BinsPerBucket;

        /// <summary>
        /// Initializes the registry with configurable sizes.
        /// </summary>
        /// <param name="variants">Ignored variant placeholder.</param>
        /// <param name="bucketsPerVariant">Ignored bucket placeholder.</param>
        /// <param name="binsPerBucket">Ignored bin placeholder.</param>
        /// <param name="initialBucketCapacity">Initial list capacity.</param>
        /// <param name="mapCapacity">Initial map capacity.</param>
        public Camera3DRegistry(
            int variants,
            int bucketsPerVariant,
            int binsPerBucket,
            int initialBucketCapacity = 32,
            int mapCapacity = 256) {
            BinsPerBucket = 1;
            Bucket = new RenderBucket3D(initialBucketCapacity);

            Buckets = new RenderBucket3D[1][][];
            Buckets[0] = new RenderBucket3D[1][];
            Buckets[0][0] = new RenderBucket3D[1];
            Buckets[0][0][0] = Bucket;

            Map = new Dictionary<IDrawable3D, Index3D>(mapCapacity, ReferenceEqualityComparer<IDrawable3D>.Instance);
        }
    }
}
