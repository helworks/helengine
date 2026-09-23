namespace helengine {
    /// <summary>
    /// Allows a host-only shader backend to opt out of the shared compile cache.
    /// </summary>
    public interface IShaderBackendCachePolicy {
        /// <summary>
        /// Gets whether the shared shader compile cache should be used for this backend.
        /// </summary>
        bool UseSharedCache { get; }
    }
}
