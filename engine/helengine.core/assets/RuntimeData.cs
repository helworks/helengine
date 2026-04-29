namespace helengine {
    /// <summary>
    /// Base type for runtime asset instances tracked by the engine.
    /// </summary>
    public class RuntimeData {
        /// <summary>
        /// Gets the unique identifier for this runtime asset.
        /// </summary>
        public string Id { get; private set; }
    }
}
