namespace helengine.editor {
    /// <summary>
    /// Stores one validated editor-global preferences selection used by the Preferences dialog and session apply flow.
    /// </summary>
    public sealed class EditorPreferencesSettings {
        /// <summary>
        /// Initializes one validated editor-global preferences selection.
        /// </summary>
        /// <param name="uiScale">Validated editor UI scale settings.</param>
        /// <param name="themeId">Stable theme identifier resolved through the editor theme catalog.</param>
        public EditorPreferencesSettings(EditorUiScaleSettings uiScale, string themeId) {
            UiScale = uiScale ?? throw new ArgumentNullException(nameof(uiScale));
            if (EditorThemeCatalog.FindById(themeId) == null) {
                throw new ArgumentOutOfRangeException(nameof(themeId), "Theme id must resolve through the editor theme catalog.");
            }

            ThemeId = themeId;
        }

        /// <summary>
        /// Gets the validated editor UI scale settings selected by the user.
        /// </summary>
        public EditorUiScaleSettings UiScale { get; }

        /// <summary>
        /// Gets the stable persisted identifier of the selected editor theme.
        /// </summary>
        public string ThemeId { get; }
    }
}
