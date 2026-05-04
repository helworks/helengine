namespace helengine {
    /// <summary>
    /// Captures shared batching eligibility metadata for one drawable submission.
    /// </summary>
    public class RenderFrameBatchingMetadata {
        /// <summary>
        /// Initializes one batching metadata entry.
        /// </summary>
        /// <param name="isStaticEligible">Whether the drawable is eligible for static batching.</param>
        /// <param name="isDynamicEligible">Whether the drawable is eligible for dynamic batching.</param>
        /// <param name="isInstancingEligible">Whether the drawable is eligible for instancing.</param>
        public RenderFrameBatchingMetadata(bool isStaticEligible, bool isDynamicEligible, bool isInstancingEligible) {
            IsStaticEligible = isStaticEligible;
            IsDynamicEligible = isDynamicEligible;
            IsInstancingEligible = isInstancingEligible;
        }

        /// <summary>
        /// Gets whether the drawable is eligible for static batching.
        /// </summary>
        public bool IsStaticEligible { get; }

        /// <summary>
        /// Gets whether the drawable is eligible for dynamic batching.
        /// </summary>
        public bool IsDynamicEligible { get; }

        /// <summary>
        /// Gets whether the drawable is eligible for instancing.
        /// </summary>
        public bool IsInstancingEligible { get; }
    }
}
