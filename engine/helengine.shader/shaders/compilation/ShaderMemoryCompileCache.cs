namespace helengine {
    /// <summary>
    /// Provides an in-memory cache for shader compilation results.
    /// </summary>
    public class ShaderMemoryCompileCache : IShaderCompileCache {
        /// <summary>
        /// Stores cached shader results keyed by the cache key string.
        /// </summary>
        readonly Dictionary<string, ShaderCompileResult> cache;

        /// <summary>
        /// Initializes a new in-memory shader compile cache.
        /// </summary>
        public ShaderMemoryCompileCache() {
            cache = new Dictionary<string, ShaderCompileResult>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Attempts to retrieve a cached shader compile result.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="result">Cached result when found.</param>
        /// <returns>True when a cached result is available.</returns>
        public bool TryGet(ShaderCompileCacheKey key, out ShaderCompileResult result) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            return cache.TryGetValue(key.ToString(), out result);
        }

        /// <summary>
        /// Stores a shader compile result in the cache.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="result">Result to store.</param>
        public void Store(ShaderCompileCacheKey key, ShaderCompileResult result) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            cache[key.ToString()] = result;
        }
    }
}
