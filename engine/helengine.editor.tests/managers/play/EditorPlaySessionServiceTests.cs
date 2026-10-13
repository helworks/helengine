using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the editor Play session orchestration: save, cook, script build, player launch, output forwarding and stop.
/// </summary>
public sealed class EditorPlaySessionServiceTests {
    /// <summary>
    /// Player executable path handed to the service under test.
    /// </summary>
    const string PlayerExecutablePath = @"C:\player\helengine.player.exe";

    /// <summary>
    /// Platform identifier used for every Play request.
    /// </summary>
    const string PlatformId = "ps2";

    /// <summary>
    /// Authored scene path representing a scene that has been saved at least once.
    /// </summary>
    const string ScenePath = @"C:\project\assets\scenes\cube_test.hscene";

    /// <summary>
    /// Platform adapter assembly path passed through to the player.
    /// </summary>
    const string AdapterAssemblyPath = @"C:\platforms\ps2\helengine.ps2.player.dll";

    /// <summary>
    /// Platform adapter type name passed through to the player.
    /// </summary>
    const string AdapterTypeName = "helengine.ps2.player.Ps2PlayerAdapter";

    /// <summary>
    /// Target frame rate the selected platform's Play capability declares, passed through to the player.
    /// </summary>
    const double TargetFrameRate = 60d;

    /// <summary>
    /// Runtime script module root reported by the fake script builder.
    /// </summary>
    const string RuntimeModulesRootPath = @"C:\project\cache\play\modules";

    /// <summary>
    /// Ensures an unsaved scene fails immediately without saving, cooking, building or launching.
    /// </summary>
    [Fact]
    public void Play_WhenSceneNeverSaved_FailsWithoutCooking() {
        FakePlayCookRunner cookRunner = CreateSucceedingCookRunner();
        FakePlayScriptBuilder scriptBuilder = CreateSucceedingScriptBuilder();
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(cookRunner, scriptBuilder, launcher, PlayerExecutablePath);
        int saveCount = 0;

        EditorBuildExecutionResult result = service.Play(PlatformId, "  ", () => { saveCount++; return true; }, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);

        Assert.False(result.Succeeded);
        Assert.Equal("Save the scene before pressing Play.", result.Message);
        Assert.Equal(0, saveCount);
        Assert.Empty(cookRunner.CookedPlatformIds);
        Assert.Empty(launcher.Requests);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
    }

    /// <summary>
    /// Ensures a failed scene save aborts Play before the cook and returns the session to stopped.
    /// </summary>
    [Fact]
    public void Play_WhenSaveFails_FailsWithoutCooking() {
        FakePlayCookRunner cookRunner = CreateSucceedingCookRunner();
        FakePlayScriptBuilder scriptBuilder = CreateSucceedingScriptBuilder();
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(cookRunner, scriptBuilder, launcher, PlayerExecutablePath);
        List<EditorPlaySessionState> states = new List<EditorPlaySessionState>();
        service.StateChanged += states.Add;

        EditorBuildExecutionResult result = service.Play(PlatformId, ScenePath, () => false, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);

        Assert.False(result.Succeeded);
        Assert.Equal("Scene save failed; Play aborted.", result.Message);
        Assert.Empty(cookRunner.CookedPlatformIds);
        Assert.Empty(scriptBuilder.BuiltPlatformIds);
        Assert.Empty(launcher.Requests);
        Assert.Equal(new[] { EditorPlaySessionState.Preparing, EditorPlaySessionState.Stopped }, states);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
    }

    /// <summary>
    /// Ensures a failed cook stops Play before the script build and launch, surfacing the cook message.
    /// </summary>
    [Fact]
    public void Play_WhenCookFails_DoesNotBuildScriptsOrLaunch() {
        FakePlayCookRunner cookRunner = new FakePlayCookRunner(EditorPlayCookResult.Failure("Cook exploded."));
        FakePlayScriptBuilder scriptBuilder = CreateSucceedingScriptBuilder();
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(cookRunner, scriptBuilder, launcher, PlayerExecutablePath);

        EditorBuildExecutionResult result = service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);

