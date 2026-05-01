namespace helengine.editor {
    /// <summary>
    /// Captures the confirmed platform and profile document coming out of the Profiles dialog.
    /// </summary>
    public sealed class ProfilesDialogSelection {
        /// <summary>
        /// Gets the active platform identifier selected in the dialog.
        /// </summary>
        public string ActivePlatformId { get; }

        /// <summary>
        /// Gets the profile settings document that should be persisted.
        /// </summary>
        public EditorProfileSettingsDocument ProfileSettingsDocument { get; }

        /// <summary>
        /// Initializes one confirmed profiles-dialog selection.
        /// </summary>
        /// <param name="activePlatformId">Selected active platform identifier.</param>
        /// <param name="profileSettingsDocument">Profile settings document to persist.</param>
        public ProfilesDialogSelection(string activePlatformId, EditorProfileSettingsDocument profileSettingsDocument) {
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            }
            if (profileSettingsDocument == null) {
                throw new ArgumentNullException(nameof(profileSettingsDocument));
            }

            ActivePlatformId = activePlatformId;
            ProfileSettingsDocument = profileSettingsDocument;
        }
    }
}
