namespace helengine.editor {
    /// <summary>
    /// Tracks the active scene-owned canvas profile used by editor previews, layout tooling, and scene settings UI.
    /// </summary>
    public sealed class EditorSceneCanvasProfileState {
        /// <summary>
        /// Backing scene settings payload currently owned by the editor session.
        /// </summary>
        SceneSettingsAsset SceneSettingsValue = new SceneSettingsAsset();

        /// <summary>
        /// Raised whenever the active scene canvas profile changes.
        /// </summary>
        public event Action SettingsChanged;

        /// <summary>
        /// Gets the active scene settings payload.
        /// </summary>
        public SceneSettingsAsset SceneSettings => SceneSettingsValue;

        /// <summary>
        /// Gets the active scene canvas width in logical pixels.
        /// </summary>
        public int CanvasWidth => SceneSettingsValue.CanvasProfile.Width;

        /// <summary>
        /// Gets the active scene canvas height in logical pixels.
        /// </summary>
        public int CanvasHeight => SceneSettingsValue.CanvasProfile.Height;

        /// <summary>
        /// Replaces the active scene settings payload and notifies listeners when the canvas profile changed.
        /// </summary>
        /// <param name="sceneSettings">Scene settings that should become active.</param>
        public void ApplySceneSettings(SceneSettingsAsset sceneSettings) {
            if (sceneSettings == null) {
                throw new ArgumentNullException(nameof(sceneSettings));
            }
            if (sceneSettings.CanvasProfile == null) {
                throw new InvalidOperationException("Scene settings must include a canvas profile.");
            }

            bool widthChanged = SceneSettingsValue.CanvasProfile.Width != sceneSettings.CanvasProfile.Width;
            bool heightChanged = SceneSettingsValue.CanvasProfile.Height != sceneSettings.CanvasProfile.Height;
            SceneSettingsValue = sceneSettings;
            if (widthChanged || heightChanged) {
                RaiseSettingsChanged();
            }
        }

        /// <summary>
        /// Applies one logical canvas width to the active scene settings payload.
        /// </summary>
        /// <param name="canvasWidth">Logical scene canvas width in pixels.</param>
        public void SetCanvasWidth(int canvasWidth) {
            int clampedValue = Math.Max(1, canvasWidth);
            if (SceneSettingsValue.CanvasProfile.Width == clampedValue) {
                return;
            }

            SceneSettingsValue.CanvasProfile.Width = clampedValue;
            RaiseSettingsChanged();
        }

        /// <summary>
        /// Applies one logical canvas height to the active scene settings payload.
        /// </summary>
        /// <param name="canvasHeight">Logical scene canvas height in pixels.</param>
        public void SetCanvasHeight(int canvasHeight) {
            int clampedValue = Math.Max(1, canvasHeight);
            if (SceneSettingsValue.CanvasProfile.Height == clampedValue) {
                return;
            }

            SceneSettingsValue.CanvasProfile.Height = clampedValue;
            RaiseSettingsChanged();
        }

        /// <summary>
        /// Raises <see cref="SettingsChanged"/> when listeners are present.
        /// </summary>
        void RaiseSettingsChanged() {
            if (SettingsChanged != null) {
                SettingsChanged();
            }
        }
    }
}
