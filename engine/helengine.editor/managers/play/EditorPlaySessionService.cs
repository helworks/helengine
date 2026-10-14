using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Orchestrates editor Play: saves the current scene, cooks content, builds the runtime scripts, launches the
    /// separate desktop player and forwards its console output to the editor log. Stop terminates the player.
    /// The concrete cook, script build and process launch are supplied through small interfaces so the sequence can
    /// be verified without running real builds or processes.
    /// </summary>
    public sealed class EditorPlaySessionService {
        /// <summary>
        /// Player flag naming the absolute root of the staged cooked content.
        /// </summary>
        public const string ContentRootOption = "--content-root";

        /// <summary>
        /// Player flag naming the identifier of the startup scene.
        /// </summary>
        public const string SceneIdOption = "--scene-id";

        /// <summary>
        /// Player flag naming the display name of the platform being played, as the Play platform selector shows it;
        /// the player puts it in square brackets in its window title.
        /// </summary>
        public const string PlatformNameOption = "--platform-name";

        /// <summary>
        /// Player flag naming the startup scene's path relative to the cooked content layout.
        /// </summary>
        public const string ScenePathOption = "--scene-path";

        /// <summary>
        /// Player flag naming the assembly that holds the platform's player adapter.
        /// </summary>
        public const string AdapterAssemblyOption = "--adapter-assembly";

        /// <summary>
        /// Player flag naming the platform's player adapter type.
        /// </summary>
        public const string AdapterTypeOption = "--adapter-type";

        /// <summary>
        /// Player flag naming the directory of built runtime script modules.
        /// </summary>
        public const string ScriptModulesOption = "--script-modules";

        /// <summary>
        /// Player flag naming the frames per second the player paces its frame loop to, taken from the platform's
        /// Play capability and written with the invariant culture.
        /// </summary>
        public const string TargetFpsOption = "--target-fps";

        /// <summary>
        /// Player flag naming the input devices the platform's hardware offers, taken from the platform's Play capability
        /// and written as the lowercase comma list of <see cref="PlayerInputDevicesText"/> (PS2: <c>gamepad</c>).
        /// </summary>
        public const string InputDevicesOption = "--input-devices";

        /// <summary>
        /// Player flag asking the player to wait for a debugger before starting the scene.
        /// </summary>
        public const string WaitForDebuggerOption = "--wait-for-debugger";

        /// <summary>
        /// Prefix applied to every editor log line that originates from the player.
        /// </summary>
        const string PlayerLogPrefix = "[player] ";

        /// <summary>
        /// Prefix of the editor error logged when the player writes a first-frame marker that is malformed or arrives
        /// after the startup summary was already logged.
        /// </summary>
        const string UnexpectedFirstFrameMarkerPrefix = "[play] ignored unexpected first-frame marker: ";

        /// <summary>
        /// Runs the assets-only cook for the requested platform.
        /// </summary>
        readonly IEditorPlayCookRunner CookRunner;

        /// <summary>
        /// Builds the runtime script modules for the requested platform.
        /// </summary>
        readonly IEditorPlayScriptBuilder ScriptBuilder;

        /// <summary>
        /// Starts the player process.
        /// </summary>
        readonly IEditorPlayProcessLauncher Launcher;

        /// <summary>
        /// Absolute path of the desktop player executable.
        /// </summary>
        readonly string PlayerExecutablePath;

        /// <summary>
        /// Monotonic clock timing the Play stages and the wait for the player's first frame.
        /// </summary>
        readonly IEditorPlayClock Clock;

        /// <summary>
        /// Binding to the player process started by the latest successful Play, or null when none is attached.
        /// Events from any other binding are stale and ignored.
        /// </summary>
        EditorPlayProcessBinding CurrentBinding;

        /// <summary>
        /// Initializes the Play session service.
        /// </summary>
        /// <param name="cookRunner">Runs the assets-only cook.</param>
        /// <param name="scriptBuilder">Builds the runtime script modules.</param>
        /// <param name="launcher">Starts the player process.</param>
        /// <param name="playerExecutablePath">Absolute path of the desktop player executable.</param>
        /// <exception cref="ArgumentNullException">Thrown when any collaborator is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the player executable path is blank.</exception>
        public EditorPlaySessionService(
            IEditorPlayCookRunner cookRunner,
            IEditorPlayScriptBuilder scriptBuilder,
            IEditorPlayProcessLauncher launcher,
            string playerExecutablePath)
            : this(cookRunner, scriptBuilder, launcher, playerExecutablePath, new EditorPlayStopwatchClock()) {
        }

        /// <summary>
        /// Initializes the Play session service with an explicit clock, so tests can time the Play stages with fixed
        /// readings.
        /// </summary>
        /// <param name="cookRunner">Runs the assets-only cook.</param>
        /// <param name="scriptBuilder">Builds the runtime script modules.</param>
        /// <param name="launcher">Starts the player process.</param>
        /// <param name="playerExecutablePath">Absolute path of the desktop player executable.</param>
        /// <param name="clock">Monotonic clock timing the Play stages for the startup summary.</param>
        /// <exception cref="ArgumentNullException">Thrown when any collaborator or the clock is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the player executable path is blank.</exception>
        internal EditorPlaySessionService(
            IEditorPlayCookRunner cookRunner,
            IEditorPlayScriptBuilder scriptBuilder,
            IEditorPlayProcessLauncher launcher,
            string playerExecutablePath,
            IEditorPlayClock clock) {
            if (string.IsNullOrWhiteSpace(playerExecutablePath)) {
                throw new ArgumentException("The Play session requires the player executable path.", nameof(playerExecutablePath));
            }

            CookRunner = cookRunner ?? throw new ArgumentNullException(nameof(cookRunner));
            ScriptBuilder = scriptBuilder ?? throw new ArgumentNullException(nameof(scriptBuilder));
            Launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            PlayerExecutablePath = playerExecutablePath;
            State = EditorPlaySessionState.Stopped;
        }

        /// <summary>
        /// Raised whenever <see cref="State"/> changes, with the new state.
        /// </summary>
        public event Action<EditorPlaySessionState> StateChanged;

        /// <summary>
        /// Gets the current Play session phase.
        /// </summary>
        public EditorPlaySessionState State { get; private set; }

        /// <summary>
        /// Reports whether the platform described by the capability can be played in the editor.
        /// </summary>
        /// <param name="capability">Player capability declared by the active platform.</param>
        /// <returns>True when the platform opts in to editor Play.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the capability is null.</exception>
        public bool IsPlayAvailable(PlatformPlayerCapability capability) {
            if (capability == null) {
                throw new ArgumentNullException(nameof(capability));
            }

            return capability.SupportsPlay;
        }

        /// <summary>
        /// Runs the full Play sequence: kill any running player, save the scene, cook, build scripts and launch the player.
        /// Every failure, including a failure to terminate the previous player, returns the session to
        /// <see cref="EditorPlaySessionState.Stopped"/>.
        /// </summary>
        /// <param name="platformId">Identifier of the platform whose content and scripts are prepared.</param>
        /// <param name="platformDisplayName">Display name of the platform, shown in the player window title.</param>
        /// <param name="currentScenePath">Saved path of the scene open in the editor; blank when it was never saved.</param>
        /// <param name="saveScene">Saves the open scene and returns whether the save succeeded.</param>
        /// <param name="adapterAssemblyPath">Path of the assembly holding the platform's player adapter.</param>
        /// <param name="adapterTypeName">Type name of the platform's player adapter.</param>
        /// <param name="targetFrameRate">Frames per second the platform's Play capability asks the player to run at.</param>
        /// <param name="inputDevices">Input devices the platform's Play capability declares; the player forwards only these to the game.</param>
        /// <param name="waitForDebugger">Whether the player should wait for a debugger before starting the scene.</param>
        /// <returns>Success naming the started player's process id, or the failure that stopped Play.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the platform, platform display name, adapter assembly or adapter type is blank.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the target frame rate is not a positive, finite number, or the input device set is empty or names
        /// unknown devices.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown when the save callback is null.</exception>
        public EditorBuildExecutionResult Play(
            string platformId,
            string platformDisplayName,
            string currentScenePath,
            Func<bool> saveScene,
            string adapterAssemblyPath,
            string adapterTypeName,
            double targetFrameRate,
            PlayerInputDevices inputDevices,
            bool waitForDebugger) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Play requires a platform identifier.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(platformDisplayName)) {
                throw new ArgumentException("Play requires the platform display name.", nameof(platformDisplayName));
            } else if (saveScene == null) {
                throw new ArgumentNullException(nameof(saveScene));
            } else if (string.IsNullOrWhiteSpace(adapterAssemblyPath)) {
                throw new ArgumentException("Play requires the player adapter assembly path.", nameof(adapterAssemblyPath));
            } else if (string.IsNullOrWhiteSpace(adapterTypeName)) {
                throw new ArgumentException("Play requires the player adapter type name.", nameof(adapterTypeName));
            } else if (!(targetFrameRate > 0d && double.IsFinite(targetFrameRate))) {
                throw new ArgumentOutOfRangeException(nameof(targetFrameRate), targetFrameRate, "Play requires a positive, finite player target frame rate.");
            } else if (inputDevices == PlayerInputDevices.None || (inputDevices & ~PlayerInputDevicesText.AllDevices) != PlayerInputDevices.None) {
                throw new ArgumentOutOfRangeException(nameof(inputDevices), inputDevices, "Play requires at least one known player input device.");
            }

            if (string.IsNullOrWhiteSpace(currentScenePath)) {
                return EditorBuildExecutionResult.Failure("Save the scene before pressing Play.");
            }

            SetState(EditorPlaySessionState.Preparing);
            try {
                KillCurrentProcess();
                return PrepareAndLaunch(platformId, platformDisplayName, saveScene, adapterAssemblyPath, adapterTypeName, targetFrameRate, inputDevices, waitForDebugger);
            } catch {
                SetState(EditorPlaySessionState.Stopped);
                throw;
            }
        }

        /// <summary>
        /// Terminates the running player, if any, and returns the session to <see cref="EditorPlaySessionState.Stopped"/>.
        /// The session ends stopped even when terminating the player fails; that failure is still rethrown.
        /// </summary>
        public void Stop() {
            try {
                KillCurrentProcess();
            } finally {
                SetState(EditorPlaySessionState.Stopped);
            }
        }

        /// <summary>
        /// Performs the save, cook, script build and launch steps of Play once the session is preparing.
        /// </summary>
        /// <param name="platformId">Identifier of the platform whose content and scripts are prepared.</param>
        /// <param name="platformDisplayName">Display name of the platform, shown in the player window title.</param>
        /// <param name="saveScene">Saves the open scene and returns whether the save succeeded.</param>
        /// <param name="adapterAssemblyPath">Path of the assembly holding the platform's player adapter.</param>
        /// <param name="adapterTypeName">Type name of the platform's player adapter.</param>
        /// <param name="targetFrameRate">Frames per second the player paces its frame loop to.</param>
        /// <param name="inputDevices">Input devices the player forwards to the game.</param>
        /// <param name="waitForDebugger">Whether the player should wait for a debugger.</param>
        /// <returns>Success naming the started player's process id, or the failure that stopped Play.</returns>
        EditorBuildExecutionResult PrepareAndLaunch(
            string platformId,
            string platformDisplayName,
            Func<bool> saveScene,
            string adapterAssemblyPath,
            string adapterTypeName,
            double targetFrameRate,
            PlayerInputDevices inputDevices,
            bool waitForDebugger) {
            TimeSpan playStartedAt = Clock.Now;
            if (!saveScene()) {
                SetState(EditorPlaySessionState.Stopped);
                return EditorBuildExecutionResult.Failure("Scene save failed; Play aborted.");
            }

            TimeSpan savedAt = Clock.Now;
            EditorPlayCookResult cookResult = CookRunner.Cook(platformId);
            TimeSpan cookedAt = Clock.Now;
            if (cookResult == null) {
                throw new InvalidOperationException("The Play cook runner returned no result.");
            } else if (!cookResult.Succeeded) {
                SetState(EditorPlaySessionState.Stopped);
                return EditorBuildExecutionResult.Failure(cookResult.Message);
            }

            EditorBuildExecutionResult buildResult = ScriptBuilder.Build(platformId, out string runtimeModulesRootPath, out bool scriptCompileSkipped);
            TimeSpan scriptsBuiltAt = Clock.Now;
            if (buildResult == null) {
                throw new InvalidOperationException("The Play script builder returned no result.");
            } else if (!buildResult.Succeeded) {
                SetState(EditorPlaySessionState.Stopped);
                return buildResult;
            }

            List<string> arguments = BuildPlayerArguments(cookResult, platformDisplayName, adapterAssemblyPath, adapterTypeName, runtimeModulesRootPath, targetFrameRate, inputDevices, waitForDebugger);
            IEditorPlayProcess process = Launcher.Start(new EditorPlayLaunchRequest(PlayerExecutablePath, arguments));
            TimeSpan launchedAt = Clock.Now;
            if (process == null) {
                throw new InvalidOperationException("The Play process launcher returned no process.");
            }

            EditorPlayStartupTiming startupTiming = new EditorPlayStartupTiming(
                playStartedAt,
                savedAt - playStartedAt,
                cookedAt - savedAt,
                scriptsBuiltAt - cookedAt,
                scriptCompileSkipped,
                launchedAt - scriptsBuiltAt);
            EditorPlayProcessBinding binding = new EditorPlayProcessBinding(this, process, startupTiming);
            CurrentBinding = binding;
            binding.Attach();
            if (CurrentBinding == binding) {
                SetState(EditorPlaySessionState.Running);
            }

            if (process.HasExited) {
                HandlePlayerExited(binding);
            }

            return EditorBuildExecutionResult.Success($"Player started (pid {process.ProcessId}).");
        }

        /// <summary>
        /// Builds the ordered player command line from the cook outcome, the platform adapter and the script modules.
        /// </summary>
        /// <param name="cookResult">Successful cook outcome naming the content root and startup scene.</param>
        /// <param name="platformDisplayName">Display name of the platform, shown in the player window title.</param>
        /// <param name="adapterAssemblyPath">Path of the assembly holding the platform's player adapter.</param>
        /// <param name="adapterTypeName">Type name of the platform's player adapter.</param>
        /// <param name="runtimeModulesRootPath">Directory holding the built runtime script modules.</param>
        /// <param name="targetFrameRate">Frames per second the player paces its frame loop to.</param>
        /// <param name="inputDevices">Input devices the player forwards to the game.</param>
        /// <param name="waitForDebugger">Whether to append the debugger wait flag.</param>
        /// <returns>Player arguments, one entry per argument, in order.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the script builder reported no module root.</exception>
        static List<string> BuildPlayerArguments(
            EditorPlayCookResult cookResult,
            string platformDisplayName,
            string adapterAssemblyPath,
            string adapterTypeName,
            string runtimeModulesRootPath,
            double targetFrameRate,
            PlayerInputDevices inputDevices,
            bool waitForDebugger) {
            if (string.IsNullOrWhiteSpace(runtimeModulesRootPath)) {
                throw new InvalidOperationException("The Play script build succeeded without reporting a runtime module root.");
            }

            List<string> arguments = new List<string> {
                ContentRootOption, cookResult.ContentRootPath,
                SceneIdOption, cookResult.StartupSceneId,
                PlatformNameOption, platformDisplayName,
                ScenePathOption, cookResult.StartupSceneCookedRelativePath,
                AdapterAssemblyOption, adapterAssemblyPath,
                AdapterTypeOption, adapterTypeName,
                ScriptModulesOption, runtimeModulesRootPath,
                TargetFpsOption, targetFrameRate.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                InputDevicesOption, PlayerInputDevicesText.Format(inputDevices)
            };
            if (waitForDebugger) {
                arguments.Add(WaitForDebuggerOption);
            }

            return arguments;
        }

        /// <summary>
        /// Detaches from the current player and terminates it when it is still running, so its late events can no
        /// longer change this session. A player stopped before its first frame logs the startup summary ending in
        /// <c>no frame (stopped)</c>, or its exit code when it had already exited.
        /// </summary>
        void KillCurrentProcess() {
            if (CurrentBinding == null) {
                return;
            }

            EditorPlayProcessBinding binding = CurrentBinding;
            DetachCurrentBinding();
            bool alreadyExited = binding.Process.HasExited;
            if (!alreadyExited) {
                binding.Process.Kill();
            }

            if (!binding.StartupSummaryLogged) {
                binding.StartupSummaryLogged = true;
                Logger.WriteLine(alreadyExited
                    ? binding.StartupTiming.FormatNoFrameSummary(binding.Process.ExitCode)
                    : binding.StartupTiming.FormatStoppedSummary());
            }
        }

        /// <summary>
        /// Unsubscribes from the current player's events and forgets it, so any event it still delivers is stale.
        /// </summary>
        void DetachCurrentBinding() {
            EditorPlayProcessBinding binding = CurrentBinding;
            CurrentBinding = null;
            binding.Detach();
        }

        /// <summary>
        /// Forwards one player standard-output line to the editor log when it comes from the current player. The
        /// first-frame marker is not echoed; it finishes the startup timing and logs the startup summary instead.
        /// </summary>
        /// <param name="binding">Binding of the process that wrote the line.</param>
        /// <param name="line">Line the player wrote.</param>
        internal void HandlePlayerOutputLine(EditorPlayProcessBinding binding, string line) {
            if (binding != CurrentBinding) {
                return;
            }

            if (EditorPlayFirstFrameMarker.IsMarker(line)) {
                LogFirstFrameSummary(binding, line);
            } else {
                Logger.WriteLine(PlayerLogPrefix + line);
            }
        }

        /// <summary>
        /// Logs the startup summary for a player that reported its first frame: the measured editor stages, the time the
        /// player reported from its start to the first frame, and the total since Play started. The first well-formed
        /// marker is authoritative; a malformed marker, or one arriving after the summary was logged, is reported as an
        /// editor error and otherwise ignored, because the line comes from another process and must never throw on the
        /// editor thread.
        /// </summary>
        /// <param name="binding">Binding of the process that wrote the marker.</param>
        /// <param name="markerLine">First-frame marker line the player wrote.</param>
        void LogFirstFrameSummary(EditorPlayProcessBinding binding, string markerLine) {
            if (binding.StartupSummaryLogged || !EditorPlayFirstFrameMarker.TryParseMilliseconds(markerLine, out long firstFrameMilliseconds)) {
                Logger.WriteError(UnexpectedFirstFrameMarkerPrefix + markerLine);
                return;
            }

            TimeSpan receivedAt = Clock.Now;
            EditorPlayStartupTiming timing = binding.StartupTiming;
            binding.StartupSummaryLogged = true;
            Logger.WriteLine(timing.FormatFirstFrameSummary(
                TimeSpan.FromMilliseconds(firstFrameMilliseconds),
                receivedAt - timing.PlayStartedAt));
        }

        /// <summary>
        /// Forwards one player standard-error line to the editor error log when it comes from the current player.
        /// </summary>
        /// <param name="binding">Binding of the process that wrote the line.</param>
        /// <param name="line">Line the player wrote.</param>
        internal void HandlePlayerErrorLine(EditorPlayProcessBinding binding, string line) {
            if (binding != CurrentBinding) {
                return;
            }

            Logger.WriteError(PlayerLogPrefix + line);
        }

        /// <summary>
        /// Handles the current player exiting on its own: logs the startup summary with the exit code when the player
        /// never presented a frame, logs a non-zero exit code read from that player and returns the session to stopped.
        /// Exits from replaced or already-handled players are ignored.
        /// </summary>
        /// <param name="binding">Binding of the process that exited.</param>
        internal void HandlePlayerExited(EditorPlayProcessBinding binding) {
            if (binding != CurrentBinding) {
                return;
            }

            int exitCode = binding.Process.ExitCode;
            DetachCurrentBinding();
            if (!binding.StartupSummaryLogged) {
                binding.StartupSummaryLogged = true;
                Logger.WriteLine(binding.StartupTiming.FormatNoFrameSummary(exitCode));
            }
            if (exitCode != 0) {
                Logger.WriteError($"{PlayerLogPrefix}exited with code {exitCode}");
            }

            SetState(EditorPlaySessionState.Stopped);
        }

        /// <summary>
        /// Updates <see cref="State"/> and raises <see cref="StateChanged"/> when the state actually changes.
        /// </summary>
        /// <param name="state">New session state.</param>
        void SetState(EditorPlaySessionState state) {
            if (State == state) {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
