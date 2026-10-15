using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;
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
    /// Platform display name used for every Play request, as the Play platform selector shows it.
    /// </summary>
    const string PlatformDisplayName = "PlayStation 2";

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
    /// Platform name the PS2 native runtime reports to the core; Play hands it to the player so the player reports it too.
    /// </summary>
    const string RuntimePlatformName = "ps2";

    /// <summary>
    /// Platform version the PS2 native runtime reports to the core; Play hands it to the player so the player reports it too.
    /// </summary>
    const string RuntimePlatformVersion = "1.0.0";

    /// <summary>
    /// Input devices handed to Play, matching the PS2 capability (pad only).
    /// </summary>
    const PlayerInputDevices InputDevices = PlayerInputDevices.Gamepad;

    /// <summary>
    /// Runtime script module root reported by the fake script builder.
    /// </summary>
    const string RuntimeModulesRootPath = @"C:\project\cache\play\modules";

    /// <summary>
    /// Main-thread queue every service under test reports its finished preparation through; the test drains it the way
    /// the editor frame does.
    /// </summary>
    readonly EditorMainThreadActionQueue Queue = new EditorMainThreadActionQueue();

    /// <summary>
    /// Ensures an unsaved scene fails immediately without saving, cooking, building or launching.
    /// </summary>
    [Fact]
    public void Play_WhenSceneNeverSaved_FailsWithoutCooking() {
        FakePlayCookRunner cookRunner = CreateSucceedingCookRunner();
        FakePlayScriptBuilder scriptBuilder = CreateSucceedingScriptBuilder();
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(cookRunner, scriptBuilder, launcher);
        int saveCount = 0;

        EditorBuildExecutionResult result = PlayToCompletion(service, PlatformId, PlatformDisplayName, "  ", () => { saveCount++; return true; }, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));

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
        EditorPlaySessionService service = CreateService(cookRunner, scriptBuilder, launcher);
        List<EditorPlaySessionState> states = new List<EditorPlaySessionState>();
        service.StateChanged += states.Add;

        EditorBuildExecutionResult result = PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => false, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));

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
        EditorPlaySessionService service = CreateService(cookRunner, scriptBuilder, launcher);

        EditorBuildExecutionResult result = PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));

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
        EditorPlaySessionService service = CreateService(cookRunner, scriptBuilder, launcher);

        EditorBuildExecutionResult result = PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));

        Assert.False(result.Succeeded);
        Assert.Equal("Scripts did not compile.", result.Message);
        Assert.Equal(new[] { PlatformId }, scriptBuilder.BuiltPlatformIds);
        Assert.Empty(launcher.Requests);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
    }

    /// <summary>
    /// Ensures a fully successful Play launches the player with the exact argument list (one --play-settings file instead
    /// of the old frame-rate, shadow and debugger flags) and leaves the session running.
    /// </summary>
    [Fact]
    public void Play_WhenAllSucceed_LaunchesWithExpectedArgumentsAndIsRunning() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        List<EditorPlaySessionState> states = new List<EditorPlaySessionState>();
        service.StateChanged += states.Add;

        EditorBuildExecutionResult result = PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));

        Assert.True(result.Succeeded);
        Assert.Equal(EditorPlaySessionState.Running, service.State);
        Assert.Equal(new[] { EditorPlaySessionState.Preparing, EditorPlaySessionState.Running }, states);
        EditorPlayLaunchRequest request = Assert.Single(launcher.Requests);
        string settingsPath = TestPlaySettings.CreateWriter().ResolvePath(PlatformId);
        string[] expectedArguments = {
            "--content-root", @"C:\project\cache\play\ps2-staging",
            "--scene-id", "scene-cube-test",
            "--platform-name", PlatformDisplayName,
            "--scene-path", "scenes/cube_test.hscene",
            "--adapter-assembly", AdapterAssemblyPath,
            "--adapter-type", AdapterTypeName,
            "--script-modules", RuntimeModulesRootPath,
            "--input-devices", "gamepad",
            "--runtime-platform-name", RuntimePlatformName,
            "--runtime-platform-version", RuntimePlatformVersion,
            "--play-settings", settingsPath
        };
        Assert.Equal(expectedArguments, request.Arguments);
        PlayerPlaySettings written = PlayerPlaySettingsFile.Read(settingsPath);
        Assert.Equal(TargetFrameRate, written.TargetFrameRate);
        Assert.False(written.WaitForDebugger);
    }

    /// <summary>
    /// The settings captured when Play was pressed are the ones the player receives, even when a newer snapshot exists by
    /// the time the preparation launches (an edit during preparation applies to the next Play).
    /// </summary>
    [Fact]
    public void Play_UsesSettingsCapturedWhenPressed() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        EditorPlaySettingsSnapshot pressed = TestPlaySettings.CreateDefaults(PlatformId, 60d, true);
        EditorBuildExecutionResult launchResult = null;

        service.Play(PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, RuntimePlatformName, RuntimePlatformVersion, pressed, result => launchResult = result);
        EditorPlaySettingsSnapshot edited = TestPlaySettings.CreateDefaults(PlatformId, 30d, false);
        Queue.ProcessPending();

        Assert.True(launchResult.Succeeded, launchResult.Message);
        PlayerPlaySettings written = PlayerPlaySettingsFile.Read(TestPlaySettings.CreateWriter().ResolvePath(PlatformId));
        Assert.True(written.WaitForDebugger);
        Assert.Equal(60d, written.TargetFrameRate);
        Assert.NotSame(pressed, edited);
    }

    /// <summary>
    /// Press-time capture against the real per-user store: Play captures <c>play.json</c> when it is pressed, so a value
    /// saved to the file while that Play is still preparing does not reach it (the player gets the pressed value), and
    /// the next Play captures the saved edit.
    /// </summary>
    [Fact]
    public void Play_WhenPlaySettingsAreSavedWhilePreparing_LaunchesPressTimeValuesAndNextPlayUsesTheEdit() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(Path.Combine(TestPlaySettings.ProjectRoot, "press-time-store"));
        PlatformPlayerCapability capability = new PlatformPlayerCapability(true, AdapterTypeName, "ps2-staging", TargetFrameRate, InputDevices, RuntimePlatformName, RuntimePlatformVersion, []);
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), new FakePlayProcessLauncher());
        string settingsPath = TestPlaySettings.CreateWriter().ResolvePath(PlatformId);
        EditorPlaySettingsDocument pressedDocument = new EditorPlaySettingsDocument();
        pressedDocument.CommonValues["window-scale"] = "2x";
        store.Save(pressedDocument);
        Assert.True(EditorPlaySettingsCapture.TryCapture(store, PlatformId, capability, null, false, 4242, out EditorPlaySettingsSnapshot pressed, out string pressedFailure), pressedFailure);
        EditorBuildExecutionResult firstLaunch = null;

        EditorBuildExecutionResult firstStart = service.Play(PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, RuntimePlatformName, RuntimePlatformVersion, pressed, result => firstLaunch = result);
        EditorPlaySettingsDocument editedDocument = new EditorPlaySettingsDocument();
        editedDocument.CommonValues["window-scale"] = "3x";
        store.Save(editedDocument);
        Queue.ProcessPending();

        Assert.True(firstStart.Succeeded, firstStart.Message);
        Assert.True(firstLaunch.Succeeded, firstLaunch.Message);
        Assert.Equal(PlayerWindowScale.Two, PlayerPlaySettingsFile.Read(settingsPath).WindowScale);

        Assert.True(EditorPlaySettingsCapture.TryCapture(store, PlatformId, capability, null, false, 4242, out EditorPlaySettingsSnapshot next, out string nextFailure), nextFailure);
        EditorBuildExecutionResult secondLaunch = PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, next);

        Assert.True(secondLaunch.Succeeded, secondLaunch.Message);
        Assert.Equal(PlayerWindowScale.Three, PlayerPlaySettingsFile.Read(settingsPath).WindowScale);
    }

    /// <summary>
    /// A cook whose platform turns shadows off and that produced a runtime settings catalog launches the configured
    /// player executable, forwards the catalog as the trailing <c>--runtime-settings</c> pair, and writes shadows off
    /// even though the Play setting <c>shadows</c> defaults to on (Play can only turn shadows off).
    /// </summary>
    [Fact]
    public void Play_WhenCookDisablesShadowsAndHasRuntimeSettings_ForwardsCatalogAndWritesShadowsOff() {
        FakePlayCookRunner cookRunner = new FakePlayCookRunner(new EditorPlayCookResult(true, "Cooked.", @"C:\project\cache\play\ps2-staging", "scene-cube-test", "scenes/cube_test.hscene", new Dictionary<string, string>(), false, "C:/settings/catalog.bin"));
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(cookRunner, CreateSucceedingScriptBuilder(), launcher);

        EditorBuildExecutionResult result = PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));

        Assert.True(result.Succeeded, result.Message);
        EditorPlayLaunchRequest request = Assert.Single(launcher.Requests);
        Assert.Equal(PlayerExecutablePath, request.PlayerExecutablePath);
        string settingsPath = TestPlaySettings.CreateWriter().ResolvePath(PlatformId);
        Assert.Equal(new[] { "--play-settings", settingsPath, "--runtime-settings", "C:/settings/catalog.bin" }, request.Arguments.Skip(request.Arguments.Count - 4).ToArray());
        Assert.False(PlayerPlaySettingsFile.Read(settingsPath).ShadowsEnabled);
    }

    /// <summary>A settings snapshot taken for another platform is rejected before anything runs.</summary>
    [Fact]
    public void Play_WhenSnapshotIsForAnotherPlatform_Throws() {
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), new FakePlayProcessLauncher());

        Assert.Throws<ArgumentException>(() => service.Play(PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, RuntimePlatformName, RuntimePlatformVersion, TestPlaySettings.CreateDefaults("ds", 60d, false), result => { }));
    }

    /// <summary>
    /// Ensures the input devices declared by the platform capability reach the player as <c>--input-devices</c>, written
    /// as the lowercase comma list in declaration order.
    /// </summary>
    [Fact]
    public void Play_PassesCapabilityInputDevicesAsInputDevicesArgument() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        PlatformPlayerCapability capability = new PlatformPlayerCapability(true, AdapterTypeName, "ps2-staging", 60d, PlayerInputDevices.Gamepad | PlayerInputDevices.Keyboard, "ps2", "1.0.0", []);

        PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, capability.AdapterTypeName, capability.InputDevices, TestPlaySettings.CreateDefaults(PlatformId, capability.TargetFrameRate, false));

        IReadOnlyList<string> arguments = Assert.Single(launcher.Requests).Arguments;
        int optionIndex = arguments.ToList().IndexOf("--input-devices");
        Assert.True(optionIndex >= 0, "The player arguments must carry --input-devices.");
        Assert.Equal("keyboard,gamepad", arguments[optionIndex + 1]);
    }

    /// <summary>
    /// Ensures Play refuses an empty input device set before saving, cooking or launching, because a player without any
    /// device could not be controlled.
    /// </summary>
    [Fact]
    public void Play_WhenInputDevicesAreNone_Throws() {
        FakePlayCookRunner cookRunner = CreateSucceedingCookRunner();
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(cookRunner, CreateSucceedingScriptBuilder(), launcher);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, PlayerInputDevices.None, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false)));

        Assert.Equal("inputDevices", exception.ParamName);
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
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));
        FakePlayProcess firstProcess = launcher.StartedProcesses[0];
        int killCountWhenSaving = -1;

        EditorBuildExecutionResult result = PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => { killCountWhenSaving = firstProcess.KillCount; return true; }, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));
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
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));
        InvalidOperationException killFailure = new InvalidOperationException("Player refused to die.");
        launcher.StartedProcesses[0].KillFailure = killFailure;
        int saveCount = 0;

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => { saveCount++; return true; }, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false)));

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
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));
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
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));
        PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));
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
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        List<string> errors = new List<string>();
        Action<LogEntry> captureError = entry => errors.Add(entry.Message);

        Logger.ErrorLogged += captureError;
        try {
            PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));
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
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));
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
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));
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
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), new FakePlayProcessLauncher());

        Assert.True(service.IsPlayAvailable(new PlatformPlayerCapability(true, AdapterTypeName, "ps2-staging", TargetFrameRate, PlayerInputDevices.Gamepad, "ps2", "1.0.0", [])));
        Assert.False(service.IsPlayAvailable(PlatformPlayerCapability.CreateUnsupported()));
    }

    /// <summary>
    /// Creates the service under test with an inline preparation scheduler, reporting through <see cref="Queue"/>.
    /// </summary>
    /// <param name="cookRunner">Cook step fake.</param>
    /// <param name="scriptBuilder">Script build step fake.</param>
    /// <param name="launcher">Process launcher fake.</param>
    /// <returns>The service under test.</returns>
    EditorPlaySessionService CreateService(IEditorPlayCookRunner cookRunner, IEditorPlayScriptBuilder scriptBuilder, FakePlayProcessLauncher launcher) {
        return new EditorPlaySessionService(cookRunner, scriptBuilder, launcher, PlayerExecutablePath, Queue, new InlinePlayPreparationScheduler(), TestPlaySettings.CreateWriter());
    }

    /// <summary>
    /// Ensures Play refuses a blank runtime platform name or version before saving, because the player could not report
    /// the platform identity the native runtime reports.
    /// </summary>
    /// <param name="runtimePlatformName">Runtime platform name handed to Play.</param>
    /// <param name="runtimePlatformVersion">Runtime platform version handed to Play.</param>
    /// <param name="expectedParameterName">Parameter the rejection must name.</param>
    [Theory]
    [InlineData(" ", "1.0.0", "runtimePlatformName")]
    [InlineData("ps2", "", "runtimePlatformVersion")]
    public void Play_WhenRuntimePlatformIdentityBlank_ThrowsBeforeSaving(string runtimePlatformName, string runtimePlatformVersion, string expectedParameterName) {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        int saveCount = 0;

        ArgumentException exception = Assert.Throws<ArgumentException>(() => service.Play(
            PlatformId, PlatformDisplayName, ScenePath, () => { saveCount++; return true; }, AdapterAssemblyPath, AdapterTypeName,
            InputDevices, runtimePlatformName, runtimePlatformVersion, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false), result => { }));

        Assert.Equal(expectedParameterName, exception.ParamName);
        Assert.Equal(0, saveCount);
        Assert.Empty(launcher.Requests);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
    }

    /// <summary>
    /// Presses Play and, when the preparation started, drains the main-thread queue the way the next editor frame does
    /// and returns the launch outcome the completion reported; otherwise returns the immediate failure.
    /// </summary>
    /// <param name="service">Service under test.</param>
    /// <param name="platformId">Platform handed to Play.</param>
    /// <param name="platformDisplayName">Platform display name handed to Play.</param>
    /// <param name="scenePath">Saved scene path handed to Play.</param>
    /// <param name="saveScene">Scene save callback handed to Play.</param>
    /// <param name="adapterAssemblyPath">Adapter assembly handed to Play.</param>
    /// <param name="adapterTypeName">Adapter type handed to Play.</param>
    /// <param name="inputDevices">Input devices handed to Play.</param>
    /// <param name="playSettings">Play settings snapshot handed to Play, as captured when Play was pressed.</param>
    /// <returns>The launch outcome, or the immediate failure when Play did not start a preparation.</returns>
    EditorBuildExecutionResult PlayToCompletion(
        EditorPlaySessionService service,
        string platformId,
        string platformDisplayName,
        string scenePath,
        Func<bool> saveScene,
        string adapterAssemblyPath,
        string adapterTypeName,
        PlayerInputDevices inputDevices,
        EditorPlaySettingsSnapshot playSettings) {
        EditorBuildExecutionResult launchResult = null;
        EditorBuildExecutionResult startResult = service.Play(platformId, platformDisplayName, scenePath, saveScene, adapterAssemblyPath, adapterTypeName, inputDevices, RuntimePlatformName, RuntimePlatformVersion, playSettings, result => launchResult = result);
        if (!startResult.Succeeded) {
            return startResult;
        }

        Assert.Null(launchResult);
        Queue.ProcessPending();
        Assert.NotNull(launchResult);
        return launchResult;
    }

    /// <summary>
    /// Creates a cook runner fake that reports a successful cook with fixed content and startup scene values.
    /// </summary>
    /// <returns>Succeeding cook runner fake.</returns>
    static FakePlayCookRunner CreateSucceedingCookRunner() {
        return new FakePlayCookRunner(new EditorPlayCookResult(true, "Cooked.", @"C:\project\cache\play\ps2-staging", "scene-cube-test", "scenes/cube_test.hscene", new Dictionary<string, string>()));
    }

    /// <summary>
    /// Creates a script builder fake that reports a successful build rooted at <see cref="RuntimeModulesRootPath"/>.
    /// </summary>
    /// <returns>Succeeding script builder fake.</returns>
    static FakePlayScriptBuilder CreateSucceedingScriptBuilder() {
        return new FakePlayScriptBuilder(EditorBuildExecutionResult.Success("Scripts built."), RuntimeModulesRootPath);
    }
}
