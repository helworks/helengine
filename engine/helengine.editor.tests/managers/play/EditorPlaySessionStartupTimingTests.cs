using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies that every Play logs one startup timing summary: when the player reports its first frame, or with the exit
/// code when the player exits before presenting one.
/// </summary>
public sealed class EditorPlaySessionStartupTimingTests {
    /// <summary>
    /// Player executable path handed to the service under test.
    /// </summary>
    const string PlayerExecutablePath = @"C:\player\helengine.player.exe";

    /// <summary>
    /// Authored scene path representing a saved scene.
    /// </summary>
    const string ScenePath = @"C:\project\assets\scenes\cube_test.hscene";

    /// <summary>
    /// Ensures the first-frame marker produces the summary line, with the player-reported first frame and the total
    /// since Play started, and is not echoed as a player line.
    /// </summary>
    [Fact]
    public void FirstFrameMarker_LogsSummaryInsteadOfPlayerLine() {
        // Readings: play start, saved, cooked, scripts built, launched, first-frame marker received.
        FakePlayClock clock = new FakePlayClock(100, 100.1, 108.5, 109.75, 110.05, 112.8);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(EditorBuildExecutionResult.Success("Scripts built."), launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        try {
            launcher.StartedProcesses[0].RaiseOutputLine("first-frame 2690");
            launcher.StartedProcesses[0].RaiseExit(0);
        } finally {
            Logger.MessageLogged -= captureMessage;
        }

        Assert.Equal(new[] { "[play] save 0.1s · cook 8.4s · scripts 1.3s · launch 0.3s · first frame 2.7s · total 12.8s" }, messages);
        Assert.Equal(0, clock.RemainingReadings);
    }

    /// <summary>
    /// Ensures a script build the builder reports as skipped is tagged in the summary.
    /// </summary>
    [Fact]
    public void FirstFrameMarker_WhenScriptBuildSkipped_TagsScriptStage() {
        FakePlayClock clock = new FakePlayClock(0, 0.1, 8.5, 8.51, 8.8, 11.5);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(
            EditorBuildExecutionResult.Success(EditorPlaySessionService.ScriptBuildSkippedMessage), launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        try {
            launcher.StartedProcesses[0].RaiseOutputLine("first-frame 2700");
        } finally {
            Logger.MessageLogged -= captureMessage;
        }

        Assert.Equal(new[] { "[play] save 0.1s · cook 8.4s · scripts 0.0s (skipped) · launch 0.3s · first frame 2.7s · total 11.5s" }, messages);
    }

    /// <summary>
    /// Ensures a player that exits before presenting a frame logs the measured stages and its exit code, alongside the
    /// existing exit-code error.
    /// </summary>
    [Fact]
    public void ExitBeforeFirstFrame_LogsStagesAndExitCode() {
        FakePlayClock clock = new FakePlayClock(0, 0.2, 3.2, 7.6, 7.9);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(EditorBuildExecutionResult.Success("Scripts built."), launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        List<string> errors = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);
        Action<LogEntry> captureError = entry => errors.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        Logger.ErrorLogged += captureError;
        try {
            launcher.StartedProcesses[0].RaiseOutputLine("loading scene");
            launcher.StartedProcesses[0].RaiseExit(5);
        } finally {
            Logger.MessageLogged -= captureMessage;
            Logger.ErrorLogged -= captureError;
        }

        Assert.Equal(new[] { "[player] loading scene", "[play] save 0.2s · cook 3.0s · scripts 4.4s · launch 0.3s · no frame (exit code 5)", "[player] exited with code 5" }, messages);
        Assert.Contains("[player] exited with code 5", errors);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
        Assert.Equal(0, clock.RemainingReadings);
    }

    /// <summary>
    /// Ensures a player that already exited when the launcher returned still logs its no-frame summary.
    /// </summary>
    [Fact]
    public void ExitBeforeEventsAreHooked_LogsNoFrameSummary() {
        FakePlayClock clock = new FakePlayClock(0, 0, 1, 1, 1.5);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        launcher.StartAlreadyExited = true;
        launcher.AlreadyExitedCode = 2;
        EditorPlaySessionService service = CreateService(EditorBuildExecutionResult.Success("Scripts built."), launcher, clock);
        List<string> messages = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        try {
            StartPlay(service);
        } finally {
            Logger.MessageLogged -= captureMessage;
        }

        Assert.Equal(new[] { "[play] save 0.0s · cook 1.0s · scripts 0.0s · launch 0.5s · no frame (exit code 2)", "[player] exited with code 2" }, messages);
    }

    /// <summary>
    /// Creates the service under test with succeeding cook, the given script build result and the fake clock.
    /// </summary>
    /// <param name="scriptBuildResult">Result the fake script builder returns.</param>
    /// <param name="launcher">Fake launcher recording the started players.</param>
    /// <param name="clock">Fake clock supplying the stage readings.</param>
    /// <returns>The service under test.</returns>
    static EditorPlaySessionService CreateService(EditorBuildExecutionResult scriptBuildResult, FakePlayProcessLauncher launcher, FakePlayClock clock) {
        FakePlayCookRunner cookRunner = new FakePlayCookRunner(new EditorPlayCookResult(true, "Cooked.", @"C:\project\cache\play\ps2-staging", "scene-cube-test", "scenes/cube_test.hscene"));
        FakePlayScriptBuilder scriptBuilder = new FakePlayScriptBuilder(scriptBuildResult, @"C:\project\cache\play\modules");
        return new EditorPlaySessionService(cookRunner, scriptBuilder, launcher, PlayerExecutablePath, clock);
    }

    /// <summary>
    /// Presses Play for the PS2 platform with a saved scene.
    /// </summary>
    /// <param name="service">Service under test.</param>
    static void StartPlay(EditorPlaySessionService service) {
        EditorBuildExecutionResult result = service.Play(
            "ps2", "PlayStation 2", ScenePath, () => true,
            @"C:\platforms\ps2\helengine.ps2.player.dll", "helengine.ps2.player.Ps2PlayerAdapter",
            60d, PlayerInputDevices.Gamepad, false);
        Assert.True(result.Succeeded);
    }
}
