namespace helengine.editor {
    /// <summary>
    /// Lets a plugin cook its own asset extension while a scene is packaged, instead of the packager copying the file raw.
    /// </summary>
    public interface IPackagedAssetCooker {
        /// <summary>
        /// Gets the file extension this cooker claims, including the leading dot. Compared case-insensitively.
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Cooks one authored file-system asset reference into the build root and returns the reference the packaged scene keeps.
        /// </summary>
        /// <param name="reference">Authored scene asset reference whose extension matches <see cref="Extension"/>.</param>
        /// <param name="context">Packaging context that resolves the source file and writes cooked output.</param>
        /// <returns>Packaged reference that replaces the authored reference in the packaged scene.</returns>
        SceneAssetReference Cook(SceneAssetReference reference, PackagedAssetCookContext context);
    }
}
