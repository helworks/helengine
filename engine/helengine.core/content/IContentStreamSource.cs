namespace helengine {
    /// <summary>
    /// Opens readable streams for runtime content paths understood by the active host or packaged runtime.
    /// </summary>
    public interface IContentStreamSource {
        /// <summary>
        /// Opens one readable stream for the supplied asset path.
        /// </summary>
        /// <param name="assetPath">Runtime asset path understood by the active source.</param>
        /// <returns>Readable stream for the requested asset path.</returns>
        Stream OpenRead(string assetPath);
    }
}
