using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies that every Play logs one startup timing summary: when the player reports its first frame, with the exit code
/// when the player exits before presenting one, or as stopped when the editor stops it first. Unexpected marker lines
/// from the player are reported and never throw.
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
    /// Main-thread queue the service under test reports its finished preparation through.
    /// </summary>
    readonly EditorMainThreadActionQueue Queue = new EditorMainThreadActionQueue();

    /// <summary>
    /// Ensures the first-frame marker produces the summary line, with the player-reported first frame and the total
    /// since Play started, and is not echoed as a player line.
    /// </summary>
    [Fact]
    public void FirstFrameMarker_LogsSummaryInsteadOfPlayerLine() {
        // Readings: play start, saved, cooked, scripts built, launched, first-frame marker received.
        FakePlayClock clock = new FakePlayClock(100, 100.1, 108.5, 109.75, 110.05, 112.8);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(false, launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        try {
            launcher.StartedProcesses[0].RaiseOutputLine("##helengine.play first-frame-ms=2690");
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
        EditorPlaySessionService service = CreateService(true, launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        try {
            launcher.StartedProcesses[0].RaiseOutputLine("##helengine.play first-frame-ms=2700");
        } finally {
            Logger.MessageLogged -= captureMessage;
        }

        Assert.Equal(new[] { "[play] save 0.1s · cook 8.4s · scripts 0.0s (skipped) · launch 0.3s · first frame 2.7s · total 11.5s" }, messages);
    }

    /// <summary>
    /// Ensures an ordinary game or engine log line that merely starts with "first-frame" is echoed as a player line and
    /// not taken as the marker.
    /// </summary>
    [Fact]
    public void LogLineStartingWithFirstFrame_IsEchoedNotConsumed() {
        FakePlayClock clock = new FakePlayClock(0, 0.1, 1, 2, 2.5);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(false, launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        try {
            launcher.StartedProcesses[0].RaiseOutputLine("first-frame 1234");
        } finally {
            Logger.MessageLogged -= captureMessage;
        }

        Assert.Equal(new[] { "[player] first-frame 1234" }, messages);
        Assert.Equal(0, clock.RemainingReadings);
    }

    /// <summary>
    /// Ensures a second marker after the summary is reported as an editor error and ignored, without throwing.
    /// </summary>
    [Fact]
    public void DuplicateFirstFrameMarker_IsReportedAndIgnored() {
        FakePlayClock clock = new FakePlayClock(0, 0.1, 1, 2, 2.5, 4);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(false, launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        List<string> errors = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);
        Action<LogEntry> captureError = entry => errors.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        Logger.ErrorLogged += captureError;
        try {
            launcher.StartedProcesses[0].RaiseOutputLine("##helengine.play first-frame-ms=1500");
            launcher.StartedProcesses[0].RaiseOutputLine("##helengine.play first-frame-ms=1600");
        } finally {
            Logger.MessageLogged -= captureMessage;
            Logger.ErrorLogged -= captureError;
        }

        Assert.Single(messages, message => message.StartsWith("[play] save", StringComparison.Ordinal));
        Assert.Contains("[play] save 0.1s · cook 0.9s · scripts 1.0s · launch 0.5s · first frame 1.5s · total 4.0s", messages);
        Assert.Equal(new[] { "[play] ignored unexpected first-frame marker: ##helengine.play first-frame-ms=1600" }, errors);
        Assert.Equal(EditorPlaySessionState.Running, service.State);
    }

    /// <summary>
    /// Ensures a malformed marker is reported and ignored, and the following well-formed marker still logs the summary.
    /// </summary>
    [Fact]
    public void MalformedFirstFrameMarker_IsReportedAndLaterValidMarkerWins() {
        FakePlayClock clock = new FakePlayClock(0, 0.1, 1, 2, 2.5, 4);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(false, launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        List<string> errors = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);
        Action<LogEntry> captureError = entry => errors.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        Logger.ErrorLogged += captureError;
        try {
            launcher.StartedProcesses[0].RaiseOutputLine("##helengine.play first-frame-ms=abc");
            launcher.StartedProcesses[0].RaiseOutputLine("##helengine.play first-frame-ms=-1");
            launcher.StartedProcesses[0].RaiseOutputLine("##helengine.play first-frame-ms=1500");
        } finally {
            Logger.MessageLogged -= captureMessage;
            Logger.ErrorLogged -= captureError;
        }

        Assert.Equal(
            new[] {
                "[play] ignored unexpected first-frame marker: ##helengine.play first-frame-ms=abc",
                "[play] ignored unexpected first-frame marker: ##helengine.play first-frame-ms=-1"
            },
            errors);
        Assert.Contains("[play] save 0.1s · cook 0.9s · scripts 1.0s · launch 0.5s · first frame 1.5s · total 4.0s", messages);
    }

    /// <summary>
    /// Ensures a marker delivered after the player exited is ignored: the no-frame summary stands and nothing throws.
    /// </summary>
    [Fact]
    public void FirstFrameMarkerAfterExit_IsIgnored() {
        FakePlayClock clock = new FakePlayClock(0, 0.1, 1, 2, 2.5);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(false, launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        try {
            launcher.StartedProcesses[0].RaiseExit(0);
            launcher.StartedProcesses[0].RaiseStaleEventsToEverySubscriber("##helengine.play first-frame-ms=1500", 0);
        } finally {
            Logger.MessageLogged -= captureMessage;
        }

        Assert.Equal(new[] { "[play] save 0.1s · cook 0.9s · scripts 1.0s · launch 0.5s · no frame (exit code 0)" }, messages);
        Assert.Equal(0, clock.RemainingReadings);
    }

    /// <summary>
    /// Ensures a player that exits before presenting a frame logs the measured stages and its exit code, alongside the
    /// existing exit-code error.
    /// </summary>
    [Fact]
    public void ExitBeforeFirstFrame_LogsStagesAndExitCode() {
        FakePlayClock clock = new FakePlayClock(0, 0.2, 3.2, 7.6, 7.9);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(false, launcher, clock);
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
        EditorPlaySessionService service = CreateService(false, launcher, clock);
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
    /// Ensures stopping the player before its first frame logs the measured stages and "no frame (stopped)" once.
    /// </summary>
    [Fact]
    public void StopBeforeFirstFrame_LogsStoppedSummary() {
        FakePlayClock clock = new FakePlayClock(0, 0.1, 1, 2, 2.5);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(false, launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        try {
            service.Stop();
            launcher.StartedProcesses[0].RaiseStaleEventsToEverySubscriber("##helengine.play first-frame-ms=1500", 1);
        } finally {
            Logger.MessageLogged -= captureMessage;
        }

        Assert.Equal(new[] { "[play] save 0.1s · cook 0.9s · scripts 1.0s · launch 0.5s · no frame (stopped)" }, messages);
        Assert.Equal(1, launcher.StartedProcesses[0].KillCount);
    }

    /// <summary>
    /// Ensures stopping a player that already presented its first frame logs no second summary.
    /// </summary>
    [Fact]
    public void StopAfterFirstFrame_LogsNoSecondSummary() {
        FakePlayClock clock = new FakePlayClock(0, 0.1, 1, 2, 2.5, 4);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(false, launcher, clock);
        StartPlay(service);
        List<string> messages = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        try {
            launcher.StartedProcesses[0].RaiseOutputLine("##helengine.play first-frame-ms=1500");
            service.Stop();
        } finally {
            Logger.MessageLogged -= captureMessage;
        }

        Assert.Equal(new[] { "[play] save 0.1s · cook 0.9s · scripts 1.0s · launch 0.5s · first frame 1.5s · total 4.0s" }, messages);
    }

    /// <summary>
    /// Creates the service under test with a succeeding cook and script build and the fake clock.
    /// </summary>
    /// <param name="compileSkipped">Whether the fake script build reports that the compile was skipped.</param>
    /// <param name="launcher">Fake launcher recording the started players.</param>
    /// <param name="clock">Fake clock supplying the stage readings.</param>
    /// <returns>The service under test.</returns>
    EditorPlaySessionService CreateService(bool compileSkipped, FakePlayProcessLauncher launcher, FakePlayClock clock) {
        FakePlayCookRunner cookRunner = new FakePlayCookRunner(new EditorPlayCookResult(true, "Cooked.", @"C:\project\cache\play\ps2-staging", "scene-cube-test", "scenes/cube_test.hscene", new Dictionary<string, string>()));
        FakePlayScriptBuilder scriptBuilder = new FakePlayScriptBuilder(EditorBuildExecutionResult.Success("Scripts built."), @"C:\project\cache\play\modules");
        scriptBuilder.CompileSkipped = compileSkipped;
        return new EditorPlaySessionService(cookRunner, scriptBuilder, launcher, PlayerExecutablePath, Queue, new InlinePlayPreparationScheduler(), TestPlaySettings.CreateWriter(), clock);
    }

    /// <summary>
    /// Presses Play for the PS2 platform with a saved scene, then drains the main-thread queue the way the next editor
    /// frame does, which launches the player.
    /// </summary>
    /// <param name="service">Service under test.</param>
    void StartPlay(EditorPlaySessionService service) {
        EditorBuildExecutionResult launchResult = null;
        EditorBuildExecutionResult result = service.Play(
            "ps2", "PlayStation 2", ScenePath, () => true,
            @"C:\platforms\ps2\helengine.ps2.player.dll", "helengine.ps2.player.Ps2PlayerAdapter",
            PlayerInputDevices.Gamepad, "ps2", "1.0.0", TestPlaySettings.CreateDefaults("ps2", 60d, false), completed => launchResult = completed);
        Assert.True(result.Succeeded);
        Queue.ProcessPending();
        Assert.NotNull(launchResult);
        Assert.True(launchResult.Succeeded);
    }
}
