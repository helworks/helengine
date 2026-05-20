using System.Text;

namespace helengine {
    /// <summary>
    /// Executes one authored fixed-order scene-memory probe and emits compact runtime checkpoints after each completed step.
    /// </summary>
    public sealed class SceneMemoryProbeComponent : UpdateComponent {
        /// <summary>
        /// Reusable scalar memory counter container used for allocation-light checkpoint capture.
        /// </summary>
        readonly RuntimeMemoryCounters MemoryCountersValue;

        /// <summary>
        /// Accumulated elapsed time for the current step.
        /// </summary>
        double CurrentStepElapsedSecondsValue;

        /// <summary>
        /// Accumulated elapsed time spent waiting for the initial probe delay.
        /// </summary>
        double InitialDelayElapsedSecondsValue;

        /// <summary>
        /// Zero-based index of the currently active step.
        /// </summary>
        int CurrentStepIndexValue;

        /// <summary>
        /// Zero-based index of the currently active loop cycle.
        /// </summary>
        int CurrentCycleIndexValue;

        /// <summary>
        /// Tracks whether the probe has entered active execution.
        /// </summary>
        bool StartedValue;

        /// <summary>
        /// Tracks whether the probe exhausted its authored step list and stopped.
        /// </summary>
        bool CompletedValue;

        /// <summary>
        /// Tracks whether the current scene action step has already issued its scene-manager request.
        /// </summary>
        bool CurrentStepActionIssuedValue;

        /// <summary>
        /// Initializes one scene-memory probe component with an empty authored step list.
        /// </summary>
        public SceneMemoryProbeComponent() {
            MemoryCountersValue = new RuntimeMemoryCounters();
            Steps = Array.Empty<SceneMemoryProbeStep>();
            ProbeName = string.Empty;
            StartAutomatically = true;
        }

        /// <summary>
        /// Gets or sets the authored probe name written to emitted checkpoints.
        /// </summary>
        public string ProbeName { get; set; }

        /// <summary>
        /// Gets or sets the authored fixed-order step list executed by this probe.
        /// </summary>
        public SceneMemoryProbeStep[] Steps { get; set; }

        /// <summary>
        /// Gets or sets whether the probe should restart from the first step after completing the final step.
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// Gets or sets whether the probe should begin automatically once the initial delay has elapsed.
        /// </summary>
        public bool StartAutomatically { get; set; }

        /// <summary>
        /// Gets or sets the initial delay in seconds applied before the first step begins.
        /// </summary>
        public double InitialDelaySeconds { get; set; }

        /// <summary>
        /// Gets the zero-based index of the currently active step.
        /// </summary>
        public int CurrentStepIndex {
            get { return CurrentStepIndexValue; }
        }

        /// <summary>
        /// Gets the zero-based index of the current loop cycle.
        /// </summary>
        public int CurrentCycleIndex {
            get { return CurrentCycleIndexValue; }
        }

        /// <summary>
        /// Gets whether the probe has started active execution.
        /// </summary>
        public bool HasStarted {
            get { return StartedValue; }
        }

        /// <summary>
        /// Gets whether the probe finished and stopped because looping is disabled.
        /// </summary>
        public bool IsCompleted {
            get { return CompletedValue; }
        }

        /// <summary>
        /// Starts the probe immediately from the first authored step and clears any previously accumulated runtime state.
        /// </summary>
        public void StartProbe() {
            ValidateConfiguration();
            ResetRuntimeState();
            StartedValue = true;
        }

        /// <summary>
        /// Stops the probe and clears any active step execution state without mutating the authored configuration.
        /// </summary>
        public void StopProbe() {
            StartedValue = false;
            CompletedValue = true;
            CurrentStepActionIssuedValue = false;
            CurrentStepElapsedSecondsValue = 0d;
            InitialDelayElapsedSecondsValue = 0d;
        }

        /// <summary>
        /// Advances the currently active authored step and emits a checkpoint when the step completes.
        /// </summary>
        public override void Update() {
            base.Update();

            Core core = Core.Instance;
            if (core == null) {
                return;
            }
            if (!StartedValue) {
                StartProbeIfNeeded(core);
                return;
            }
            if (CompletedValue || ResolveStepCount() == 0) {
                return;
            }

            SceneMemoryProbeStep currentStep = ResolveCurrentStep();
            if (currentStep.ActionKind == SceneMemoryProbeActionKind.Wait) {
                AdvanceWaitStep(core, currentStep);
                return;
            }

            AdvanceSceneActionStep(core, currentStep);
        }

