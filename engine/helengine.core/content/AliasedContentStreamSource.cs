namespace helengine {
    /// <summary>
    /// Resolves one explicit set of logical content paths before delegating reads.
    /// </summary>
    public sealed class AliasedContentStreamSource : IContentStreamSource {
        readonly IContentStreamSource Source;
        readonly IReadOnlyDictionary<string, string> Aliases;

        /// <summary>
        /// Initializes an aliasing source around a borrowed content source.
        /// </summary>
        /// <param name="source">Underlying content source.</param>
        /// <param name="aliases">Logical-to-stored path mappings.</param>
        public AliasedContentStreamSource(IContentStreamSource source, [NativeRetainsBorrow] IReadOnlyDictionary<string, string> aliases) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (aliases == null) {
                throw new ArgumentNullException(nameof(aliases));
            }

            foreach (KeyValuePair<string, string> alias in aliases) {
                if (string.IsNullOrWhiteSpace(alias.Key)) {
                    throw new ArgumentException("Alias keys must be provided.", nameof(aliases));
                }
                if (string.IsNullOrWhiteSpace(alias.Value)) {
                    throw new ArgumentException("Alias values must be provided.", nameof(aliases));
                }

            }

            Aliases = aliases;
        }

        /// <summary>
        /// Opens one logical path after applying at most one explicit alias lookup.
        /// </summary>
        /// <param name="assetPath">Logical or stored asset path.</param>
        /// <returns>Readable stream from the underlying source.</returns>
        [NativeOwnedReturn]
        public Stream OpenRead(string assetPath) {
            if (string.IsNullOrWhiteSpace(assetPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(assetPath));
            }

            string mappedPath = assetPath;
            if (Aliases.TryGetValue(assetPath, out mappedPath)) {
                return Source.OpenRead(mappedPath);
            }

            return Source.OpenRead(assetPath);
        }
    }
}
