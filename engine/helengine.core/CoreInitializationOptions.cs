namespace helengine {
    /// <summary>
    /// Describes core initialization settings for bucket counts and default capacities.
    /// </summary>
    public class CoreInitializationOptions {
        /// <summary>
        /// Gets or sets the number of update buckets used for per-frame updates.
        /// </summary>
        public byte TotalUpdateBuckets { get; set; } = 4;

        /// <summary>
        /// Gets or sets the number of 2D render buckets used for draw ordering.
        /// </summary>
        public byte TotalBuckets2D { get; set; } = 4;

        /// <summary>
        /// Gets or sets the number of 3D render buckets used for draw ordering.
        /// </summary>
        public byte TotalBuckets3D { get; set; } = 4;

        /// <summary>
        /// Gets or sets the number of 3D render variants supported per camera.
        /// </summary>
        public byte TotalVariants3D { get; set; } = 4;

        /// <summary>
        /// Gets or sets the number of camera buckets used for camera draw ordering.
        /// </summary>
        public byte TotalCameraBuckets { get; set; } = 3;

        /// <summary>
        /// Gets or sets the number of state bins per 3D bucket.
        /// </summary>
        public int RenderBucket3DBinsPerBucket { get; set; } = 4;

        /// <summary>
        /// Gets or sets the initial capacity for each update bucket.
        /// </summary>
        public int[] UpdateBucketInitialCapacity { get; set; } = new int[] { 4, 4, 4, 4 };

        /// <summary>
        /// Gets or sets the initial capacity for 2D render buckets.
        /// </summary>
        public int RenderBucket2DInitialCapacity { get; set; } = 64;

        /// <summary>
        /// Gets or sets the initial capacity for 3D render buckets.
        /// </summary>
        public int RenderBucket3DInitialCapacity { get; set; } = 64;

        /// <summary>
        /// Gets or sets the initial capacity for camera 2D lookup maps.
        /// </summary>
        public int Camera2DMapCapacity { get; set; } = 256;

        /// <summary>
        /// Gets or sets the initial capacity for camera 3D lookup maps.
        /// </summary>
        public int Camera3DMapCapacity { get; set; } = 512;

        /// <summary>
        /// Clamps option values to valid minimums.
        /// </summary>
        public void Normalize() {
            TotalUpdateBuckets = (byte)Math.Max(1, (int)TotalUpdateBuckets);
            TotalBuckets2D = (byte)Math.Max(1, (int)TotalBuckets2D);
            TotalBuckets3D = (byte)Math.Max(1, (int)TotalBuckets3D);
            TotalVariants3D = (byte)Math.Max(1, (int)TotalVariants3D);
            TotalCameraBuckets = (byte)Math.Max(1, (int)TotalCameraBuckets);
            RenderBucket3DBinsPerBucket = Math.Max(1, RenderBucket3DBinsPerBucket);
            if (UpdateBucketInitialCapacity == null) {
                throw new InvalidOperationException("UpdateBucketInitialCapacity must be set.");
            }

            if (UpdateBucketInitialCapacity.Length != TotalUpdateBuckets) {
                throw new InvalidOperationException("UpdateBucketInitialCapacity must match TotalUpdateBuckets.");
            }

            for (int i = 0; i < UpdateBucketInitialCapacity.Length; i++) {
                UpdateBucketInitialCapacity[i] = Math.Max(1, UpdateBucketInitialCapacity[i]);
            }
            RenderBucket2DInitialCapacity = Math.Max(1, RenderBucket2DInitialCapacity);
            RenderBucket3DInitialCapacity = Math.Max(1, RenderBucket3DInitialCapacity);
            Camera2DMapCapacity = Math.Max(1, Camera2DMapCapacity);
            Camera3DMapCapacity = Math.Max(1, Camera3DMapCapacity);
        }
    }
}