        /// <summary>
        /// Starts the probe automatically when enabled and the authored initial delay has elapsed.
        /// </summary>
        /// <param name="core">Active runtime core driving the current frame.</param>
        void StartProbeIfNeeded(Core core) {
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }
            if (!StartAutomatically) {
                return;
            }

            ValidateConfiguration();
            if (ResolveStepCount() == 0) {
                StartedValue = true;
                CompletedValue = true;
                return;
            }

            if (InitialDelaySeconds <= 0d) {
                StartedValue = true;
                return;
            }

            InitialDelayElapsedSecondsValue += core.DeltaTime;
            if (InitialDelayElapsedSecondsValue >= InitialDelaySeconds) {
                StartedValue = true;
            }
        }

        /// <summary>
        /// Advances one wait step until the authored duration elapses and then emits one checkpoint.
        /// </summary>
        /// <param name="core">Active runtime core driving the current frame.</param>
        /// <param name="step">Current authored step.</param>
        void AdvanceWaitStep(Core core, SceneMemoryProbeStep step) {
            CurrentStepElapsedSecondsValue += core.DeltaTime;
            if (CurrentStepElapsedSecondsValue < step.DurationSeconds) {
                return;
            }

            EmitMeasurement(core, step);
            AdvanceToNextStep();
        }

        /// <summary>
        /// Issues one scene-manager request for the current action step and emits one checkpoint on the following update after the transition has had one frame to apply.
        /// </summary>
        /// <param name="core">Active runtime core driving the current frame.</param>
        /// <param name="step">Current authored step.</param>
        void AdvanceSceneActionStep(Core core, SceneMemoryProbeStep step) {
            if (!CurrentStepActionIssuedValue) {
                ExecuteSceneAction(core, step);
                CurrentStepActionIssuedValue = true;
                return;
            }

            EmitMeasurement(core, step);
            AdvanceToNextStep();
        }

        /// <summary>
        /// Issues the scene-manager operation requested by the current authored step.
        /// </summary>
        /// <param name="core">Active runtime core driving the current frame.</param>
        /// <param name="step">Current authored step.</param>
        void ExecuteSceneAction(Core core, SceneMemoryProbeStep step) {
            SceneManager sceneManager = core.SceneManager ?? throw new InvalidOperationException("Scene memory probes require one initialized scene manager.");
            if (string.IsNullOrWhiteSpace(step.SceneId)) {
                throw new InvalidOperationException($"Scene memory probe step '{CurrentStepIndexValue}' requires one scene id for action '{step.ActionKind}'.");
            }

            if (step.ActionKind == SceneMemoryProbeActionKind.LoadSceneSingle) {
                sceneManager.LoadScene(step.SceneId, SceneLoadMode.Single);
                return;
            }
            if (step.ActionKind == SceneMemoryProbeActionKind.LoadSceneAdditive) {
                sceneManager.LoadScene(step.SceneId, SceneLoadMode.Additive);
                return;
            }
            if (step.ActionKind == SceneMemoryProbeActionKind.UnloadScene) {
                sceneManager.UnloadScene(step.SceneId);
                return;
            }

            throw new InvalidOperationException($"Scene memory probe does not support scene action '{step.ActionKind}' as one scene-manager operation.");
        }

        /// <summary>
        /// Emits one formatted probe checkpoint for the current step.
        /// </summary>
        /// <param name="core">Active runtime core driving the current frame.</param>
        /// <param name="step">Current authored step that completed.</param>
        void EmitMeasurement(Core core, SceneMemoryProbeStep step) {
            return;

            RuntimeDiagnosticsService diagnosticsService = core.RuntimeDiagnosticsService;
            if (diagnosticsService != null) {
                diagnosticsService.CaptureMemoryCounters(MemoryCountersValue);
            } else {
                MemoryCountersValue.Reset();
            }

            SceneManager sceneManager = core.SceneManager;
            ObjectManager objectManager = core.ObjectManager;
            SceneMemoryProbeMeasurement measurement = new SceneMemoryProbeMeasurement {
                ProbeName = ProbeName ?? string.Empty,
                CycleIndex = CurrentCycleIndexValue,
                StepIndex = CurrentStepIndexValue,
                Label = step.Label ?? string.Empty,
                ActionKind = step.ActionKind,
                ResidentBytes = MemoryCountersValue.ResidentBytes,
                CommittedBytes = MemoryCountersValue.CommittedBytes,
                LoadedSceneIds = BuildLoadedSceneIds(sceneManager),
                Drawables2DCount = objectManager == null ? 0 : objectManager.Drawables2D.Count,
                Drawables3DCount = objectManager == null ? 0 : objectManager.Drawables3D.Count,
                DrawCallCount = core.LastRenderManager3DDrawCallCount,
                ActiveOwnedTextureCount = sceneManager == null ? 0 : sceneManager.ActiveOwnedTextureReferenceCount,
                ActiveOwnedFontCount = sceneManager == null ? 0 : sceneManager.ActiveOwnedFontReferenceCount,
                ActiveOwnedModelCount = sceneManager == null ? 0 : sceneManager.ActiveOwnedModelReferenceCount,
                ActiveOwnedMaterialCount = sceneManager == null ? 0 : sceneManager.ActiveOwnedMaterialReferenceCount
            };
            Logger.WriteLine(SceneMemoryProbeLogFormatter.Format(measurement));
            NativeOwnership.Delete(measurement);
        }

