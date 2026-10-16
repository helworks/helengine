namespace helengine {
    /// <summary>
    /// Resolves one explicit set of logical content paths before delegating reads.
    /// </summary>
    public sealed class AliasedContentStreamSource : IContentStreamSource, IDisposable {
        /// <summary>
        /// Borrows the underlying source; this wrapper does not own its lifetime.
        /// </summary>
        readonly IContentStreamSource Source;

        /// <summary>
        /// Owns the private alias snapshot copied from the caller's mapping.
        /// </summary>
        [NativeOwnedMember]
        IReadOnlyDictionary<string, string> Aliases;

        /// <summary>
        /// Initializes an aliasing source around a borrowed content source.
        /// </summary>
        /// <param name="source">Underlying content source.</param>
        /// <param name="aliases">Logical-to-stored path mappings.</param>
        public AliasedContentStreamSource(IContentStreamSource source, IReadOnlyDictionary<string, string> aliases) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (aliases == null) {
                throw new ArgumentNullException(nameof(aliases));
            }

            Dictionary<string, string> copiedAliases = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> alias in aliases) {
                if (string.IsNullOrWhiteSpace(alias.Key)) {
                    throw new ArgumentException("Alias keys must be provided.", nameof(aliases));
                }
                if (string.IsNullOrWhiteSpace(alias.Value)) {
                    throw new ArgumentException("Alias values must be provided.", nameof(aliases));
                }

                copiedAliases.Add(alias.Key, alias.Value);
            }

            Aliases = copiedAliases;
        }

        /// <summary>
        /// Opens one logical path after applying at most one explicit alias lookup.
        /// </summary>
        /// <param name="assetPath">Logical or stored asset path.</param>
        /// <returns>Readable stream from the underlying source.</returns>
        public Stream OpenRead(string assetPath) {
            if (Aliases == null) {
                throw new ObjectDisposedException(nameof(AliasedContentStreamSource));
            }
            if (string.IsNullOrWhiteSpace(assetPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(assetPath));
            }

            string mappedPath;
            bool hasAlias = Aliases.TryGetValue(assetPath, out mappedPath);
            return Source.OpenRead(hasAlias ? mappedPath : assetPath);
        }

        /// <summary>
        /// Releases the copied alias container without disposing the borrowed underlying source.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref Aliases);
        }
    }
}
