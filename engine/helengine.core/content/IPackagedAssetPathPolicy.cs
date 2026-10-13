namespace helengine {
    /// <summary>
    /// Lets a host decide how packaged asset paths stored in cooked scenes and assets map to the paths its content source opens.
    /// Hosts that leave this unset keep the built-in rules: canonical content-relative paths everywhere, plus rooted paths on
    /// generated console cores built with <c>HELENGINE_RUNTIME_ALLOW_ROOTED_PACKAGED_PATHS</c>.
    /// </summary>
    public interface IPackagedAssetPathPolicy {
        /// <summary>
        /// Maps one packaged asset path read from cooked content to the path passed to the active content source.
        /// Throws when the path cannot be served by this host.
        /// </summary>
        /// <param name="packagedPath">Packaged asset path exactly as stored in cooked content.</param>
        /// <returns>Path understood by the host's content source.</returns>
        string ResolvePackagedPath(string packagedPath);
    }
}
