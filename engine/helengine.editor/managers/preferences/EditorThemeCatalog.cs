namespace helengine.editor {
    /// <summary>
    /// Provides the editor-global theme catalog used by preferences persistence, dialogs, and runtime theme application.
    /// </summary>
    public static class EditorThemeCatalog {
        /// <summary>
        /// Gets the stable identifier of the default editor theme.
        /// </summary>
        public const string DefaultThemeId = "neon-90s";

        /// <summary>
        /// Gets the currently supported editor theme definitions.
        /// </summary>
        public static IReadOnlyList<EditorThemeDefinition> Themes { get; } = new EditorThemeDefinition[] {
            new EditorThemeDefinition(DefaultThemeId, "Neon 90s", ThemeManager.CreateNeon90s),
            new EditorThemeDefinition("dark", "Dark", ThemeManager.CreateDarkTheme),
            new EditorThemeDefinition("light", "Light", ThemeManager.CreateLightTheme)
        };

        /// <summary>
        /// Resolves one theme definition by its stable persisted identifier.
        /// </summary>
        /// <param name="themeId">Stable persisted theme identifier.</param>
        /// <returns>Matching theme definition, or null when the identifier is unknown.</returns>
        public static EditorThemeDefinition FindById(string themeId) {
            if (string.IsNullOrWhiteSpace(themeId)) {
                return null;
            }

            for (int index = 0; index < Themes.Count; index++) {
                EditorThemeDefinition theme = Themes[index];
                if (string.Equals(theme.Id, themeId, StringComparison.Ordinal)) {
                    return theme;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the default theme definition used when persisted data is missing or invalid.
        /// </summary>
        /// <returns>Default editor theme definition.</returns>
        public static EditorThemeDefinition GetDefault() {
            EditorThemeDefinition theme = FindById(DefaultThemeId);
            if (theme == null) {
                throw new InvalidOperationException("The editor theme catalog must contain the declared default theme.");
            }

            return theme;
        }
    }
}
