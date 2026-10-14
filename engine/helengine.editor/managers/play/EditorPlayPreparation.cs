using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// One in-flight editor Play preparation: the request captured on the editor frame thread when Play was pressed,
    /// the cancellation flag Stop raises, and the outcome the background preparation records for the frame thread.
    /// <para>
    /// Threading contract: the request members are immutable. The outcome members are written only by the preparation
    /// thread, before it queues its completion on the editor's main-thread queue; the frame thread reads them only while
    /// running that completion, so the queue's lock orders every write before every read. Cancellation is a polled
    /// flag, never an exception, so a stopped preparation raises no first-chance exception.
    /// </para>
    /// </summary>
    public sealed class EditorPlayPreparation {
        /// <summary>
        /// Set once Stop (or closing the editor) cancels this preparation; read by both threads.
        /// </summary>
        volatile bool CancellationRequested;

        /// <summary>
        /// Signaled by the preparation thread once it finished its work and queued its completion.
        /// </summary>
        readonly ManualResetEventSlim WorkerFinishedSignal = new ManualResetEventSlim(false);

        /// <summary>
        /// Initializes the preparation from the values captured when Play was pressed.
        /// </summary>
        /// <param name="platformId">Identifier of the platform whose content and scripts are prepared.</param>
        /// <param name="platformDisplayName">Display name of the platform, shown in the player window title.</param>
        /// <param name="adapterAssemblyPath">Path of the assembly holding the platform's player adapter.</param>
        /// <param name="adapterTypeName">Type name of the platform's player adapter.</param>
        /// <param name="targetFrameRate">Frames per second the player paces its frame loop to.</param>
        /// <param name="inputDevices">Input devices the player forwards to the game.</param>
        /// <param name="runtimePlatformName">Platform name the player reports to the core, matching the platform's native runtime.</param>
        /// <param name="runtimePlatformVersion">Platform version the player reports to the core, matching the platform's native runtime.</param>
        /// <param name="waitForDebugger">Whether the player should wait for a debugger.</param>
        /// <param name="completed">Receives the launch outcome on the editor frame thread, unless the preparation is cancelled.</param>
        /// <param name="playStartedAt">Clock reading taken when Play was pressed.</param>
        /// <param name="savedAt">Clock reading taken once the scene save finished.</param>
        /// <exception cref="ArgumentNullException">Thrown when the completion callback is null.</exception>
        public EditorPlayPreparation(
            string platformId,
            string platformDisplayName,
            string adapterAssemblyPath,
            string adapterTypeName,
            double targetFrameRate,
            PlayerInputDevices inputDevices,
            string runtimePlatformName,
            string runtimePlatformVersion,
            bool waitForDebugger,
            Action<EditorBuildExecutionResult> completed,
            TimeSpan playStartedAt,
            TimeSpan savedAt) {
            PlatformId = platformId;
            PlatformDisplayName = platformDisplayName;
            AdapterAssemblyPath = adapterAssemblyPath;
            AdapterTypeName = adapterTypeName;
            TargetFrameRate = targetFrameRate;
            InputDevices = inputDevices;
            RuntimePlatformName = runtimePlatformName;
            RuntimePlatformVersion = runtimePlatformVersion;
            WaitForDebugger = waitForDebugger;
            Completed = completed ?? throw new ArgumentNullException(nameof(completed));
            PlayStartedAt = playStartedAt;
            SavedAt = savedAt;
        }

        /// <summary>
        /// Gets the identifier of the platform whose content and scripts are prepared.
        /// </summary>
        public string PlatformId { get; }

        /// <summary>
        /// Gets the display name of the platform, shown in the player window title.
        /// </summary>
        public string PlatformDisplayName { get; }

        /// <summary>
        /// Gets the path of the assembly holding the platform's player adapter.
        /// </summary>
        public string AdapterAssemblyPath { get; }

        /// <summary>
        /// Gets the type name of the platform's player adapter.
        /// </summary>
        public string AdapterTypeName { get; }

        /// <summary>
        /// Gets the frames per second the player paces its frame loop to.
        /// </summary>
        public double TargetFrameRate { get; }

        /// <summary>
        /// Gets the input devices the player forwards to the game.
        /// </summary>
        public PlayerInputDevices InputDevices { get; }

        /// <summary>
        /// Gets the platform name the player reports to the core, matching the name the platform's native runtime reports.
        /// </summary>
        public string RuntimePlatformName { get; }

        /// <summary>
        /// Gets the platform version the player reports to the core, matching the version the platform's native runtime reports.
        /// </summary>
        public string RuntimePlatformVersion { get; }

        /// <summary>
        /// Gets whether the player should wait for a debugger before starting the scene.
        /// </summary>
        public bool WaitForDebugger { get; }

        /// <summary>
        /// Gets the callback that receives the launch outcome on the editor frame thread.
        /// </summary>
        public Action<EditorBuildExecutionResult> Completed { get; }

        /// <summary>
        /// Gets the clock reading taken when Play was pressed.
        /// </summary>
        public TimeSpan PlayStartedAt { get; }

        /// <summary>
        /// Gets the clock reading taken once the scene save finished.
        /// </summary>
        public TimeSpan SavedAt { get; }

        /// <summary>
        /// Gets whether Stop, or closing the editor, cancelled this preparation. A cancelled preparation never launches.
        /// </summary>
        public bool IsCancellationRequested => CancellationRequested;

        /// <summary>
        /// Gets the cancellation of the child processes the preparation starts, such as its <c>dotnet build</c>.
        /// Cancelling the preparation kills them, with their whole process trees.
        /// </summary>
        public EditorProcessCancellation BuildCancellation { get; } = new EditorProcessCancellation();

        /// <summary>
        /// Gets or sets the cook outcome, or null when the cook did not run.
        /// </summary>
        public EditorPlayCookResult CookResult { get; set; }

        /// <summary>
        /// Gets or sets the clock reading taken once the cook finished.
        /// </summary>
        public TimeSpan CookedAt { get; set; }

        /// <summary>
        /// Gets or sets the script build outcome, or null when the script build did not run.
        /// </summary>
        public EditorBuildExecutionResult ScriptBuildResult { get; set; }

        /// <summary>
        /// Gets or sets the runtime module root the script build reported.
        /// </summary>
        public string RuntimeModulesRootPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the script build skipped its compile because the modules were up to date.
        /// </summary>
        public bool ScriptCompileSkipped { get; set; }

        /// <summary>
        /// Gets or sets the clock reading taken once the script build finished.
        /// </summary>
        public TimeSpan ScriptsBuiltAt { get; set; }

        /// <summary>
        /// Gets or sets the failure that stopped the preparation, or null when it did not fail. It is reported on the
        /// frame thread exactly as an exception thrown by a synchronous Play was.
        /// </summary>
        public Exception Failure { get; set; }

        /// <summary>
        /// Cancels the preparation. A running child process tree (the script build) is killed, the preparation thread
        /// stops at its next stage boundary, and the frame thread discards whatever it produced; the player is never
        /// launched.
        /// </summary>
        public void RequestCancellation() {
            CancellationRequested = true;
            BuildCancellation.Cancel();
        }

        /// <summary>
        /// Called by the preparation thread as its very last step, after it queued its completion.
        /// </summary>
        public void MarkWorkerFinished() {
            WorkerFinishedSignal.Set();
        }

        /// <summary>
        /// Waits, without raising any exception, until the preparation thread finished or the wait times out.
        /// </summary>
        /// <param name="maximumWait">Longest time to wait.</param>
        /// <returns>True when the preparation thread finished within the wait.</returns>
        public bool WaitForWorker(TimeSpan maximumWait) {
            return WorkerFinishedSignal.Wait(maximumWait);
        }
    }
}
