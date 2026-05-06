namespace helengine.editor {
    /// <summary>
    /// Stores the persisted editor-global preferences document.
    /// </summary>
    public sealed class EditorPreferencesDocument {
        /// <summary>
        /// Gets or sets whether the editor UI follows monitor DPI or uses one explicit override.
        /// </summary>
        public EditorUiScaleMode UiScaleMode { get; set; } = EditorUiScaleMode.Auto;

        /// <summary>
        /// Gets or sets the persisted explicit editor UI scale percentage.
        /// </summary>
        public int UiScalePercent { get; set; } = 100;

        /// <summary>
        /// Gets or sets the persisted editor-global theme identifier.
        /// </summary>
        public string ThemeId { get; set; } = EditorThemeCatalog.DefaultThemeId;
    }
}
