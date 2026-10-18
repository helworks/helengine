using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;

namespace helengine.editor {
    /// <summary>
    /// Orchestrates editor Play: saves the current scene, cooks content, builds the runtime scripts, launches the
    /// separate desktop player and forwards its console output to the editor log. Stop terminates the player.
    /// The concrete cook, script build and process launch are supplied through small interfaces so the sequence can
    /// be verified without running real builds or processes.
    /// <para>
    /// Threading: <see cref="Play"/>, <see cref="Stop"/> and every player event run on the editor frame thread. Play
    /// saves the scene there, then hands the cook and the script build to a background preparation and returns at once,
    /// so the editor keeps rendering while Play is prepared. The preparation reports back through the main-thread queue,
    /// and the player is launched on the frame thread. Only one preparation exists at a time: a second Play while one is
    /// preparing is ignored, and Stop cancels it so it never launches.
    /// </para>
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
        /// Player flag naming the input devices the platform's hardware offers, taken from the platform's Play capability
        /// and written as the lowercase comma list of <see cref="PlayerInputDevicesText"/> (PS2: <c>gamepad</c>).
        /// </summary>
        public const string InputDevicesOption = "--input-devices";

        /// <summary>
        /// Player flag naming the platform name the player reports to the core, taken from the platform's Play capability
        /// so it matches the name the platform's native runtime reports (PS2: <c>ps2</c>).
        /// </summary>
        public const string RuntimePlatformNameOption = "--runtime-platform-name";

        /// <summary>
        /// Player flag naming the platform version the player reports to the core, taken from the platform's Play
        /// capability so it matches the version the platform's native runtime reports (PS2: <c>1.0.0</c>).
        /// </summary>
        public const string RuntimePlatformVersionOption = "--runtime-platform-version";

        /// <summary>
        /// Player flag naming the resolved Play settings file (window scale and monitor, debugger wait, shadows, frame
        /// pacing and the platform's own Play settings).
        /// </summary>
        public const string PlaySettingsOption = "--play-settings";

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
        /// Result message of a Play pressed while another Play is still preparing; that second Play is ignored.
        /// </summary>
        public const string AlreadyPreparingMessage = "Play is already preparing; the extra Play was ignored.";

        /// <summary>
        /// Result message of a Play pressed after Stop cancelled a preparation whose background cook has not finished yet.
        /// </summary>
        public const string PreviousPreparationStoppingMessage = "The stopped Play is still finishing its cook in the background; press Play again in a moment.";

        /// <summary>
        /// Editor log line written when Stop cancels a preparation.
        /// </summary>
        public const string PreparationStoppedMessage = "[play] preparation stopped; the player will not be launched.";

        /// <summary>
        /// Prefix of the failure reported when the cook, the script build or the launch throws.
        /// </summary>
        const string PlayFailedPrefix = "Play failed: ";

        /// <summary>
        /// Editor warning logged when the editor closes before a stopped preparation's cook finished.
        /// </summary>
        public const string ShutdownAbandonedMessage = "[play] the stopped preparation was still cooking when the editor closed; it was abandoned.";

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
        /// Queue the editor drains on its frame thread; the background preparation reports its outcome through it.
        /// </summary>
        readonly EditorMainThreadActionQueue MainThreadQueue;

        /// <summary>
        /// Starts the cook and the script build away from the editor frame thread.
        /// </summary>
        readonly IEditorPlayPreparationScheduler PreparationScheduler;

        /// <summary>
        /// Writes the resolved Play settings file the player reads.
        /// </summary>
        readonly EditorPlaySettingsFileWriter PlaySettingsFileWriter;

        /// <summary>
        /// Binding to the player process started by the latest successful Play, or null when none is attached.
        /// Events from any other binding are stale and ignored.
        /// </summary>
        EditorPlayProcessBinding CurrentBinding;

        /// <summary>
        /// Preparation whose completion the frame thread has not processed yet, including one Stop already cancelled
        /// whose background work is still finishing; null when no preparation exists. Only the frame thread reads or
        /// writes it.
        /// </summary>
        EditorPlayPreparation CurrentPreparation;

