namespace helengine.editor {
    /// <summary>
    /// Describes one editor-global theme option with a stable persisted identifier and palette factory.
    /// </summary>
    public sealed class EditorThemeDefinition {
        /// <summary>
        /// Initializes one editor theme definition.
        /// </summary>
        /// <param name="id">Stable persisted theme identifier.</param>
        /// <param name="displayName">User-facing theme label shown in Preferences.</param>
        /// <param name="paletteFactory">Factory that resolves the runtime palette when the theme is applied.</param>
        public EditorThemeDefinition(string id, string displayName, Func<ThemeManager.ThemePalette> paletteFactory) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentException("Theme id must be provided.", nameof(id));
            }
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Theme display name must be provided.", nameof(displayName));
            }

            Id = id;
            DisplayName = displayName;
            PaletteFactory = paletteFactory ?? throw new ArgumentNullException(nameof(paletteFactory));
        }

        /// <summary>
        /// Gets the stable persisted identifier used by the editor preferences document.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the user-facing display name shown by the Preferences dialog.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the factory used to resolve the runtime theme palette when this theme is selected.
        /// </summary>
        public Func<ThemeManager.ThemePalette> PaletteFactory { get; }
    }
}
