namespace helengine {
    /// <summary>
    /// Provides a cache for compiled shader results.
    /// </summary>
    public interface IShaderCompileCache {
        /// <summary>
        /// Attempts to retrieve a cached shader compile result.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="result">Cached result when found.</param>
        /// <returns>True when a cached result is available.</returns>
        bool TryGet(ShaderCompileCacheKey key, out ShaderCompileResult result);

        /// <summary>
        /// Stores a shader compile result in the cache.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="result">Result to store.</param>
        void Store(ShaderCompileCacheKey key, ShaderCompileResult result);
    }
}