        /// <summary>
        /// Builds the stable comma-separated loaded-scene id list captured in the emitted checkpoint.
        /// </summary>
        /// <param name="sceneManager">Active scene manager that owns the current loaded-scene set.</param>
        /// <returns>Comma-separated loaded-scene id list.</returns>
        static string BuildLoadedSceneIds(SceneManager sceneManager) {
            if (sceneManager == null || sceneManager.LoadedScenes.Count == 0) {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < sceneManager.LoadedScenes.Count; index++) {
                if (index > 0) {
                    builder.Append(',');
                }

                builder.Append(sceneManager.LoadedScenes[index].SceneId);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Advances the probe to the next authored step or loops back to the start when looping is enabled.
        /// </summary>
        void AdvanceToNextStep() {
            CurrentStepActionIssuedValue = false;
            CurrentStepElapsedSecondsValue = 0d;
            CurrentStepIndexValue++;
            if (CurrentStepIndexValue < ResolveStepCount()) {
                return;
            }

            if (!Loop || ResolveStepCount() == 0) {
                CompletedValue = true;
                return;
            }

            CurrentCycleIndexValue++;
            CurrentStepIndexValue = 0;
        }

        /// <summary>
        /// Resolves the current authored step and validates that the step entry is present.
        /// </summary>
        /// <returns>Current authored step.</returns>
        SceneMemoryProbeStep ResolveCurrentStep() {
            if (CurrentStepIndexValue < 0 || CurrentStepIndexValue >= ResolveStepCount()) {
                throw new InvalidOperationException("Scene memory probe attempted to resolve one step outside the authored step range.");
            }

            SceneMemoryProbeStep step = Steps[CurrentStepIndexValue];
            if (step == null) {
                throw new InvalidOperationException($"Scene memory probe step '{CurrentStepIndexValue}' must not be null.");
            }

            return step;
        }

        /// <summary>
        /// Returns the authored step count while tolerating a null authored array as one empty probe.
        /// </summary>
        /// <returns>Authored step count.</returns>
        int ResolveStepCount() {
            return Steps == null ? 0 : Steps.Length;
        }

        /// <summary>
        /// Validates the authored probe configuration before execution begins.
        /// </summary>
        void ValidateConfiguration() {
            if (double.IsNaN(InitialDelaySeconds) || double.IsInfinity(InitialDelaySeconds) || InitialDelaySeconds < 0d) {
                throw new InvalidOperationException("Scene memory probe initial delay must be one finite non-negative value.");
            }

            for (int index = 0; index < ResolveStepCount(); index++) {
                SceneMemoryProbeStep step = Steps[index] ?? throw new InvalidOperationException($"Scene memory probe step '{index}' must not be null.");
                if (double.IsNaN(step.DurationSeconds) || double.IsInfinity(step.DurationSeconds) || step.DurationSeconds < 0d) {
                    throw new InvalidOperationException($"Scene memory probe step '{index}' must define one finite non-negative duration.");
                }
            }
        }

        /// <summary>
        /// Resets the mutable runtime state used to drive probe execution.
        /// </summary>
        void ResetRuntimeState() {
            CurrentStepElapsedSecondsValue = 0d;
            InitialDelayElapsedSecondsValue = 0d;
            CurrentStepIndexValue = 0;
            CurrentCycleIndexValue = 0;
            CurrentStepActionIssuedValue = false;
            CompletedValue = false;
        }
    }
}
