namespace helengine {
    /// <summary>
    /// Stores the current asset path being decoded so binary reader failures can report their source file.
    /// </summary>
    public static class EngineBinaryReadContext {
        /// <summary>
        /// Tracks the asset path currently being deserialized by the active runtime read operation.
        /// </summary>
        static string CurrentAssetPathValue = string.Empty;

        /// <summary>
        /// Gets or sets the current asset path associated with engine binary reads.
        /// </summary>
        public static string CurrentAssetPath {
            get { return CurrentAssetPathValue; }
            set {
                if (value == null) {
                    CurrentAssetPathValue = string.Empty;
                } else {
                    CurrentAssetPathValue = value;
                }
            }
        }
    }
}
