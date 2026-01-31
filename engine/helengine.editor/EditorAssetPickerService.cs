namespace helengine.editor {
    /// <summary>
    /// Broadcasts asset pick requests from editor UI to the active asset picker modal.
    /// </summary>
    public static class EditorAssetPickerService {
        /// <summary>
        /// Raised when an editor field requests an asset pick operation.
        /// </summary>
        public static event Action<AssetPickerRequest> PickRequested;

        /// <summary>
        /// Requests that the editor show the asset picker and return the chosen asset.
        /// </summary>
        /// <param name="onPicked">Callback invoked with the selected asset entry.</param>
        public static void RequestPick(Action<AssetBrowserEntry> onPicked) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            PickRequested?.Invoke(new AssetPickerRequest(onPicked, string.Empty));
        }

        /// <summary>
        /// Requests that the editor show the asset picker with an extension filter.
        /// </summary>
        /// <param name="onPicked">Callback invoked with the selected asset entry.</param>
        /// <param name="extensionFilter">Extension filter for assets.</param>
        public static void RequestPick(Action<AssetBrowserEntry> onPicked, string extensionFilter) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            PickRequested?.Invoke(new AssetPickerRequest(onPicked, extensionFilter ?? string.Empty));
        }
    }
}
