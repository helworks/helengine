namespace helengine.editor {
    /// <summary>
    /// Resolves stable scene asset references back into runtime assets required by persisted components.
    /// </summary>
    public interface ISceneAssetReferenceResolver {
        /// <summary>
        /// Resolves one stable scene asset reference into a runtime model.
        /// </summary>
        /// <param name="reference">Stable scene asset reference to resolve.</param>
        /// <returns>Runtime model resolved from the reference.</returns>
        RuntimeModel ResolveModel(SceneAssetReference reference);

        /// <summary>
        /// Resolves one stable scene asset reference into a runtime material.
        /// </summary>
        /// <param name="reference">Stable scene asset reference to resolve.</param>
        /// <returns>Runtime material resolved from the reference.</returns>
        RuntimeMaterial ResolveMaterial(SceneAssetReference reference);

        /// <summary>
        /// Resolves one stable scene asset reference into a runtime font.
        /// </summary>
        /// <param name="reference">Stable scene asset reference to resolve.</param>
        /// <returns>Runtime font resolved from the reference.</returns>
        FontAsset ResolveFont(SceneAssetReference reference);
    }
}
