namespace helengine.editor {
    /// <summary>
    /// Carries the full asset-settings changes requested from the properties panel.
    /// </summary>
    public class AssetImportSettingsApplyRequest {
        /// <summary>
        /// Initializes a new apply request with the pending importer and processor settings.
        /// </summary>
        /// <param name="importerId">Importer identifier selected in the view.</param>
        /// <param name="selectedPlatformId">Platform currently selected in the processor tabs.</param>
        /// <param name="processorSettings">Pending processor settings to persist.</param>
        public AssetImportSettingsApplyRequest(string importerId, string selectedPlatformId, AssetProcessorSettings processorSettings) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            } else if (string.IsNullOrWhiteSpace(selectedPlatformId)) {
                throw new ArgumentException("Selected platform id must be provided.", nameof(selectedPlatformId));
            } else if (processorSettings == null) {
                throw new ArgumentNullException(nameof(processorSettings));
            }

            ImporterId = importerId;
            SelectedPlatformId = selectedPlatformId;
            ProcessorSettings = processorSettings;
        }

        /// <summary>
        /// Gets the importer identifier selected in the view.
        /// </summary>
        public string ImporterId { get; }

        /// <summary>
        /// Gets the processor platform tab selected when the request was raised.
        /// </summary>
        public string SelectedPlatformId { get; }

        /// <summary>
        /// Gets the pending processor settings to persist.
        /// </summary>
        public AssetProcessorSettings ProcessorSettings { get; }
    }
}
