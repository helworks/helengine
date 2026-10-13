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
        /// Player flag asking the player to wait for a debugger before starting the scene.
        /// </summary>
        public const string WaitForDebuggerOption = "--wait-for-debugger";

        /// <summary>
        /// Prefix applied to every editor log line that originates from the player.
        /// </summary>
        const string PlayerLogPrefix = "[player] ";

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
        /// Player process started by the latest successful Play, or null when none is attached.
        /// </summary>
        IEditorPlayProcess CurrentProcess;

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
            string playerExecutablePath) {
            if (string.IsNullOrWhiteSpace(playerExecutablePath)) {
                throw new ArgumentException("The Play session requires the player executable path.", nameof(playerExecutablePath));
            }

            CookRunner = cookRunner ?? throw new ArgumentNullException(nameof(cookRunner));
            ScriptBuilder = scriptBuilder ?? throw new ArgumentNullException(nameof(scriptBuilder));
            Launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
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
        /// Every failure returns the session to <see cref="EditorPlaySessionState.Stopped"/>.
        /// </summary>
        /// <param name="platformId">Identifier of the platform whose content and scripts are prepared.</param>
        /// <param name="currentScenePath">Saved path of the scene open in the editor; blank when it was never saved.</param>
        /// <param name="saveScene">Saves the open scene and returns whether the save succeeded.</param>
        /// <param name="adapterAssemblyPath">Path of the assembly holding the platform's player adapter.</param>
        /// <param name="adapterTypeName">Type name of the platform's player adapter.</param>
        /// <param name="waitForDebugger">Whether the player should wait for a debugger before starting the scene.</param>
        /// <returns>Success naming the started player's process id, or the failure that stopped Play.</returns>
        /// <exception cref="ArgumentException">Thrown when the platform, adapter assembly or adapter type is blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the save callback is null.</exception>
        public EditorBuildExecutionResult Play(
            string platformId,
            string currentScenePath,
            Func<bool> saveScene,
            string adapterAssemblyPath,
            string adapterTypeName,
            bool waitForDebugger) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Play requires a platform identifier.", nameof(platformId));
            } else if (saveScene == null) {
                throw new ArgumentNullException(nameof(saveScene));
            } else if (string.IsNullOrWhiteSpace(adapterAssemblyPath)) {
                throw new ArgumentException("Play requires the player adapter assembly path.", nameof(adapterAssemblyPath));
            } else if (string.IsNullOrWhiteSpace(adapterTypeName)) {
                throw new ArgumentException("Play requires the player adapter type name.", nameof(adapterTypeName));
            }

            if (string.IsNullOrWhiteSpace(currentScenePath)) {
                return EditorBuildExecutionResult.Failure("Save the scene before pressing Play.");
            }

            KillCurrentProcess();
            SetState(EditorPlaySessionState.Preparing);
            try {
                return PrepareAndLaunch(platformId, saveScene, adapterAssemblyPath, adapterTypeName, waitForDebugger);
            } catch {
                SetState(EditorPlaySessionState.Stopped);
                throw;
            }
        }

        /// <summary>
        /// Terminates the running player, if any, and returns the session to <see cref="EditorPlaySessionState.Stopped"/>.
        /// </summary>
        public void Stop() {
            KillCurrentProcess();
            SetState(EditorPlaySessionState.Stopped);
        }

        /// <summary>
        /// Performs the save, cook, script build and launch steps of Play once the session is preparing.
        /// </summary>
        /// <param name="platformId">Identifier of the platform whose content and scripts are prepared.</param>
        /// <param name="saveScene">Saves the open scene and returns whether the save succeeded.</param>
        /// <param name="adapterAssemblyPath">Path of the assembly holding the platform's player adapter.</param>
        /// <param name="adapterTypeName">Type name of the platform's player adapter.</param>
        /// <param name="waitForDebugger">Whether the player should wait for a debugger.</param>
        /// <returns>Success naming the started player's process id, or the failure that stopped Play.</returns>
        EditorBuildExecutionResult PrepareAndLaunch(
            string platformId,
            Func<bool> saveScene,
            string adapterAssemblyPath,
            string adapterTypeName,
            bool waitForDebugger) {
            if (!saveScene()) {
                SetState(EditorPlaySessionState.Stopped);
                return EditorBuildExecutionResult.Failure("Scene save failed; Play aborted.");
            }

            EditorPlayCookResult cookResult = CookRunner.Cook(platformId);
            if (cookResult == null) {
                throw new InvalidOperationException("The Play cook runner returned no result.");
            } else if (!cookResult.Succeeded) {
                SetState(EditorPlaySessionState.Stopped);
                return EditorBuildExecutionResult.Failure(cookResult.Message);
            }

            EditorBuildExecutionResult buildResult = ScriptBuilder.Build(platformId, out string runtimeModulesRootPath);
            if (buildResult == null) {
                throw new InvalidOperationException("The Play script builder returned no result.");
            } else if (!buildResult.Succeeded) {
                SetState(EditorPlaySessionState.Stopped);
                return buildResult;
            }

            List<string> arguments = BuildPlayerArguments(cookResult, adapterAssemblyPath, adapterTypeName, runtimeModulesRootPath, waitForDebugger);
            IEditorPlayProcess process = Launcher.Start(new EditorPlayLaunchRequest(PlayerExecutablePath, arguments));
            if (process == null) {
                throw new InvalidOperationException("The Play process launcher returned no process.");
            }

            CurrentProcess = process;
            SetState(EditorPlaySessionState.Running);
            process.OutputLine += HandlePlayerOutputLine;
            process.ErrorLine += HandlePlayerErrorLine;
            process.Exited += HandlePlayerExited;
            return EditorBuildExecutionResult.Success($"Player started (pid {process.ProcessId}).");
        }

        /// <summary>
        /// Builds the ordered player command line from the cook outcome, the platform adapter and the script modules.
        /// </summary>
        /// <param name="cookResult">Successful cook outcome naming the content root and startup scene.</param>
        /// <param name="adapterAssemblyPath">Path of the assembly holding the platform's player adapter.</param>
        /// <param name="adapterTypeName">Type name of the platform's player adapter.</param>
        /// <param name="runtimeModulesRootPath">Directory holding the built runtime script modules.</param>
        /// <param name="waitForDebugger">Whether to append the debugger wait flag.</param>
        /// <returns>Player arguments, one entry per argument, in order.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the script builder reported no module root.</exception>
        static List<string> BuildPlayerArguments(
            EditorPlayCookResult cookResult,
            string adapterAssemblyPath,
            string adapterTypeName,
            string runtimeModulesRootPath,
            bool waitForDebugger) {
            if (string.IsNullOrWhiteSpace(runtimeModulesRootPath)) {
                throw new InvalidOperationException("The Play script build succeeded without reporting a runtime module root.");
            }

            List<string> arguments = new List<string> {
                ContentRootOption, cookResult.ContentRootPath,
                SceneIdOption, cookResult.StartupSceneId,
                ScenePathOption, cookResult.StartupSceneCookedRelativePath,
                AdapterAssemblyOption, adapterAssemblyPath,
                AdapterTypeOption, adapterTypeName,
                ScriptModulesOption, runtimeModulesRootPath
            };
            if (waitForDebugger) {
                arguments.Add(WaitForDebuggerOption);
            }

            return arguments;
        }

        /// <summary>
        /// Detaches from the current player and terminates it when it is still running, so its late events can no
        /// longer change this session.
        /// </summary>
        void KillCurrentProcess() {
            if (CurrentProcess == null) {
                return;
            }

            IEditorPlayProcess process = CurrentProcess;
            DetachCurrentProcess();
            if (!process.HasExited) {
                process.Kill();
            }
        }

        /// <summary>
        /// Unsubscribes from the current player's events and forgets it.
        /// </summary>
        void DetachCurrentProcess() {
            CurrentProcess.OutputLine -= HandlePlayerOutputLine;
            CurrentProcess.ErrorLine -= HandlePlayerErrorLine;
            CurrentProcess.Exited -= HandlePlayerExited;
            CurrentProcess = null;
        }

        /// <summary>
        /// Forwards one player standard-output line to the editor log.
        /// </summary>
        /// <param name="line">Line the player wrote.</param>
        void HandlePlayerOutputLine(string line) {
            Logger.WriteLine(PlayerLogPrefix + line);
        }

        /// <summary>
        /// Forwards one player standard-error line to the editor error log.
        /// </summary>
        /// <param name="line">Line the player wrote.</param>
        void HandlePlayerErrorLine(string line) {
            Logger.WriteError(PlayerLogPrefix + line);
        }

        /// <summary>
        /// Handles the current player exiting on its own: logs a non-zero exit code and returns the session to stopped.
        /// </summary>
        void HandlePlayerExited() {
            int exitCode = CurrentProcess.ExitCode;
            DetachCurrentProcess();
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
