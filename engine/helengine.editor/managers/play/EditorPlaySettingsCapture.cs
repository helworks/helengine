using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Captures the Play settings on the editor frame thread when Play is pressed: loads <c>user_settings/play.json</c>,
    /// evaluates it for the selected platform and freezes it into a snapshot. Invalid values refuse Play with one message;
    /// a broken file throws (it is not a normal path).
    /// </summary>
    public static class EditorPlaySettingsCapture {
        /// <summary>
        /// Tries to capture the Play settings for one Play.
        /// </summary>
        /// <param name="store">Per-user Play settings store.</param>
        /// <param name="platformId">Platform being played.</param>
        /// <param name="capability">Play capability of the platform.</param>
        /// <param name="videoModeCapability">Video modes of the platform, or null when it declares none.</param>
        /// <param name="waitForDebuggerRequested">Whether Shift was held on the Play button.</param>
        /// <param name="editorProcessId">Process id of this editor.</param>
        /// <param name="snapshot">The snapshot when captured; null otherwise.</param>
        /// <param name="failureMessage">Why Play is refused; empty when captured.</param>
        /// <returns>True when the settings are valid and captured.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the store is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the Play settings file is broken (malformed, unknown version or id).</exception>
        public static bool TryCapture(
            EditorPlaySettingsStore store,
            string platformId,
            PlatformPlayerCapability capability,
            PlatformVideoModeCapability videoModeCapability,
            bool waitForDebuggerRequested,
            int editorProcessId,
            out EditorPlaySettingsSnapshot snapshot,
            out string failureMessage) {
            if (store == null) {
                throw new ArgumentNullException(nameof(store));
            }

            EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(store.Load(), store.FilePath, platformId, capability);
            if (!evaluation.IsValid) {
                snapshot = null;
                failureMessage = evaluation.FormatFailure();
                return false;
            }

            snapshot = new EditorPlaySettingsSnapshot(evaluation, waitForDebuggerRequested, capability, videoModeCapability, editorProcessId);
            failureMessage = string.Empty;
            return true;
        }
    }
}
