namespace helengine {
    /// <summary>
    /// Marks a persisted member as an ordinal payload extension that follows all existing members.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ScenePersistenceAppendAttribute : Attribute {
        /// <summary>
        /// Initializes an append-only marker with the immutable zero-based ordinal used within the component's optional suffix.
        /// </summary>
        /// <param name="order">Zero-based append ordinal. Existing ordinals must never be renumbered or reused.</param>
        public ScenePersistenceAppendAttribute(int order) {
            if (order < 0) {
                throw new ArgumentOutOfRangeException(nameof(order), "Append order cannot be negative.");
            }

            Order = order;
        }

        /// <summary>
        /// Gets the immutable zero-based ordinal that keeps append-only payload members stable across future extensions.
        /// </summary>
        public int Order { get; }
    }
}
