namespace helengine.files {
    /// <summary>
    /// Describes a packfile layout plan used by the future segmented container writer.
    /// </summary>
    public sealed class PackfileWritePlan {
        /// <summary>
        /// Initializes one packfile write plan.
        /// </summary>
        /// <param name="containerId">Stable container identifier.</param>
        /// <param name="maxSegmentSizeBytes">Maximum bytes per segment, or zero for a single segment.</param>
        public PackfileWritePlan(string containerId, long maxSegmentSizeBytes) {
            if (string.IsNullOrWhiteSpace(containerId)) {
                throw new ArgumentException("Container id is required.", nameof(containerId));
            }
            if (maxSegmentSizeBytes < 0) {
                throw new ArgumentOutOfRangeException(nameof(maxSegmentSizeBytes), "Segment size cannot be negative.");
            }

            ContainerId = containerId;
            MaxSegmentSizeBytes = maxSegmentSizeBytes;
        }

        /// <summary>
        /// Gets the stable container identifier.
        /// </summary>
        public string ContainerId { get; }

        /// <summary>
        /// Gets the maximum bytes per segment, or zero for a single segment.
        /// </summary>
        public long MaxSegmentSizeBytes { get; }
    }
}