        /// <summary>
        /// Initializes the Play session service.
        /// </summary>
        /// <param name="cookRunner">Runs the assets-only cook.</param>
        /// <param name="scriptBuilder">Builds the runtime script modules.</param>
        /// <param name="launcher">Starts the player process.</param>
        /// <param name="playerExecutablePath">Absolute path of the desktop player executable.</param>
        /// <param name="mainThreadQueue">Queue the editor drains on its frame thread, used to report a finished preparation.</param>
        /// <param name="preparationScheduler">Starts the cook and the script build off the editor frame thread.</param>
        /// <param name="playSettingsFileWriter">Writes the resolved Play settings file the player reads.</param>
        /// <exception cref="ArgumentNullException">Thrown when any collaborator is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the player executable path is blank.</exception>
        public EditorPlaySessionService(
            IEditorPlayCookRunner cookRunner,
            IEditorPlayScriptBuilder scriptBuilder,
            IEditorPlayProcessLauncher launcher,
            string playerExecutablePath,
            EditorMainThreadActionQueue mainThreadQueue,
            IEditorPlayPreparationScheduler preparationScheduler,
            EditorPlaySettingsFileWriter playSettingsFileWriter)
            : this(cookRunner, scriptBuilder, launcher, playerExecutablePath, mainThreadQueue, preparationScheduler, playSettingsFileWriter, new EditorPlayStopwatchClock()) {
        }

        /// <summary>
        /// Initializes the Play session service with an explicit clock, so tests can time the Play stages with fixed
        /// readings.
        /// </summary>
        /// <param name="cookRunner">Runs the assets-only cook.</param>
        /// <param name="scriptBuilder">Builds the runtime script modules.</param>
        /// <param name="launcher">Starts the player process.</param>
        /// <param name="playerExecutablePath">Absolute path of the desktop player executable.</param>
        /// <param name="mainThreadQueue">Queue the editor drains on its frame thread, used to report a finished preparation.</param>
        /// <param name="preparationScheduler">Starts the cook and the script build off the editor frame thread.</param>
        /// <param name="playSettingsFileWriter">Writes the resolved Play settings file the player reads.</param>
        /// <param name="clock">Monotonic clock timing the Play stages for the startup summary; read from both threads.</param>
        /// <exception cref="ArgumentNullException">Thrown when any collaborator or the clock is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the player executable path is blank.</exception>
        internal EditorPlaySessionService(
            IEditorPlayCookRunner cookRunner,
            IEditorPlayScriptBuilder scriptBuilder,
            IEditorPlayProcessLauncher launcher,
            string playerExecutablePath,
            EditorMainThreadActionQueue mainThreadQueue,
            IEditorPlayPreparationScheduler preparationScheduler,
            EditorPlaySettingsFileWriter playSettingsFileWriter,
            IEditorPlayClock clock) {
            if (string.IsNullOrWhiteSpace(playerExecutablePath)) {
                throw new ArgumentException("The Play session requires the player executable path.", nameof(playerExecutablePath));
            }

            CookRunner = cookRunner ?? throw new ArgumentNullException(nameof(cookRunner));
            ScriptBuilder = scriptBuilder ?? throw new ArgumentNullException(nameof(scriptBuilder));
            Launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            MainThreadQueue = mainThreadQueue ?? throw new ArgumentNullException(nameof(mainThreadQueue));
            PreparationScheduler = preparationScheduler ?? throw new ArgumentNullException(nameof(preparationScheduler));
            PlaySettingsFileWriter = playSettingsFileWriter ?? throw new ArgumentNullException(nameof(playSettingsFileWriter));
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
        /// Gets whether a Play preparation still has background work the frame thread has not completed: one that is
        /// preparing, or one Stop cancelled whose cook is still finishing. While it is true the project's Play inputs
        /// may be read by the preparation, so the editor defers authoring operations that would race it.
        /// </summary>
        public bool IsPreparationActive => CurrentPreparation != null;

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
        /// Starts Play: kills any running player and saves the scene on the calling (frame) thread, then starts the cook
        /// and the script build in the background and returns without waiting for them. When the preparation finishes,
        /// the player is launched on the frame thread during the next main-thread queue drain and
        /// <paramref name="completed"/> receives the launch outcome; a preparation cancelled by <see cref="Stop"/> never
        /// launches and never calls <paramref name="completed"/>. A Play pressed while a preparation is still active is
        /// ignored and returns a failure naming why. Every failure, including a failure to terminate the previous player,
        /// returns the session to <see cref="EditorPlaySessionState.Stopped"/>.
        /// </summary>
        /// <param name="platformId">Identifier of the platform whose content and scripts are prepared.</param>
        /// <param name="platformDisplayName">Display name of the platform, shown in the player window title.</param>
        /// <param name="currentScenePath">Saved path of the scene open in the editor; blank when it was never saved.</param>
        /// <param name="saveScene">Saves the open scene and returns whether the save succeeded.</param>
        /// <param name="adapterAssemblyPath">Path of the assembly holding the platform's player adapter.</param>
        /// <param name="adapterTypeName">Type name of the platform's player adapter.</param>
        /// <param name="inputDevices">Input devices the platform's Play capability declares; the player forwards only these to the game.</param>
        /// <param name="runtimePlatformName">Platform name the platform's Play capability declares; the player reports it to the core.</param>
        /// <param name="runtimePlatformVersion">Platform version the platform's Play capability declares; the player reports it to the core.</param>
        /// <param name="playSettings">Play settings captured when Play was pressed; edits made afterwards apply to the next Play.</param>
        /// <param name="completed">Receives the launch outcome on the frame thread: success naming the started player's process id, or the failure that stopped Play.</param>
        /// <returns>Success once the preparation started in the background, or the failure that stopped or ignored Play.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the platform, platform display name, adapter assembly, adapter type, runtime platform name or runtime
        /// platform version is blank, or the Play settings were captured for another platform.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the input device set is empty or names unknown devices.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown when the save callback, the completion callback or the Play settings are null.</exception>
        public EditorBuildExecutionResult Play(
            string platformId,
            string platformDisplayName,
            string currentScenePath,
            Func<bool> saveScene,
            string adapterAssemblyPath,
            string adapterTypeName,
            PlayerInputDevices inputDevices,
            string runtimePlatformName,
            string runtimePlatformVersion,
            EditorPlaySettingsSnapshot playSettings,
            Action<EditorBuildExecutionResult> completed) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Play requires a platform identifier.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(platformDisplayName)) {
                throw new ArgumentException("Play requires the platform display name.", nameof(platformDisplayName));
            } else if (saveScene == null) {
                throw new ArgumentNullException(nameof(saveScene));
            } else if (completed == null) {
                throw new ArgumentNullException(nameof(completed));
            } else if (string.IsNullOrWhiteSpace(adapterAssemblyPath)) {
                throw new ArgumentException("Play requires the player adapter assembly path.", nameof(adapterAssemblyPath));
            } else if (string.IsNullOrWhiteSpace(adapterTypeName)) {
                throw new ArgumentException("Play requires the player adapter type name.", nameof(adapterTypeName));
            } else if (inputDevices == PlayerInputDevices.None || (inputDevices & ~PlayerInputDevicesText.AllDevices) != PlayerInputDevices.None) {
                throw new ArgumentOutOfRangeException(nameof(inputDevices), inputDevices, "Play requires at least one known player input device.");
            } else if (string.IsNullOrWhiteSpace(runtimePlatformName)) {
                throw new ArgumentException("Play requires the platform name the native runtime reports.", nameof(runtimePlatformName));
            } else if (string.IsNullOrWhiteSpace(runtimePlatformVersion)) {
                throw new ArgumentException("Play requires the platform version the native runtime reports.", nameof(runtimePlatformVersion));
            } else if (playSettings == null) {
                throw new ArgumentNullException(nameof(playSettings));
            } else if (!string.Equals(playSettings.PlatformId, platformId, StringComparison.Ordinal)) {
                throw new ArgumentException($"The Play settings were captured for platform '{playSettings.PlatformId}', not '{platformId}'.", nameof(playSettings));
            }

