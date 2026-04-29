namespace helengine.editor {
    /// <summary>
    /// Stores one confirmed build-settings platform selection.
    /// </summary>
    public class BuildSettingsSelection {
        /// <summary>
        /// Gets the selected platform ids in dialog row order.
        /// </summary>
        public IReadOnlyList<string> SelectedPlatformIds { get; }

        /// <summary>
        /// Initializes one confirmed build-settings selection.
        /// </summary>
        /// <param name="selectedPlatformIds">Platform ids chosen by the user.</param>
        public BuildSettingsSelection(IReadOnlyList<string> selectedPlatformIds) {
            if (selectedPlatformIds == null) {
                throw new ArgumentNullException(nameof(selectedPlatformIds));
            }

            List<string> copiedPlatformIds = new List<string>(selectedPlatformIds.Count);
            for (int index = 0; index < selectedPlatformIds.Count; index++) {
                copiedPlatformIds.Add(selectedPlatformIds[index]);
            }

            SelectedPlatformIds = copiedPlatformIds;
        }
    }
}
