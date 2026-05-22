namespace helengine {
    /// <summary>
    /// Represents a resolved include file and its source contents.
    /// </summary>
    public class ShaderIncludeResult {
        /// <summary>
        /// Initializes a new include result.
        /// </summary>
        /// <param name="path">Resolved path for the include file.</param>
        /// <param name="source">Source contents of the include file.</param>
        public ShaderIncludeResult(string path, string source) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Include path must be provided.", nameof(path));
            }

            if (string.IsNullOrWhiteSpace(source)) {
                throw new ArgumentException("Include source must be provided.", nameof(source));
            }

            Path = path;
            Source = source;
        }

        /// <summary>
        /// Gets the resolved path for the include file.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the source contents of the include file.
        /// </summary>
        public string Source { get; }
    }
}
