namespace helengine {
    /// <summary>
    /// Base type for runtime asset instances tracked by the engine.
    /// </summary>
    public class RuntimeData {
        /// <summary>
        /// Gets the unique identifier for this runtime asset.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Assigns the unique identifier for this runtime asset during material or model construction.
        /// </summary>
        /// <param name="id">Unique runtime asset identifier.</param>
        public void SetId(string id) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentException("Runtime asset id must be provided.", nameof(id));
            }

            Id = id;
        }
    }
}
