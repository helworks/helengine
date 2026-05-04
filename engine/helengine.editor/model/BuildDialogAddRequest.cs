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
        /// Gets the debug-build snapshot selected for this queued build.
        /// </summary>
        public bool DebugBuild { get; }

        /// <summary>
        /// Gets the selected builder-provided build profile id.
        /// </summary>
        public string SelectedBuildProfileId { get; }

        /// <summary>
        /// Gets the selected builder-provided graphics profile id.
        /// </summary>
        public string SelectedGraphicsProfileId { get; }

        /// <summary>
        /// Gets the selected builder-provided build option values.
        /// </summary>
        public IReadOnlyDictionary<string, string> SelectedBuildOptionValues { get; }

        /// <summary>
        /// Gets the selected builder-provided graphics option values.
        /// </summary>
        public IReadOnlyDictionary<string, string> SelectedGraphicsOptionValues { get; }

        /// <summary>
        /// Gets the selected builder-provided codegen profile id.
        /// </summary>
        public string SelectedCodegenProfileId { get; }

        /// <summary>
        /// Gets the selected builder-provided storage profile id.
        /// </summary>
        public string SelectedStorageProfileId { get; }

        /// <summary>
        /// Gets the selected builder-provided media profile id.
        /// </summary>
        public string SelectedMediaProfileId { get; }

        /// <summary>
        /// Gets the selected builder-provided codegen option values.
        /// </summary>
        public IReadOnlyDictionary<string, string> SelectedCodegenOptionValues { get; }

        /// <summary>
        /// Gets the project-authored code-module identifiers selected for this queued build.
        /// </summary>
        public IReadOnlyList<string> SelectedCodeModuleIds { get; }

        /// <summary>
        /// Initializes one queued-build request captured from the dialog UI.
        /// </summary>
        /// <param name="platformId">Platform id for the queued build.</param>
        /// <param name="selectedSceneIds">Project-relative scene ids selected by the user.</param>
        /// <param name="outputDirectoryPath">Output directory path chosen by the user.</param>
        /// <param name="debugBuild">True when the queued build should use the debug native player configuration.</param>
        /// <param name="selectedBuildProfileId">Selected builder-provided build profile id.</param>
        /// <param name="selectedGraphicsProfileId">Selected builder-provided graphics profile id.</param>
        /// <param name="selectedCodegenProfileId">Selected builder-provided codegen profile id.</param>
        /// <param name="selectedStorageProfileId">Selected builder-provided storage profile id.</param>
        /// <param name="selectedMediaProfileId">Selected builder-provided media profile id.</param>
        /// <param name="selectedBuildOptionValues">Selected builder-provided build option values.</param>
        /// <param name="selectedGraphicsOptionValues">Selected builder-provided graphics option values.</param>
        /// <param name="selectedCodegenOptionValues">Selected builder-provided codegen option values.</param>
        public BuildDialogAddRequest(string platformId, IReadOnlyList<string> selectedSceneIds, string outputDirectoryPath, bool debugBuild = false)
            : this(platformId, selectedSceneIds, outputDirectoryPath, debugBuild, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null, null, null) {
        }

        /// <summary>
        /// Initializes one queued-build request captured from the dialog UI.
        /// </summary>
        /// <param name="platformId">Platform id for the queued build.</param>
        /// <param name="selectedSceneIds">Project-relative scene ids selected by the user.</param>
        /// <param name="outputDirectoryPath">Output directory path chosen by the user.</param>
        /// <param name="debugBuild">True when the queued build should use the debug native player configuration.</param>
        /// <param name="selectedBuildProfileId">Selected builder-provided build profile id.</param>
        /// <param name="selectedGraphicsProfileId">Selected builder-provided graphics profile id.</param>
        /// <param name="selectedCodegenProfileId">Selected builder-provided codegen profile id.</param>
        /// <param name="selectedStorageProfileId">Selected builder-provided storage profile id.</param>
        /// <param name="selectedMediaProfileId">Selected builder-provided media profile id.</param>
        /// <param name="selectedBuildOptionValues">Selected builder-provided build option values.</param>
        /// <param name="selectedGraphicsOptionValues">Selected builder-provided graphics option values.</param>
        /// <param name="selectedCodegenOptionValues">Selected builder-provided codegen option values.</param>
        /// <param name="selectedCodeModuleIds">Selected project-authored code-module identifiers.</param>
        public BuildDialogAddRequest(
            string platformId,
            IReadOnlyList<string> selectedSceneIds,
            string outputDirectoryPath,
            bool debugBuild,
            string selectedBuildProfileId,
            string selectedGraphicsProfileId,
            string selectedCodegenProfileId,
            string selectedStorageProfileId,
            string selectedMediaProfileId,
            IReadOnlyDictionary<string, string> selectedBuildOptionValues,
            IReadOnlyDictionary<string, string> selectedGraphicsOptionValues,
            IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
            IReadOnlyList<string> selectedCodeModuleIds = null) {
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
            DebugBuild = debugBuild;
            SelectedBuildProfileId = selectedBuildProfileId ?? string.Empty;
            SelectedGraphicsProfileId = selectedGraphicsProfileId ?? string.Empty;
            SelectedCodegenProfileId = selectedCodegenProfileId ?? string.Empty;
            SelectedStorageProfileId = selectedStorageProfileId ?? string.Empty;
            SelectedMediaProfileId = selectedMediaProfileId ?? string.Empty;
            SelectedBuildOptionValues = selectedBuildOptionValues ?? new Dictionary<string, string>();
            SelectedGraphicsOptionValues = selectedGraphicsOptionValues ?? new Dictionary<string, string>();
            SelectedCodegenOptionValues = selectedCodegenOptionValues ?? new Dictionary<string, string>();
            SelectedCodeModuleIds = selectedCodeModuleIds != null
                ? new List<string>(selectedCodeModuleIds)
                : [];
        }
    }
}