            if (string.IsNullOrWhiteSpace(currentScenePath)) {
                return EditorBuildExecutionResult.Failure("Save the scene before pressing Play.");
            }

            if (CurrentPreparation != null) {
                return EditorBuildExecutionResult.Failure(CurrentPreparation.IsCancellationRequested
                    ? PreviousPreparationStoppingMessage
                    : AlreadyPreparingMessage);
            }

            SetState(EditorPlaySessionState.Preparing);
            EditorPlayPreparation preparation;
            try {
                KillCurrentProcess();
                TimeSpan playStartedAt = Clock.Now;
                if (!saveScene()) {
                    SetState(EditorPlaySessionState.Stopped);
                    return EditorBuildExecutionResult.Failure("Scene save failed; Play aborted.");
                }

                preparation = new EditorPlayPreparation(
                    platformId,
                    platformDisplayName,
                    adapterAssemblyPath,
                    adapterTypeName,
                    inputDevices,
                    runtimePlatformName,
                    runtimePlatformVersion,
                    playSettings,
                    completed,
                    playStartedAt,
                    Clock.Now);
                CurrentPreparation = preparation;
                PreparationScheduler.Start(() => RunPreparation(preparation));
            } catch {
                CurrentPreparation = null;
                SetState(EditorPlaySessionState.Stopped);
                throw;
            }

