namespace helengine {
    /// <summary>
    /// Provides helper functions for mapping render orders to buckets.
    /// </summary>
    public static class RenderBucketUtils {
        /// <summary>
        /// Converts a render order into a bucket index for the given bucket count.
        /// </summary>
        /// <param name="renderOrder">Render order value.</param>
        /// <param name="totalBuckets">Total number of buckets.</param>
        /// <returns>Bucket index for the render order.</returns>
        public static int GetBucketIndex(byte renderOrder, byte totalBuckets) {
            if (totalBuckets < 1) {
                return 0;
            }

            return (renderOrder * totalBuckets) / 256;
        }

        /// <summary>
        /// Computes a render order value that maps into a desired bucket.
        /// </summary>
        /// <param name="bucketIndex">Desired bucket index.</param>
        /// <param name="totalBuckets">Total number of buckets.</param>
        /// <returns>Render order value that maps into the requested bucket.</returns>
        public static byte GetRenderOrderForBucket(int bucketIndex, byte totalBuckets) {
            if (totalBuckets < 1) {
                return 0;
            }

            int clampedBucket = bucketIndex;
            if (clampedBucket < 0) {
                clampedBucket = 0;
            } else if (clampedBucket >= totalBuckets) {
                clampedBucket = totalBuckets - 1;
            }

            int baseOrder = (clampedBucket * 256) / totalBuckets;
            if (baseOrder < 0) {
                baseOrder = 0;
            } else if (baseOrder > byte.MaxValue) {
                baseOrder = byte.MaxValue;
            }

            return (byte)baseOrder;
        }
    }
}
