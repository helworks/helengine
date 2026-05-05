namespace helengine.editor {
    /// <summary>
    /// Coordinates persisted editor UI scale settings with host-reported monitor DPI.
    /// </summary>
    public sealed class EditorUiScaleController {
        /// <summary>
        /// Service used to load and save the editor-global UI scale preference document.
        /// </summary>
        readonly EditorPreferencesService PreferencesService;

        /// <summary>
        /// Current validated editor UI scale settings loaded from the preferences service.
        /// </summary>
        EditorUiScaleSettings CurrentSettings;

        /// <summary>
        /// Initializes one editor UI scale controller around the supplied preferences service.
        /// </summary>
        /// <param name="preferencesService">Preferences service used to persist editor-global UI scale settings.</param>
        public EditorUiScaleController(EditorPreferencesService preferencesService) {
            PreferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
            CurrentSettings = PreferencesService.Load();
        }

        /// <summary>
        /// Loads the latest validated editor UI scale settings from the preferences service.
        /// </summary>
        /// <returns>Current validated editor UI scale settings.</returns>
        public EditorUiScaleSettings Load() {
            CurrentSettings = PreferencesService.Load();
            return CurrentSettings;
        }

        /// <summary>
        /// Resolves the effective editor UI metrics for the supplied monitor DPI.
        /// </summary>
        /// <param name="monitorDpi">Current monitor DPI reported by the active host.</param>
        /// <returns>Scaled editor UI metrics for the current settings and monitor DPI.</returns>
        public EditorUiMetrics ResolveMetrics(int monitorDpi) {
            return new EditorUiMetrics(CurrentSettings.ResolveEffectiveScale(monitorDpi));
        }

        /// <summary>
        /// Persists one new user-selected editor UI scale settings document and returns the validated current value.
        /// </summary>
        /// <param name="settings">Validated editor UI scale settings chosen by the user.</param>
        /// <returns>Current validated editor UI scale settings after persistence.</returns>
        public EditorUiScaleSettings ApplyUserSelection(EditorUiScaleSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            PreferencesService.Save(settings);
            CurrentSettings = settings;
            return CurrentSettings;
        }

        /// <summary>
        /// Returns true when a host DPI change should trigger a live editor UI scale refresh.
        /// </summary>
        /// <returns>True when the current settings follow monitor DPI; otherwise false.</returns>
        public bool ShouldReapplyForMonitorDpiChange() {
            return CurrentSettings.Mode == EditorUiScaleMode.Auto;
        }
    }
}
