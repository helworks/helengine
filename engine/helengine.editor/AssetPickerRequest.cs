namespace helengine.editor {
    /// <summary>
    /// Represents a request to pick an asset with an optional extension filter.
    /// </summary>
    public sealed class AssetPickerRequest {
        /// <summary>
        /// Initializes a new asset picker request.
        /// </summary>
        /// <param name="onPicked">Callback invoked when an asset is selected.</param>
        /// <param name="extensionFilter">Optional extension filter (with or without a leading dot).</param>
        public AssetPickerRequest(Action<AssetBrowserEntry> onPicked, string extensionFilter) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            OnPicked = onPicked;
            ExtensionFilter = extensionFilter ?? string.Empty;
        }

        /// <summary>
        /// Gets the callback invoked when an asset is selected.
        /// </summary>
        public Action<AssetBrowserEntry> OnPicked { get; }

        /// <summary>
        /// Gets the extension filter for the picker.
        /// </summary>
        public string ExtensionFilter { get; }
    }
}