        Assert.False(result.Succeeded);
        Assert.Equal("Cook exploded.", result.Message);
        Assert.Equal(new[] { PlatformId }, cookRunner.CookedPlatformIds);
        Assert.Empty(scriptBuilder.BuiltPlatformIds);
        Assert.Empty(launcher.Requests);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
    }

    /// <summary>
    /// Ensures a failed script build stops Play before launch, surfacing the build message.
    /// </summary>
    [Fact]
    public void Play_WhenScriptBuildFails_DoesNotLaunch() {
        FakePlayCookRunner cookRunner = CreateSucceedingCookRunner();
        FakePlayScriptBuilder scriptBuilder = new FakePlayScriptBuilder(EditorBuildExecutionResult.Failure("Scripts did not compile."), string.Empty);
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(cookRunner, scriptBuilder, launcher, PlayerExecutablePath);

        EditorBuildExecutionResult result = service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);

        Assert.False(result.Succeeded);
        Assert.Equal("Scripts did not compile.", result.Message);
        Assert.Equal(new[] { PlatformId }, scriptBuilder.BuiltPlatformIds);
        Assert.Empty(launcher.Requests);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
    }

    /// <summary>
    /// Ensures a fully successful Play launches the player with the exact argument list and leaves the session running,
    /// adding the debugger wait flag only when requested.
    /// </summary>
    [Fact]
    public void Play_WhenAllSucceed_LaunchesWithExpectedArgumentsAndIsRunning() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath);
        List<EditorPlaySessionState> states = new List<EditorPlaySessionState>();
        service.StateChanged += states.Add;

        EditorBuildExecutionResult result = service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);

        Assert.True(result.Succeeded);
        Assert.Equal("Player started (pid 1000).", result.Message);
        Assert.Equal(EditorPlaySessionState.Running, service.State);
        Assert.Equal(new[] { EditorPlaySessionState.Preparing, EditorPlaySessionState.Running }, states);
        EditorPlayLaunchRequest request = Assert.Single(launcher.Requests);
        Assert.Equal(PlayerExecutablePath, request.PlayerExecutablePath);
        string[] expectedArguments = {
            "--content-root", @"C:\project\cache\play\ps2-staging",
            "--scene-id", "scene-cube-test",
            "--scene-path", "scenes/cube_test.hscene",
            "--adapter-assembly", AdapterAssemblyPath,
            "--adapter-type", AdapterTypeName,
            "--script-modules", RuntimeModulesRootPath,
            "--target-fps", "60"
        };
        Assert.Equal(expectedArguments, request.Arguments);

        FakePlayProcessLauncher debugLauncher = new FakePlayProcessLauncher();
        EditorPlaySessionService debugService = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), debugLauncher, PlayerExecutablePath);

        debugService.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, true);

        string[] expectedDebugArguments = expectedArguments.Concat(new[] { "--wait-for-debugger" }).ToArray();
        Assert.Equal(expectedDebugArguments, Assert.Single(debugLauncher.Requests).Arguments);
    }

    /// <summary>
    /// Ensures the target frame rate taken from the platform capability reaches the player as <c>--target-fps</c>,
    /// formatted with the invariant culture even when the editor runs under a comma-decimal culture.
    /// </summary>
    [Fact]
    public void Play_PassesCapabilityTargetFrameRateAsInvariantTargetFpsArgument() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath);
        PlatformPlayerCapability capability = new PlatformPlayerCapability(true, AdapterTypeName, "ps2-staging", 59.94d, PlayerInputDevices.Gamepad);
        System.Globalization.CultureInfo previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try {
            service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, capability.AdapterTypeName, capability.TargetFrameRate, false);
        } finally {
            System.Globalization.CultureInfo.CurrentCulture = previousCulture;
        }

        IReadOnlyList<string> arguments = Assert.Single(launcher.Requests).Arguments;
        int optionIndex = arguments.ToList().IndexOf("--target-fps");
        Assert.True(optionIndex >= 0, "The player arguments must carry --target-fps.");
        Assert.Equal("59.94", arguments[optionIndex + 1]);
    }

    /// <summary>
    /// Ensures Play refuses a non-positive or non-finite target frame rate before saving, cooking or launching.
    /// </summary>
    /// <param name="targetFrameRate">Invalid frame rate handed to Play.</param>
    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Play_WhenTargetFrameRateIsNotPositive_Throws(double targetFrameRate) {
        FakePlayCookRunner cookRunner = CreateSucceedingCookRunner();
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(cookRunner, CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, targetFrameRate, false));

        Assert.Equal("targetFrameRate", exception.ParamName);
        Assert.Empty(cookRunner.CookedPlatformIds);
        Assert.Empty(launcher.Requests);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
    }

    /// <summary>
    /// Ensures pressing Play while a player runs kills the previous player before the new one starts, and the old
    /// player's late exit no longer affects the new session.
    /// </summary>
    [Fact]
    public void Play_WhenAlreadyRunning_KillsPreviousBeforeLaunching() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath);
        service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);
        FakePlayProcess firstProcess = launcher.StartedProcesses[0];
        int killCountWhenSaving = -1;

        EditorBuildExecutionResult result = service.Play(PlatformId, ScenePath, () => { killCountWhenSaving = firstProcess.KillCount; return true; }, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);
        firstProcess.RaiseExit(1);

        Assert.True(result.Succeeded);
        Assert.Equal(1, killCountWhenSaving);
        Assert.Equal(1, firstProcess.KillCount);
        Assert.Equal(2, launcher.StartedProcesses.Count);
        Assert.Equal(0, launcher.StartedProcesses[1].KillCount);
        Assert.Equal(EditorPlaySessionState.Running, service.State);
    }

    /// <summary>
    /// Ensures a previous player that cannot be terminated fails the new Play with that error, ends the session
    /// stopped instead of leaving it running without a player, and launches nothing.
    /// </summary>
    [Fact]
    public void Play_WhenKillingPreviousPlayerThrows_EndsStoppedAndRethrows() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath);
        service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);
        InvalidOperationException killFailure = new InvalidOperationException("Player refused to die.");
        launcher.StartedProcesses[0].KillFailure = killFailure;
        int saveCount = 0;

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => service.Play(PlatformId, ScenePath, () => { saveCount++; return true; }, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false));

        Assert.Same(killFailure, thrown);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
        Assert.Equal(0, saveCount);
        Assert.Single(launcher.StartedProcesses);
    }

    /// <summary>
    /// Ensures Stop ends the session stopped even when terminating the player throws, and rethrows that failure.
    /// </summary>
    [Fact]
    public void Stop_WhenKillThrows_EndsStoppedAndRethrows() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath);
        service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);
        InvalidOperationException killFailure = new InvalidOperationException("Player refused to die.");
        launcher.StartedProcesses[0].KillFailure = killFailure;

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => service.Stop());

        Assert.Same(killFailure, thrown);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
    }

    /// <summary>
    /// Ensures late events from a replaced player, delivered through delegates captured before the service
    /// unsubscribed, neither stop the new session nor log anything.
    /// </summary>
    [Fact]
    public void ProcessExit_FromReplacedPlayerAfterRelaunch_IsIgnored() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath);
        service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);
        service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);
        FakePlayProcess oldProcess = launcher.StartedProcesses[0];
        List<EditorPlaySessionState> states = new List<EditorPlaySessionState>();
        service.StateChanged += states.Add;
        List<string> loggedPlayerLines = new List<string>();
        Action<LogEntry> captureEntry = entry => {
            if (entry.Message.StartsWith("[player]", StringComparison.Ordinal)) {
                loggedPlayerLines.Add(entry.Message);
            }
        };

        Logger.MessageLogged += captureEntry;
        Logger.ErrorLogged += captureEntry;
        try {
            oldProcess.RaiseStaleEventsToEverySubscriber("stale line", 7);
        } finally {
            Logger.MessageLogged -= captureEntry;
            Logger.ErrorLogged -= captureEntry;
        }

        Assert.Equal(EditorPlaySessionState.Running, service.State);
        Assert.Empty(states);
        Assert.Empty(loggedPlayerLines);
        Assert.Equal(0, launcher.StartedProcesses[1].KillCount);
    }

    /// <summary>
    /// Ensures a player that exits before the service subscribes to it is still handled: the exit code is logged and
    /// the session ends stopped.
    /// </summary>
    [Fact]
    public void Play_WhenPlayerExitsBeforeEventsAreHooked_ReturnsToStoppedAndLogsError() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        launcher.StartAlreadyExited = true;
        launcher.AlreadyExitedCode = 4;
        EditorPlaySessionService service = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath);
        List<string> errors = new List<string>();
        Action<LogEntry> captureError = entry => errors.Add(entry.Message);

        Logger.ErrorLogged += captureError;
        try {
            service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);
        } finally {
            Logger.ErrorLogged -= captureError;
        }

        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
        Assert.Contains("[player] exited with code 4", errors);
    }

    /// <summary>
    /// Ensures Stop kills the running player and returns the session to stopped.
    /// </summary>
    [Fact]
    public void Stop_KillsProcessAndReturnsToStopped() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath);
        service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);
        List<EditorPlaySessionState> states = new List<EditorPlaySessionState>();
        service.StateChanged += states.Add;

        service.Stop();

        Assert.Equal(1, launcher.StartedProcesses[0].KillCount);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
        Assert.Equal(new[] { EditorPlaySessionState.Stopped }, states);
    }

    /// <summary>
    /// Ensures a player that exits with a non-zero code returns the session to stopped and logs the exit code, and
    /// that player output is forwarded to the editor log with the player prefix.
    /// </summary>
    [Fact]
    public void ProcessExit_WithNonZeroCode_ReturnsToStoppedAndLogsError() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath);
        service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, TargetFrameRate, false);
        FakePlayProcess process = launcher.StartedProcesses[0];
        List<string> messages = new List<string>();
        List<string> errors = new List<string>();
        Action<LogEntry> captureMessage = entry => messages.Add(entry.Message);
        Action<LogEntry> captureError = entry => errors.Add(entry.Message);

        Logger.MessageLogged += captureMessage;
        Logger.ErrorLogged += captureError;
        try {
            process.RaiseOutputLine("frame 1");
            process.RaiseErrorLine("texture missing");
            process.RaiseExit(3);
        } finally {
            Logger.MessageLogged -= captureMessage;
            Logger.ErrorLogged -= captureError;
        }

        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
        Assert.Contains("[player] frame 1", messages);
        Assert.Contains("[player] texture missing", errors);
        Assert.Contains("[player] exited with code 3", errors);
        Assert.Equal(0, process.KillCount);
    }

    /// <summary>
    /// Ensures Play availability follows the platform's player capability.
    /// </summary>
    [Fact]
    public void IsPlayAvailable_FollowsCapability() {
        EditorPlaySessionService service = new EditorPlaySessionService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), new FakePlayProcessLauncher(), PlayerExecutablePath);

        Assert.True(service.IsPlayAvailable(new PlatformPlayerCapability(true, AdapterTypeName, "ps2-staging", TargetFrameRate, PlayerInputDevices.Gamepad)));
        Assert.False(service.IsPlayAvailable(PlatformPlayerCapability.CreateUnsupported()));
    }

    /// <summary>
    /// Creates a cook runner fake that reports a successful cook with fixed content and startup scene values.
    /// </summary>
    /// <returns>Succeeding cook runner fake.</returns>
    static FakePlayCookRunner CreateSucceedingCookRunner() {
        return new FakePlayCookRunner(new EditorPlayCookResult(true, "Cooked.", @"C:\project\cache\play\ps2-staging", "scene-cube-test", "scenes/cube_test.hscene"));
    }

    /// <summary>
    /// Creates a script builder fake that reports a successful build rooted at <see cref="RuntimeModulesRootPath"/>.
    /// </summary>
    /// <returns>Succeeding script builder fake.</returns>
    static FakePlayScriptBuilder CreateSucceedingScriptBuilder() {
        return new FakePlayScriptBuilder(EditorBuildExecutionResult.Success("Scripts built."), RuntimeModulesRootPath);
    }
}
