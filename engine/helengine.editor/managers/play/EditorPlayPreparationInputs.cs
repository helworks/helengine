using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Editor state the Play cook needs, captured on the editor frame thread when Play is pressed so the background
    /// preparation never reads live editor scene, dialog or catalog objects. Everything here is either immutable, a
    /// private copy, or an object the editor only reads.
    /// </summary>
    public sealed class EditorPlayPreparationInputs {
        /// <summary>
        /// Initializes the captured Play inputs.
        /// </summary>
        /// <param name="platformId">Platform being played.</param>
        /// <param name="scenePath">Saved path of the scene open in the editor when Play was pressed.</param>
        /// <param name="visiblePlatformIds">Copy of the platform ids the build configuration is loaded for.</param>
        /// <param name="buildDialogOpen">Whether the Build dialog held an edited build configuration when Play was pressed.</param>
        /// <param name="buildDialogPlatformConfig">Private copy of the Build dialog's settings for the platform, or null when the dialog had none for it.</param>
        /// <param name="selectionModel">Builder selection model of the platform, resolved from the platform catalog.</param>
        /// <param name="platformDescriptor">Installed platform descriptor the cook's build executor is created for.</param>
        /// <exception cref="ArgumentException">Thrown when the platform id or scene path is blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the platform list, selection model or descriptor is null.</exception>
        public EditorPlayPreparationInputs(
            string platformId,
            string scenePath,
            IReadOnlyList<string> visiblePlatformIds,
            bool buildDialogOpen,
            EditorBuildPlatformConfigDocument buildDialogPlatformConfig,
            EditorPlatformBuildSelectionModel selectionModel,
            AvailablePlatformDescriptor platformDescriptor) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Play inputs require the platform id.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new ArgumentException("Play inputs require the saved scene path.", nameof(scenePath));
            } else if (visiblePlatformIds == null) {
                throw new ArgumentNullException(nameof(visiblePlatformIds));
            } else if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            } else if (platformDescriptor == null) {
                throw new ArgumentNullException(nameof(platformDescriptor));
            }

            PlatformId = platformId;
            ScenePath = scenePath;
            VisiblePlatformIds = visiblePlatformIds.ToArray();
            BuildDialogOpen = buildDialogOpen;
            BuildDialogPlatformConfig = buildDialogPlatformConfig;
            SelectionModel = selectionModel;
            PlatformDescriptor = platformDescriptor;
        }

        /// <summary>
        /// Gets the platform being played.
        /// </summary>
        public string PlatformId { get; }

        /// <summary>
        /// Gets the saved path of the scene open in the editor when Play was pressed.
        /// </summary>
        public string ScenePath { get; }

        /// <summary>
        /// Gets the platform ids the build configuration is loaded for when the Build dialog was closed.
        /// </summary>
        public IReadOnlyList<string> VisiblePlatformIds { get; }

        /// <summary>
        /// Gets whether the Build dialog held an edited build configuration when Play was pressed; Play then uses the
        /// dialog's settings for the platform instead of the saved ones, as it always did.
        /// </summary>
        public bool BuildDialogOpen { get; }

        /// <summary>
        /// Gets the private copy of the Build dialog's settings for the platform, or null when the dialog had none for it.
        /// The preparation may change this copy freely; the dialog never sees it.
        /// </summary>
        public EditorBuildPlatformConfigDocument BuildDialogPlatformConfig { get; }

        /// <summary>
        /// Gets the builder selection model of the platform.
        /// </summary>
        public EditorPlatformBuildSelectionModel SelectionModel { get; }

        /// <summary>
        /// Gets the installed platform descriptor the cook's build executor is created for.
        /// </summary>
        public AvailablePlatformDescriptor PlatformDescriptor { get; }
    }
}
