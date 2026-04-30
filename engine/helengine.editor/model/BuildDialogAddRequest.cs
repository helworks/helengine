namespace helengine.editor {
    /// <summary>
    /// Stores one queued-build request captured from the build dialog for the active platform tab.
    /// </summary>
    public class BuildDialogAddRequest {
        /// <summary>
        /// Gets the platform id selected by the currently active tab.
        /// </summary>
        public string PlatformId { get; }

        /// <summary>
        /// Gets the project-relative scene ids selected for this queued build.
        /// </summary>
        public IReadOnlyList<string> SelectedSceneIds { get; }

        /// <summary>
        /// Gets the output directory path selected for this queued build.
        /// </summary>
        public string OutputDirectoryPath { get; }

        /// <summary>
        /// Initializes one queued-build request captured from the dialog UI.
        /// </summary>
        /// <param name="platformId">Platform id for the queued build.</param>
        /// <param name="selectedSceneIds">Project-relative scene ids selected by the user.</param>
        /// <param name="outputDirectoryPath">Output directory path chosen by the user.</param>
        public BuildDialogAddRequest(string platformId, IReadOnlyList<string> selectedSceneIds, string outputDirectoryPath) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id is required.", nameof(platformId));
            }

            if (selectedSceneIds == null) {
                throw new ArgumentNullException(nameof(selectedSceneIds));
            }

            if (outputDirectoryPath == null) {
                throw new ArgumentNullException(nameof(outputDirectoryPath));
            }

            List<string> copiedSceneIds = new List<string>(selectedSceneIds.Count);
            for (int index = 0; index < selectedSceneIds.Count; index++) {
                copiedSceneIds.Add(selectedSceneIds[index]);
            }

            PlatformId = platformId;
            SelectedSceneIds = copiedSceneIds;
            OutputDirectoryPath = outputDirectoryPath;
        }
    }
}
