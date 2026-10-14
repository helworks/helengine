using helengine.baseplatform.Definitions;
using helengine.editor;

namespace helengine.player.tests {
    /// <summary>
    /// Builds Play settings snapshots and a workspace-owned settings file writer for the editor-to-player argument
    /// contract tests.
    /// </summary>
    public static class ContractPlaySettings {
        /// <summary>Workspace-owned root that holds the Play settings files the contract tests write.</summary>
        public const string ProjectRoot = @"C:\dev\helworks\builds\helengine\tests\play-contract-project";

        /// <summary>
        /// Creates a snapshot of the declared defaults for a platform whose capability paces at the given frame rate.
        /// </summary>
        /// <param name="platformId">Platform id.</param>
        /// <param name="targetFrameRate">Capability frame rate.</param>
        /// <param name="waitForDebuggerRequested">Whether Shift was held.</param>
        /// <returns>Snapshot of default Play settings.</returns>
        public static EditorPlaySettingsSnapshot CreateDefaults(string platformId, double targetFrameRate, bool waitForDebuggerRequested) {
            PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "staging", targetFrameRate, PlayerInputDevices.Gamepad, platformId, "1.0.0", []);
            EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(new EditorPlaySettingsDocument(), ProjectRoot + @"\user_settings\play.json", platformId, capability);
            return new EditorPlaySettingsSnapshot(evaluation, waitForDebuggerRequested, capability, 4242);
        }

        /// <summary>
        /// Creates the settings file writer rooted at <see cref="ProjectRoot"/>.
        /// </summary>
        /// <returns>Settings file writer.</returns>
        public static EditorPlaySettingsFileWriter CreateWriter() {
            return new EditorPlaySettingsFileWriter(ProjectRoot);
        }
    }
}