            return EditorBuildExecutionResult.Success($"Preparing Play for {platformDisplayName}.");
        }

        /// <summary>
        /// Stops Play. A preparation that is still running is cancelled: its <c>dotnet build</c> process tree, when one
        /// runs, is killed, the preparation stops at its next stage boundary, and the player is never launched. A running player is terminated. The session returns to
        /// <see cref="EditorPlaySessionState.Stopped"/> at once, even when terminating the player fails; that failure is
        /// still rethrown.
        /// </summary>
        public void Stop() {
            EditorPlayPreparation preparation = CurrentPreparation;
            try {
                if (preparation != null && !preparation.IsCancellationRequested) {
                    Logger.WriteLine(PreparationStoppedMessage);
                    preparation.RequestCancellation();
                }

                KillCurrentProcess();
            } finally {
                SetState(EditorPlaySessionState.Stopped);
            }
        }

        /// <summary>
        /// Shuts Play down when the editor closes: stops it exactly like <see cref="Stop"/> (cancelling a preparation and
        /// killing its <c>dotnet build</c> process tree), then waits a bounded time for the preparation thread to finish,
        /// so the services it is still using are not disposed under it and no child process outlives the editor. The
        /// cook itself cannot be interrupted; when it does not finish within the wait the thread is abandoned (it is a
        /// background thread and dies with the process) and a warning is logged. Waiting raises no exception.
        /// </summary>
        /// <param name="maximumWait">Longest time to wait for a running preparation thread.</param>
        /// <returns>True when no preparation thread is left running.</returns>
        public bool Shutdown(TimeSpan maximumWait) {
            EditorPlayPreparation preparation = CurrentPreparation;
            Stop();
            if (preparation == null || preparation.WaitForWorker(maximumWait)) {
                return true;
            }

            Logger.WriteWarning(ShutdownAbandonedMessage);
            return false;
        }

        /// <summary>
        /// Runs the cook and then the script build for one preparation on the preparation thread, records their
        /// outcomes on the preparation and queues its completion for the frame thread. The cancellation flag is polled
        /// before each stage, so a stopped preparation skips the stages it has not started; the cook itself is not
        /// interruptible. Any exception is recorded and reported by the completion instead of escaping the thread.
        /// This method touches no editor UI or scene state: it reads only the preparation's captured request and the
        /// cook runner and script builder, which work from files and from inputs captured when Play was pressed.
        /// </summary>
        /// <param name="preparation">Preparation to run.</param>
        void RunPreparation(EditorPlayPreparation preparation) {
            try {
                if (preparation.IsCancellationRequested) {
                    return;
                }

                EditorPlayCookResult cookResult = CookRunner.Cook(preparation.PlatformId, preparation.PlaySettings);
                preparation.CookedAt = Clock.Now;
                preparation.CookResult = cookResult;
                if (cookResult == null || !cookResult.Succeeded || preparation.IsCancellationRequested) {
                    return;
                }

                EditorBuildExecutionResult buildResult = ScriptBuilder.Build(preparation.PlatformId, preparation.BuildCancellation, out string runtimeModulesRootPath, out bool scriptCompileSkipped);
                preparation.ScriptsBuiltAt = Clock.Now;
                preparation.ScriptBuildResult = buildResult;
                preparation.RuntimeModulesRootPath = runtimeModulesRootPath ?? string.Empty;
                preparation.ScriptCompileSkipped = scriptCompileSkipped;
            } catch (Exception exception) {
                preparation.Failure = exception;
            } finally {
                MainThreadQueue.Enqueue(() => CompletePreparation(preparation));
                preparation.MarkWorkerFinished();
            }
        }

        /// <summary>
        /// Completes one preparation on the frame thread. A cancelled preparation is discarded without launching or
        /// reporting anything, because Stop already returned the session to stopped. Otherwise the player is launched
        /// when the cook and the script build succeeded, the session ends running or stopped, and the preparation's
        /// completion callback receives the outcome. A launch that throws is reported as a failure, never propagated
        /// into the frame's queue drain.
        /// </summary>
        /// <param name="preparation">Preparation whose background work finished.</param>
        void CompletePreparation(EditorPlayPreparation preparation) {
            if (preparation != CurrentPreparation) {
                return;
            }

            CurrentPreparation = null;
            if (preparation.IsCancellationRequested) {
                return;
            }

            EditorBuildExecutionResult result;
            try {
                result = LaunchPreparedPlayer(preparation);
            } catch (Exception exception) {
                result = EditorBuildExecutionResult.Failure(PlayFailedPrefix + exception);
            }

            if (!result.Succeeded) {
                SetState(EditorPlaySessionState.Stopped);
            }

            preparation.Completed(result);
        }

        /// <summary>
        /// Turns a finished preparation into a launched player: reports the first failure the preparation recorded, or
        /// resolves the Play settings captured when Play was pressed against the cook result, writes them to the Play
        /// settings file, builds the player command line, starts the player, binds its events and enters
        /// <see cref="EditorPlaySessionState.Running"/>.
        /// </summary>
        /// <param name="preparation">Finished, not cancelled preparation.</param>
        /// <returns>Success naming the started player's process id, or the failure that stopped Play.</returns>
        EditorBuildExecutionResult LaunchPreparedPlayer(EditorPlayPreparation preparation) {
            EditorPlayCookResult cookResult = preparation.CookResult;
            EditorBuildExecutionResult buildResult = preparation.ScriptBuildResult;
            if (preparation.Failure != null) {
                return EditorBuildExecutionResult.Failure(PlayFailedPrefix + preparation.Failure);
            } else if (cookResult == null) {
                return EditorBuildExecutionResult.Failure(PlayFailedPrefix + "The Play cook runner returned no result.");
            } else if (!cookResult.Succeeded) {
                return EditorBuildExecutionResult.Failure(cookResult.Message);
            } else if (buildResult == null) {
                return EditorBuildExecutionResult.Failure(PlayFailedPrefix + "The Play script builder returned no result.");
            } else if (!buildResult.Succeeded) {
                return buildResult;
            }

            PlayerPlaySettings playerSettings = EditorPlayLaunchSettingsResolver.Resolve(preparation.PlaySettings, cookResult);
            string playSettingsPath = PlaySettingsFileWriter.Write(preparation.PlatformId, playerSettings);
            List<string> arguments = BuildPlayerArguments(
                cookResult,
                preparation.PlatformDisplayName,
                preparation.AdapterAssemblyPath,
                preparation.AdapterTypeName,
                preparation.RuntimeModulesRootPath,
                preparation.InputDevices,
                preparation.RuntimePlatformName,
                preparation.RuntimePlatformVersion,
                playSettingsPath);
            IEditorPlayProcess process = Launcher.Start(new EditorPlayLaunchRequest(PlayerExecutablePath, arguments));
            TimeSpan launchedAt = Clock.Now;
            if (process == null) {
                return EditorBuildExecutionResult.Failure(PlayFailedPrefix + "The Play process launcher returned no process.");
            }

            EditorPlayStartupTiming startupTiming = new EditorPlayStartupTiming(
                preparation.PlayStartedAt,
                preparation.SavedAt - preparation.PlayStartedAt,
                preparation.CookedAt - preparation.SavedAt,
                preparation.ScriptsBuiltAt - preparation.CookedAt,
                preparation.ScriptCompileSkipped,
                launchedAt - preparation.ScriptsBuiltAt);
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
        /// Builds the ordered player command line from the cook outcome, the platform adapter, the script modules and the
        /// resolved Play settings file.
        /// </summary>
        /// <param name="cookResult">Successful cook outcome naming the content root and startup scene.</param>
        /// <param name="platformDisplayName">Display name of the platform, shown in the player window title.</param>
        /// <param name="adapterAssemblyPath">Path of the assembly holding the platform's player adapter.</param>
        /// <param name="adapterTypeName">Type name of the platform's player adapter.</param>
        /// <param name="runtimeModulesRootPath">Directory holding the built runtime script modules.</param>
        /// <param name="inputDevices">Input devices the player forwards to the game.</param>
        /// <param name="runtimePlatformName">Platform name the player reports to the core.</param>
        /// <param name="runtimePlatformVersion">Platform version the player reports to the core.</param>
        /// <param name="playSettingsPath">Resolved Play settings file.</param>
        /// <returns>Player arguments, one entry per argument, in order.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the script builder reported no module root.</exception>
        static List<string> BuildPlayerArguments(
            EditorPlayCookResult cookResult,
            string platformDisplayName,
            string adapterAssemblyPath,
            string adapterTypeName,
            string runtimeModulesRootPath,
            PlayerInputDevices inputDevices,
            string runtimePlatformName,
            string runtimePlatformVersion,
            string playSettingsPath) {
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
                InputDevicesOption, PlayerInputDevicesText.Format(inputDevices),
                RuntimePlatformNameOption, runtimePlatformName,
                RuntimePlatformVersionOption, runtimePlatformVersion,
                PlaySettingsOption, playSettingsPath
            };
            if (cookResult.RuntimeSettingsPath.Length > 0) {
                arguments.Add("--runtime-settings");
                arguments.Add(cookResult.RuntimeSettingsPath);
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
