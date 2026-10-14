using helengine.baseplatform.Definitions;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Builds Play settings snapshots and a workspace-owned settings file writer for Play session tests.
/// </summary>
public static class TestPlaySettings {
    /// <summary>Workspace-owned folder under which every test process gets its own project root.</summary>
    const string ScratchRoot = @"C:\dev\helworks\builds\helengine\tests\play-session-project";

    /// <summary>
    /// Project root that holds the Play settings files this test process writes. It is unique to the process, so test
    /// runs in other sessions or worktrees never read or overwrite these files, and it is deleted when the process exits.
    /// </summary>
    public static readonly string ProjectRoot = CreateProcessProjectRoot();

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

    /// <summary>
    /// Chooses this process's project root beneath <see cref="ScratchRoot"/> and arranges for it to be deleted when
    /// the test process exits.
    /// </summary>
    /// <returns>Absolute project root unique to this process.</returns>
    static string CreateProcessProjectRoot() {
        string root = Path.Combine(ScratchRoot, Guid.NewGuid().ToString("N"));
        AppDomain.CurrentDomain.ProcessExit += (sender, args) => {
            if (Directory.Exists(root)) {
                Directory.Delete(root, true);
            }
        };
        return root;
    }
}
