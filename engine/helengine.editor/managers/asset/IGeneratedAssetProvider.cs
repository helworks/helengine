namespace helengine.editor {
    /// <summary>
    /// Supplies browseable generated assets and resolves runtime data for generated entries.
    /// </summary>
    public interface IGeneratedAssetProvider {
        /// <summary>
        /// Gets the stable provider identifier used by generated entries.
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Appends entries that live directly under one virtual relative path.
        /// </summary>
        /// <param name="relativePath">Virtual path whose direct children should be appended.</param>
        /// <param name="entries">Target list that receives the generated entries.</param>
        void LoadEntries(string relativePath, List<AssetBrowserEntry> entries);

        /// <summary>
        /// Attempts to resolve one generated model entry to a runtime model.
        /// </summary>
        /// <param name="entry">Generated entry requested by the editor.</param>
        /// <param name="runtimeModel">Resolved runtime model when the provider owns the entry.</param>
        /// <returns>True when the provider resolved the entry; otherwise false.</returns>
        bool TryResolveRuntimeModel(AssetBrowserEntry entry, out RuntimeModel runtimeModel);
    }
}
