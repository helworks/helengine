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
        /// Gets or sets the initial capacity for update buckets.
        /// </summary>
        public int UpdateBucketInitialCapacity { get; set; } = 64;

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
            if (TotalUpdateBuckets < 1) {
                TotalUpdateBuckets = 1;
            }

            if (TotalBuckets2D < 1) {
                TotalBuckets2D = 1;
            }

            if (TotalBuckets3D < 1) {
                TotalBuckets3D = 1;
            }

            if (TotalVariants3D < 1) {
                TotalVariants3D = 1;
            }

            if (TotalCameraBuckets < 1) {
                TotalCameraBuckets = 1;
            }

            if (RenderBucket3DBinsPerBucket < 1) {
                RenderBucket3DBinsPerBucket = 1;
            }

            if (UpdateBucketInitialCapacity < 1) {
                UpdateBucketInitialCapacity = 1;
            }

            if (RenderBucket2DInitialCapacity < 1) {
                RenderBucket2DInitialCapacity = 1;
            }

            if (RenderBucket3DInitialCapacity < 1) {
                RenderBucket3DInitialCapacity = 1;
            }

            if (Camera2DMapCapacity < 1) {
                Camera2DMapCapacity = 1;
            }

            if (Camera3DMapCapacity < 1) {
                Camera3DMapCapacity = 1;
            }
        }
    }
}
