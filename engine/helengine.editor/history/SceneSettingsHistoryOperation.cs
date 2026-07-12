namespace helengine.editor {
    /// <summary>
    /// Reverses and reapplies one authored scene settings change.
    /// </summary>
    public class SceneSettingsHistoryOperation : IEditorHistoryOperation {
        /// <summary>
        /// Detached scene settings snapshot that restores the prior scene-level settings during undo.
        /// </summary>
        readonly SceneSettingsAsset PreviousSceneSettings;

        /// <summary>
        /// Detached scene settings snapshot that restores the new scene-level settings during redo.
        /// </summary>
        readonly SceneSettingsAsset CurrentSceneSettings;

        /// <summary>
        /// Initializes one scene-settings history operation.
        /// </summary>
        /// <param name="previousSceneSettings">Detached snapshot of the prior scene settings.</param>
        /// <param name="currentSceneSettings">Detached snapshot of the new scene settings.</param>
        public SceneSettingsHistoryOperation(SceneSettingsAsset previousSceneSettings, SceneSettingsAsset currentSceneSettings) {
            PreviousSceneSettings = previousSceneSettings ?? throw new ArgumentNullException(nameof(previousSceneSettings));
            CurrentSceneSettings = currentSceneSettings ?? throw new ArgumentNullException(nameof(currentSceneSettings));
        }

        /// <summary>
        /// Gets a short human-readable description of this history operation.
        /// </summary>
        public string Description {
            get { return "Scene Settings"; }
        }

        /// <summary>
        /// Restores the previous scene settings.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Undo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.ApplySceneSettings(PreviousSceneSettings);
        }

        /// <summary>
        /// Reapplies the new scene settings.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Redo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.ApplySceneSettings(CurrentSceneSettings);
        }
    }
}
