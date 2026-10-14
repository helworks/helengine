using helengine.baseplatform.Definitions;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Builds Play settings snapshots and a workspace-owned settings file writer for Play session tests.
/// </summary>
public static class TestPlaySettings {
    /// <summary>Workspace-owned root that holds the Play settings files tests write.</summary>
    public const string ProjectRoot = @"C:\dev\helworks\builds\helengine\tests\play-session-project";

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
