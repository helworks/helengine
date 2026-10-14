namespace helengine.editor {
    /// <summary>
    /// Shares one lazily created <see cref="EditorAssetReferenceResolver"/> across every reference resolved during one
    /// caller-owned operation, such as a single build-configuration load or save. Constructing a resolver initializes a
    /// fresh asset identity index, which walks and stamps the whole <c>assets</c> tree, so batching every scene list of
    /// one operation through this holder pays that cost at most once. The resolver is only created when a file-backed
    /// reference actually needs it, and it is disposed together with this holder.
    /// </summary>
    public sealed class EditorSharedAssetReferenceResolver : IDisposable {
        /// <summary>
        /// Creates the resolver the first time a caller needs one.
        /// </summary>
        readonly Func<EditorAssetReferenceResolver> ResolverFactory;

        /// <summary>
        /// Resolver created by <see cref="ResolverFactory"/>, or null while no caller has needed one yet.
        /// </summary>
        EditorAssetReferenceResolver Resolver;

        /// <summary>
        /// True once <see cref="Dispose"/> has run.
        /// </summary>
        bool IsDisposed;

        /// <summary>
        /// Initializes one shared holder that creates its resolver through the supplied factory on first use.
        /// </summary>
        /// <param name="resolverFactory">Creates the project resolver the first time one is needed.</param>
        /// <exception cref="ArgumentNullException">Thrown when the factory is null.</exception>
        public EditorSharedAssetReferenceResolver(Func<EditorAssetReferenceResolver> resolverFactory) {
            ResolverFactory = resolverFactory ?? throw new ArgumentNullException(nameof(resolverFactory));
        }

        /// <summary>
        /// Returns the shared resolver, creating it through the factory on the first call.
        /// </summary>
        /// <returns>Resolver shared by every caller of this holder.</returns>
        /// <exception cref="ObjectDisposedException">Thrown after the holder was disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the factory returns null.</exception>
        public EditorAssetReferenceResolver GetResolver() {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (Resolver == null) {
                Resolver = ResolverFactory() ?? throw new InvalidOperationException("The asset reference resolver factory returned no resolver.");
            }

            return Resolver;
        }

        /// <summary>
        /// Disposes the shared resolver when one was created.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            IsDisposed = true;
            Resolver?.Dispose();
            Resolver = null;
        }
    }
}
