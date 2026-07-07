using System.Diagnostics;

namespace helengine {
    /// <summary>
    /// Central runtime coordinating managers, lifecycle, and shared services.
    /// </summary>
    public class Core : IDisposable {
        /// <summary>
        /// Cached content managers keyed by stream-source identity.
        /// </summary>
        readonly Dictionary<IContentStreamSource, ContentManager> ContentManagersBySource;

        /// <summary>
        /// Synchronizes content-manager creation for shared sources.
        /// </summary>
        readonly object ContentManagerLock;

        /// <summary>
        /// Fixed-step scheduler used to feed attached physics runtimes from host frame time.
        /// </summary>
        PhysicsFixedStepScheduler PhysicsSchedulerValue;

        /// <summary>
        /// Attached physics runtime advanced by the core update loop when one has been configured.
        /// </summary>
        IPhysicsRuntime PhysicsRuntimeValue;
        /// <summary>
        /// Clipboard service used by textbox shortcut commands.
        /// </summary>
        ITextClipboardService TextClipboardServiceValue;
        /// <summary>
        /// Registry that stores the active textbox shortcut bindings.
        /// </summary>
        TextBoxShortcutRegistry TextBoxShortcutRegistryValue;
        /// <summary>
        /// Tracks elapsed wall-clock time for the parameterless update path.
        /// </summary>
        readonly Stopwatch UpdateStopwatchValue;
        /// <summary>
        /// Tracks elapsed wall-clock time for the draw-duration sampling path without allocating one new stopwatch each frame.
        /// </summary>
        readonly Stopwatch DrawStopwatchValue;
        /// <summary>
        /// Stores whether one previous measured update timestamp has been captured yet.
        /// </summary>
        bool HasPreviousMeasuredUpdateSeconds;
        /// <summary>
        /// Stores the previous measured elapsed update time returned by the host clock.
        /// </summary>
        double PreviousMeasuredUpdateSeconds;
        /// <summary>
        /// Optional diagnostics sink that receives high-frequency core update stage labels when a host explicitly provides one.
        /// </summary>
        IRuntimeUpdateStageDiagnosticsProvider UpdateStageDiagnosticsProviderValue;

        /// <summary>
        /// Initializes a new core instance with default initialization options.
        /// </summary>
        public Core() : this(new CoreInitializationOptions()) { }

        /// <summary>
        /// Initializes a new core instance and registers the static singleton reference.
        /// </summary>
        /// <param name="options">Initialization options that control ordering and list sizing.</param>
        public Core(CoreInitializationOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            ContentManagersBySource = new Dictionary<IContentStreamSource, ContentManager>();
            ContentManagerLock = new object();
            Instance = this;
            InitializationOptions = options;
            InitializationOptions.Normalize();
            PhysicsSchedulerValue = CreatePhysicsScheduler(InitializationOptions);
            Input = new InputSystem();
            StandardPlatformInput = new StandardPlatformInput(Input);
            PointerInteractionSystem = new PointerInteractionSystem(this, Input);
            TextClipboardServiceValue = new NullTextClipboardService();
            TextBoxShortcutRegistryValue = new TextBoxShortcutRegistry();
            UpdateStopwatchValue = Stopwatch.StartNew();
            DrawStopwatchValue = new Stopwatch();
            ResolvedPerformanceOverlayFontScale = 1f;
            ResolvedPerformanceOverlayPadding = new int2(0, 0);
            ResolvedPerformanceOverlayUpdateText = string.Empty;
            ResolvedPerformanceOverlayRenderText = string.Empty;
            ResolvedPerformanceOverlayDetailText = string.Empty;
            ResolvedPerformanceOverlayAdditionalText = string.Empty;
        }

        /// <summary>
        /// Gets the singleton core instance.
        /// </summary>
        public static Core Instance { get; private set; }

        /// <summary>
        /// Gets the initialization options used to configure core systems.
        /// </summary>
        public CoreInitializationOptions InitializationOptions { get; private set; }

        /// <summary>
        /// Gets the default content manager backed by <see cref="CoreInitializationOptions.ContentStreamSource"/>.
        /// </summary>
        public ContentManager ContentManager => GetContentManager();

        /// <summary>
        /// Gets the object manager responsible for updating entities and components.
        /// </summary>
        public ObjectManager ObjectManager { get; private set; }

        /// <summary>
        /// Gets the host-owned authored entity factory exposed to scene-authoring consumers.
        /// </summary>
        public IEntityFactory EntityFactory { get; private set; }

        /// <summary>
        /// Gets the 3D render manager.
        /// </summary>
        public RenderManager3D RenderManager3D { get; private set; }

        /// <summary>
        /// Gets the most recent measured duration spent executing <see cref="RenderManager3D.Draw"/>.
        /// </summary>
        public double LastRenderManager3DDrawMilliseconds { get; private set; }

        /// <summary>
        /// Gets the draw-call count reported by the most recent render-manager draw.
        /// </summary>
        public int LastRenderManager3DDrawCallCount { get; private set; }

        /// <summary>
        /// Gets whether the active runtime has published platform-specific performance overlay metrics.
        /// </summary>
        public bool UsesPerformanceOverlayMetrics { get; private set; }

        /// <summary>
        /// Gets the most recent triangle-setup duration published for the FPS overlay.
        /// </summary>
        public double PerformanceOverlayTriangleSetupMilliseconds { get; private set; }

        /// <summary>
        /// Gets the most recent triangle-preparation duration published for the FPS overlay.
        /// </summary>
        public double PerformanceOverlayTrianglePrepMilliseconds { get; private set; }

        /// <summary>
        /// Gets the most recent triangle-emission duration published for the FPS overlay.
        /// </summary>
        public double PerformanceOverlayTriangleEmitMilliseconds { get; private set; }

        /// <summary>
        /// Gets the most recent packet-encode duration published for the FPS overlay.
        /// </summary>
        public double PerformanceOverlayPacketEncodeMilliseconds { get; private set; }

        /// <summary>
        /// Gets the most recent submit duration published for the FPS overlay.
        /// </summary>
        public double PerformanceOverlaySubmitMilliseconds { get; private set; }

        /// <summary>
        /// Gets the most recent wait duration published for the FPS overlay.
        /// </summary>
        public double PerformanceOverlayWaitMilliseconds { get; private set; }

        /// <summary>
        /// Gets the most recent submitted-triangle count published for the FPS overlay.
        /// </summary>
        public int PerformanceOverlaySubmittedTriangleCount { get; private set; }

        /// <summary>
        /// Gets the most recent dispatch count published for the FPS overlay.
        /// </summary>
        public int PerformanceOverlayDispatchCount { get; private set; }

        /// <summary>
        /// Gets one optional platform-owned update-row text override for the FPS overlay.
        /// </summary>
        public string PerformanceOverlayUpdateText { get; private set; }

        /// <summary>
        /// Gets one optional platform-owned render-row text override for the FPS overlay.
        /// </summary>
        public string PerformanceOverlayRenderText { get; private set; }

        /// <summary>
        /// Gets one optional platform-owned detail-row text override for the FPS overlay.
        /// </summary>
        public string PerformanceOverlayDetailText { get; private set; }

        /// <summary>
        /// Gets one optional platform-owned multi-line text block rendered beneath the FPS overlay rows.
        /// </summary>
        public string PerformanceOverlayAdditionalText { get; private set; }

        /// <summary>
        /// Gets whether the active runtime owns final presentation of the FPS overlay rows instead of relying on scene text drawables.
        /// </summary>
        public bool UsesPlatformOwnedPerformanceOverlayPresentation { get; private set; }

        /// <summary>
        /// Gets the font assigned to the resolved FPS overlay presentation contract.
        /// </summary>
        public FontAsset ResolvedPerformanceOverlayFont { get; private set; }

        /// <summary>
        /// Gets the font scale assigned to the resolved FPS overlay presentation contract.
        /// </summary>
        public float ResolvedPerformanceOverlayFontScale { get; private set; }

        /// <summary>
        /// Gets the resolved overlay padding assigned to the platform-owned FPS presentation contract.
        /// </summary>
        public int2 ResolvedPerformanceOverlayPadding { get; private set; }

        /// <summary>
        /// Gets the final resolved update-row text published for platform-owned FPS overlay presentation.
        /// </summary>
        public string ResolvedPerformanceOverlayUpdateText { get; private set; }

        /// <summary>
        /// Gets the final resolved render-row text published for platform-owned FPS overlay presentation.
        /// </summary>
        public string ResolvedPerformanceOverlayRenderText { get; private set; }

        /// <summary>
        /// Gets the final resolved detail-row text published for platform-owned FPS overlay presentation.
        /// </summary>
        public string ResolvedPerformanceOverlayDetailText { get; private set; }

        /// <summary>
        /// Gets the final resolved additional text block published for platform-owned FPS overlay presentation.
        /// </summary>
        public string ResolvedPerformanceOverlayAdditionalText { get; private set; }

        /// <summary>
        /// Gets the 2D render manager.
        /// </summary>
        public RenderManager2D RenderManager2D { get; private set; }

        /// <summary>
        /// Gets the portable input system that resolves logical actions from raw frame data.
        /// </summary>
        public InputSystem Input { get; private set; }

        /// <summary>
        /// Gets the portable input system that resolves logical actions from raw frame data.
        /// </summary>
        public InputSystem InputSystem => Input;

        /// <summary>
        /// Gets the engine-owned helper that resolves platform-standard actions such as accept and return.
        /// </summary>
        public StandardPlatformInput StandardPlatformInput { get; private set; }

        /// <summary>
        /// Gets the pointer interaction router used to translate raw pointer state into hover and press events.
        /// </summary>
        public PointerInteractionSystem PointerInteractionSystem { get; private set; }

        /// <summary>
        /// Gets the elapsed time, in seconds, that was applied during the most recent update.
        /// </summary>
        public double FrameDeltaSeconds { get; private set; }
        /// <summary>
        /// Gets the platform metadata injected by the active host during core initialization.
        /// </summary>
        public PlatformInfo PlatformInfo { get; private set; }
        /// <summary>
        /// Gets the elapsed scaled update time, in seconds, that components can read during the current update.
        /// </summary>
        public float DeltaTime { get; private set; }
        /// <summary>
        /// Gets the elapsed unscaled update time, in seconds, that components can read during the current update.
        /// </summary>
        public float UnscaledDeltaTime { get; private set; }

        /// <summary>
        /// Gets the accumulated update time, in seconds, since this core instance started running.
        /// </summary>
        public double TotalElapsedSeconds { get; private set; }

        /// <summary>
        /// Gets the fixed-step scheduler that converts host frame time into physics simulation steps.
        /// </summary>
        public PhysicsFixedStepScheduler PhysicsScheduler => PhysicsSchedulerValue;

        /// <summary>
        /// Gets the total fixed-step physics time predicted to be consumed during the current update based on the scheduler accumulator and catch-up cap.
        /// </summary>
        public double PredictedPhysicsStepSeconds { get; private set; }

        /// <summary>
        /// Gets the currently attached pluggable physics runtime, when one has been configured.
        /// </summary>
        public IPhysicsRuntime PhysicsRuntime => PhysicsRuntimeValue;

        /// <summary>
        /// Gets the packaged scene asset resolver configured for the current runtime target.
        /// </summary>
        public RuntimeSceneAssetReferenceResolver SceneAssetReferenceResolver { get; private set; }

        /// <summary>
        /// Gets the runtime scene loader configured for packaged player scene assets.
        /// This service remains an internal collaborator of <see cref="SceneManager"/> and should not be used as a public transition seam.
        /// </summary>
        internal RuntimeSceneLoadService SceneLoadService { get; private set; }

        /// <summary>
        /// Gets the runtime scene manager configured for built scene tracking and notifications.
        /// </summary>
        public SceneManager SceneManager { get; private set; }

        /// <summary>
        /// Gets the runtime diagnostics service used to capture shared diagnostics snapshots.
        /// </summary>
        public RuntimeDiagnosticsService RuntimeDiagnosticsService { get; private set; }

        /// <summary>
        /// Gets the runtime component registry used to materialize packaged scene components.
        /// </summary>
        public RuntimeComponentRegistry SceneRuntimeComponentRegistry { get; private set; }

        /// <summary>
        /// Gets the most recent core-side scene-transition stage recorded for runtime diagnostics.
        /// </summary>
        public string LastSceneTransitionStage { get; private set; }

        /// <summary>
        /// Gets the clipboard service used by textbox shortcut commands.
        /// </summary>
        public ITextClipboardService TextClipboardService => TextClipboardServiceValue;

        /// <summary>
        /// Gets the registry that stores the active textbox shortcut bindings.
        /// </summary>
        public TextBoxShortcutRegistry TextBoxShortcutRegistry => TextBoxShortcutRegistryValue;

        /// <summary>
        /// Updates the platform-specific performance overlay metrics consumed by the FPS component.
        /// </summary>
        /// <param name="usesPerformanceOverlayMetrics">Whether the active runtime wants the FPS overlay to show custom metrics.</param>
        /// <param name="triangleSetupMilliseconds">Most recent triangle-setup duration in milliseconds.</param>
        /// <param name="trianglePrepMilliseconds">Most recent triangle-preparation duration in milliseconds.</param>
        /// <param name="triangleEmitMilliseconds">Most recent triangle-emission duration in milliseconds.</param>
        /// <param name="packetEncodeMilliseconds">Most recent packet-encode duration in milliseconds.</param>
        /// <param name="submitMilliseconds">Most recent submit duration in milliseconds.</param>
        /// <param name="waitMilliseconds">Most recent wait duration in milliseconds.</param>
        /// <param name="submittedTriangleCount">Most recent submitted-triangle count.</param>
        /// <param name="dispatchCount">Most recent dispatch count.</param>
        public void SetPerformanceOverlayMetrics(
            bool usesPerformanceOverlayMetrics,
            double triangleSetupMilliseconds,
            double trianglePrepMilliseconds,
            double triangleEmitMilliseconds,
            double packetEncodeMilliseconds,
            double submitMilliseconds,
            double waitMilliseconds,
            int submittedTriangleCount,
            int dispatchCount) {
            UsesPerformanceOverlayMetrics = usesPerformanceOverlayMetrics;
            PerformanceOverlayTriangleSetupMilliseconds = triangleSetupMilliseconds;
            PerformanceOverlayTrianglePrepMilliseconds = trianglePrepMilliseconds;
            PerformanceOverlayTriangleEmitMilliseconds = triangleEmitMilliseconds;
            PerformanceOverlayPacketEncodeMilliseconds = packetEncodeMilliseconds;
            PerformanceOverlaySubmitMilliseconds = submitMilliseconds;
            PerformanceOverlayWaitMilliseconds = waitMilliseconds;
            PerformanceOverlaySubmittedTriangleCount = submittedTriangleCount;
            PerformanceOverlayDispatchCount = dispatchCount;
            PerformanceOverlayUpdateText = string.Empty;
            PerformanceOverlayRenderText = string.Empty;
            PerformanceOverlayDetailText = string.Empty;
            PerformanceOverlayAdditionalText = string.Empty;
        }

        /// <summary>
        /// Updates optional platform-owned text rows consumed by the FPS overlay when one runtime wants to surface custom diagnostics labels.
        /// </summary>
        /// <param name="usesPerformanceOverlayMetrics">Whether the active runtime wants the FPS overlay to show custom metrics.</param>
        /// <param name="updateText">Visible update-row text override.</param>
        /// <param name="renderText">Visible render-row text override.</param>
        /// <param name="detailText">Visible detail-row text override.</param>
        /// <param name="additionalText">Optional multi-line text block rendered beneath the main overlay rows.</param>
        public void SetPerformanceOverlayTextRows(
            bool usesPerformanceOverlayMetrics,
            string updateText,
            string renderText,
            string detailText,
            string additionalText) {
            UsesPerformanceOverlayMetrics = usesPerformanceOverlayMetrics;
            PerformanceOverlayUpdateText = usesPerformanceOverlayMetrics ? updateText ?? string.Empty : string.Empty;
            PerformanceOverlayRenderText = usesPerformanceOverlayMetrics ? renderText ?? string.Empty : string.Empty;
            PerformanceOverlayDetailText = usesPerformanceOverlayMetrics ? detailText ?? string.Empty : string.Empty;
            PerformanceOverlayAdditionalText = usesPerformanceOverlayMetrics ? additionalText ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Stores whether the active runtime owns final FPS overlay presentation instead of using scene text drawables.
        /// </summary>
        /// <param name="usesPlatformOwnedPresentation">True when the runtime should consume resolved FPS overlay rows directly.</param>
        public void SetPlatformOwnedPerformanceOverlayPresentation(bool usesPlatformOwnedPresentation) {
            UsesPlatformOwnedPerformanceOverlayPresentation = usesPlatformOwnedPresentation;
            if (!usesPlatformOwnedPresentation) {
                ClearResolvedPerformanceOverlayPresentation();
            }
        }

        /// <summary>
        /// Updates the final resolved FPS overlay presentation state consumed by one platform-owned overlay renderer.
        /// </summary>
        /// <param name="font">Font chosen by the FPS component for the visible overlay.</param>
        /// <param name="fontScale">Font scale chosen by the FPS component for the visible overlay.</param>
        /// <param name="padding">Resolved top-left overlay padding in screen pixels.</param>
        /// <param name="updateText">Final visible update-row text.</param>
        /// <param name="renderText">Final visible render-row text.</param>
        /// <param name="detailText">Final visible optional detail-row text.</param>
        /// <param name="additionalText">Final visible optional additional multi-line text block.</param>
        public void SetResolvedPerformanceOverlayPresentation(
            FontAsset font,
            float fontScale,
            int2 padding,
            string updateText,
            string renderText,
            string detailText,
            string additionalText) {
            if (!UsesPlatformOwnedPerformanceOverlayPresentation) {
                ClearResolvedPerformanceOverlayPresentation();
                return;
            }

            ResolvedPerformanceOverlayFont = font;
            ResolvedPerformanceOverlayFontScale = fontScale;
            ResolvedPerformanceOverlayPadding = padding;
            ResolvedPerformanceOverlayUpdateText = updateText ?? string.Empty;
            ResolvedPerformanceOverlayRenderText = renderText ?? string.Empty;
            ResolvedPerformanceOverlayDetailText = detailText ?? string.Empty;
            ResolvedPerformanceOverlayAdditionalText = additionalText ?? string.Empty;
        }

        /// <summary>
        /// Clears the final resolved FPS overlay presentation state when the runtime no longer owns presentation.
        /// </summary>
        void ClearResolvedPerformanceOverlayPresentation() {
            ResolvedPerformanceOverlayFont = null;
            ResolvedPerformanceOverlayFontScale = 1f;
            ResolvedPerformanceOverlayPadding = new int2(0, 0);
            ResolvedPerformanceOverlayUpdateText = string.Empty;
            ResolvedPerformanceOverlayRenderText = string.Empty;
            ResolvedPerformanceOverlayDetailText = string.Empty;
            ResolvedPerformanceOverlayAdditionalText = string.Empty;
        }

        /// <summary>
        /// Initializes core systems with rendering and input capture.
        /// </summary>
        /// <param name="render3D">3D renderer instance.</param>
        /// <param name="render2D">2D renderer instance.</param>
        /// <param name="input">Platform-specific input backend instance.</param>
        /// <param name="platformInfo">Runtime platform metadata injected by the active host.</param>
        public virtual void Initialize(RenderManager3D render3D, RenderManager2D render2D, IInputBackend input, PlatformInfo platformInfo) {
            Initialize(render3D, render2D, input, platformInfo, InitializationOptions);
        }

        /// <summary>
        /// Initializes core systems with rendering, input, and initialization options.
        /// </summary>
        /// <param name="render3D">3D renderer instance.</param>
        /// <param name="render2D">2D renderer instance.</param>
        /// <param name="input">Platform-specific input backend instance.</param>
        /// <param name="platformInfo">Runtime platform metadata injected by the active host.</param>
        /// <param name="options">Initialization options that control ordering and list sizing.</param>
        public virtual void Initialize(
            RenderManager3D render3D,
            RenderManager2D render2D,
            IInputBackend input,
            PlatformInfo platformInfo,
            CoreInitializationOptions options) {
            if (platformInfo == null) {
                throw new ArgumentNullException(nameof(platformInfo));
            }

            RenderManager3D = render3D;
            RenderManager2D = render2D;
            Input.SetBackend(input);
            PlatformInfo = platformInfo;

            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            options.Normalize();
            InitializationOptions = options;
            PhysicsSchedulerValue = CreatePhysicsScheduler(options);
            StandardPlatformInput.Configure(options.StandardPlatformInputConfiguration);

            ObjectManager = new ObjectManager(options);
            EntityFactory = CreateEntityFactory();
            if (EntityFactory == null) {
                throw new InvalidOperationException("Core entity factory creation must return one factory instance.");
            }

            ContentManager contentManager = GetContentManager();
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            SceneAssetReferenceResolver = new RuntimeSceneAssetReferenceResolver(contentManager);
            SceneRuntimeComponentRegistry = RuntimeComponentRegistry.CreateDefault();
            SceneLoadService = new RuntimeSceneLoadService(SceneAssetReferenceResolver, SceneRuntimeComponentRegistry);
            SceneManager = CreateSceneManager(contentManager, InitializationOptions.SceneCatalog);
            RuntimeDiagnosticsService = new RuntimeDiagnosticsService(
                InitializationOptions.RuntimeDiagnosticsProvider,
                SceneManager,
                ObjectManager);
            if (InitializationOptions.RuntimeDiagnosticsProvider is IRuntimeUpdateStageDiagnosticsProvider stageDiagnosticsProvider) {
                UpdateStageDiagnosticsProviderValue = stageDiagnosticsProvider;
            } else {
                UpdateStageDiagnosticsProviderValue = null;
            }
        }

        /// <summary>
        /// Creates the authored entity factory that should be exposed by the active host core.
        /// </summary>
        /// <returns>Host-owned authored entity factory.</returns>
        protected virtual IEntityFactory CreateEntityFactory() {
            return new RuntimeEntityFactory();
        }

        /// <summary>
        /// Registers one additional packaged-scene component deserializer with the active runtime scene loader.
        /// </summary>
        /// <param name="deserializer">Deserializer to register for packaged runtime scene loading.</param>
        public void RegisterRuntimeComponentDeserializer(IRuntimeComponentDeserializer deserializer) {
            if (deserializer == null) {
                throw new ArgumentNullException(nameof(deserializer));
            }
            if (SceneRuntimeComponentRegistry == null) {
                throw new InvalidOperationException("Core must be initialized before runtime component deserializers can be registered.");
            }

            SceneRuntimeComponentRegistry.Register(deserializer);
        }

        /// <summary>
        /// Replaces the clipboard service used by textbox shortcut commands.
        /// </summary>
        /// <param name="clipboardService">Clipboard service exposed by the active host.</param>
        public void SetTextClipboardService(ITextClipboardService clipboardService) {
            if (clipboardService == null) {
                throw new ArgumentNullException(nameof(clipboardService));
            }

            TextClipboardServiceValue = clipboardService;
        }

        /// <summary>
        /// Gets the default content manager configured for the current core instance.
        /// </summary>
        /// <returns>Cached content manager backed by the configured content stream source.</returns>
        public ContentManager GetContentManager() {
            return GetContentManager(InitializationOptions.ContentStreamSource);
        }

        /// <summary>
        /// Gets a cached content manager for a specific content stream source, creating it the first time that source is requested.
        /// </summary>
        /// <param name="streamSource">Source used to open runtime content streams.</param>
        /// <returns>Cached content manager for the requested source.</returns>
        public ContentManager GetContentManager(IContentStreamSource streamSource) {
            if (streamSource == null) {
                throw new ArgumentNullException(nameof(streamSource));
            }

            lock (ContentManagerLock) {
                if (ContentManagersBySource.TryGetValue(streamSource, out ContentManager contentManager)) {
                    return contentManager;
                }

                contentManager = new ContentManager(streamSource);
                ContentManagersBySource.Add(streamSource, contentManager);
                return contentManager;
            }
        }

        /// <summary>
        /// Attaches one pluggable physics runtime to the core update loop.
        /// </summary>
        /// <param name="runtime">Physics runtime that should receive fixed simulation steps.</param>
        public void AttachPhysicsRuntime(IPhysicsRuntime runtime) {
            if (runtime == null) {
                throw new ArgumentNullException(nameof(runtime));
            }

            PhysicsRuntimeValue = runtime;
            PhysicsSchedulerValue.Reset();
            PredictedPhysicsStepSeconds = 0d;
        }

        /// <summary>
        /// Detaches the current physics runtime and clears any unconsumed fixed-step accumulator state.
        /// </summary>
        public void DetachPhysicsRuntime() {
            PhysicsRuntimeValue = null;
            PhysicsSchedulerValue.Reset();
            PredictedPhysicsStepSeconds = 0d;
        }

        /// <summary>
        /// Clears any accumulated fixed-step timing debt so the next scene starts from a clean physics schedule.
        /// </summary>
        public void ResetPhysicsTimingState() {
            PhysicsSchedulerValue.Reset();
            PredictedPhysicsStepSeconds = 0d;
        }

        /// <summary>
        /// Advances the engine update loop using real elapsed time measured between parameterless update calls.
        /// </summary>
        public virtual void Update() {
            double currentMeasuredUpdateSeconds = GetCurrentMeasuredUpdateSeconds();
            double elapsedSeconds = ResolveMeasuredElapsedSeconds(currentMeasuredUpdateSeconds);
            AdvanceUpdate(elapsedSeconds, currentMeasuredUpdateSeconds);
        }

        /// <summary>
        /// Advances the engine update loop for objects and input using one explicit elapsed frame time.
        /// </summary>
        /// <param name="elapsedSeconds">Elapsed frame time in seconds supplied by the host runtime.</param>
        public virtual void Update(double elapsedSeconds) {
            ValidateElapsedSeconds(elapsedSeconds);
            double currentMeasuredUpdateSeconds = GetCurrentMeasuredUpdateSeconds();
            AdvanceUpdate(elapsedSeconds, currentMeasuredUpdateSeconds);
        }

        /// <summary>
        /// Executes the engine draw cycle.
        /// </summary>
        public virtual void Draw() {
            LastSceneTransitionStage = "DrawBegin";
            if (InitializationOptions.CommitPendingSceneOperationsDuringDraw) {
                LastSceneTransitionStage = "BeforeCompleteFrameBoundary";
                CompleteFrameBoundary();
                LastSceneTransitionStage = "AfterCompleteFrameBoundary";
            }

            LastSceneTransitionStage = "BeforeRenderManager3DDraw";
            LastRenderManager3DDrawMilliseconds = MeasureRenderManager3DDrawMilliseconds();
            LastSceneTransitionStage = "AfterRenderManager3DDraw";
            LastRenderManager3DDrawCallCount = RenderManager3D == null ? 0 : RenderManager3D.LastDrawCallCount;
            FPSComponent.RecordRenderFrame();
            DebugComponent.RecordRenderFrame();
            LastSceneTransitionStage = "DrawEnd";
        }

        /// <summary>
        /// Commits queued scene operations after the active host reaches one frame-boundary safe point for resource release.
        /// </summary>
        public virtual void CompleteFrameBoundary() {
            if (SceneManager != null) {
                LastSceneTransitionStage = "CompleteFrameBoundaryCommitBegin";
                SceneManager.CommitPendingOperationsAtFrameBoundary();
                LastSceneTransitionStage = "CompleteFrameBoundaryCommitEnd";
            }
        }

        /// <summary>
        /// Releases managed resources for render managers.
        /// </summary>
        public void Dispose() {
            if (RenderManager3D != null) {
                RenderManager3D.Dispose();
            }
            if (RenderManager2D != null) {
                RenderManager2D.Dispose();
            }
            RuntimeDiagnosticsService = null;
        }

        /// <summary>
        /// Validates one host-supplied elapsed frame time before it is accumulated into core timing state.
        /// </summary>
        /// <param name="elapsedSeconds">Elapsed frame time in seconds.</param>
        void ValidateElapsedSeconds(double elapsedSeconds) {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds)) {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed frame time must be finite.");
            }

            if (elapsedSeconds < 0d) {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed frame time cannot be negative.");
            }
        }

        /// <summary>
        /// Returns the current measured elapsed time used to resolve parameterless update deltas.
        /// </summary>
        /// <returns>Total elapsed wall-clock seconds from the active host clock.</returns>
        protected virtual double GetCurrentMeasuredUpdateSeconds() {
            return UpdateStopwatchValue.Elapsed.TotalMilliseconds / 1000d;
        }

        /// <summary>
        /// Measures one <see cref="RenderManager3D.Draw"/> execution and returns the elapsed duration in milliseconds.
        /// </summary>
        /// <returns>Measured draw duration in milliseconds.</returns>
        protected virtual double MeasureRenderManager3DDrawMilliseconds() {
            DrawStopwatchValue.Restart();
            RenderManager3D.Draw();
            DrawStopwatchValue.Stop();
            return DrawStopwatchValue.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Computes elapsed seconds from the measured update time stream.
        /// </summary>
        /// <param name="currentMeasuredUpdateSeconds">Current measured elapsed update time in seconds.</param>
        /// <returns>Elapsed seconds since the previous measured update, or zero on the first update.</returns>
        double ResolveMeasuredElapsedSeconds(double currentMeasuredUpdateSeconds) {
            if (!HasPreviousMeasuredUpdateSeconds) {
                return 0d;
            }

            return currentMeasuredUpdateSeconds - PreviousMeasuredUpdateSeconds;
        }

        /// <summary>
        /// Applies one elapsed update slice to cached timing state and the normal update pipeline.
        /// </summary>
        /// <param name="elapsedSeconds">Elapsed update time in seconds.</param>
        /// <param name="currentMeasuredUpdateSeconds">Current measured elapsed update time captured for this update.</param>
        void AdvanceUpdate(double elapsedSeconds, double currentMeasuredUpdateSeconds) {
            float elapsedSecondsFloat = (float)elapsedSeconds;
            FrameDeltaSeconds = elapsedSeconds;
            UnscaledDeltaTime = elapsedSecondsFloat;
            DeltaTime = elapsedSecondsFloat;
            TotalElapsedSeconds += elapsedSeconds;
            PreviousMeasuredUpdateSeconds = currentMeasuredUpdateSeconds;
            HasPreviousMeasuredUpdateSeconds = true;
            PredictedPhysicsStepSeconds = ResolvePredictedPhysicsStepSeconds(elapsedSeconds);

            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.BeforeInputEarlyUpdatePhaseId);
            bool shouldRecordUpdateStages = UpdateStageDiagnosticsProviderValue != null;
            if (shouldRecordUpdateStages) {
                RecordUpdateStage("BeforeInputEarlyUpdate");
            }
            Input.EarlyUpdate();
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.AfterInputEarlyUpdatePhaseId);
            if (shouldRecordUpdateStages) {
                RecordUpdateStage("AfterInputEarlyUpdate");
                RecordUpdateStage("BeforeFpsRecordUpdateFrame");
            }
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.BeforeFpsRecordUpdateFramePhaseId);
            FPSComponent.RecordUpdateFrame();
            DebugComponent.RecordUpdateFrame();
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.AfterFpsRecordUpdateFramePhaseId);
            if (shouldRecordUpdateStages) {
                RecordUpdateStage("AfterFpsRecordUpdateFrame");
            }

            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.BeforeObjectManagerUpdatePhaseId);
            if (shouldRecordUpdateStages) {
                RecordUpdateStage("BeforeObjectManagerUpdate");
            }
            ObjectManager.Update();
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.AfterObjectManagerUpdatePhaseId);
            if (shouldRecordUpdateStages) {
                RecordUpdateStage("AfterObjectManagerUpdate");
            }
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.BeforeUpdatePhysicsPhaseId);
            if (shouldRecordUpdateStages) {
                RecordUpdateStage("BeforeUpdatePhysics");
            }
            UpdatePhysics(elapsedSeconds);
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.AfterUpdatePhysicsPhaseId);
            if (shouldRecordUpdateStages) {
                RecordUpdateStage("AfterUpdatePhysics");
            }

            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.BeforeInputUpdatePhaseId);
            if (shouldRecordUpdateStages) {
                RecordUpdateStage("BeforeInputUpdate");
            }
            Input.Update();
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.AfterInputUpdatePhaseId);
            if (shouldRecordUpdateStages) {
                RecordUpdateStage("AfterInputUpdate");
                RecordUpdateStage("BeforePointerInteractionSystemUpdate");
            }
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.BeforePointerInteractionSystemUpdatePhaseId);
            PointerInteractionSystem.Update();
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.AfterPointerInteractionSystemUpdatePhaseId);
            if (shouldRecordUpdateStages) {
                RecordUpdateStage("AfterPointerInteractionSystemUpdate");
            }
        }

        /// <summary>
        /// Stores one core update stage and publishes it to hosts that can render live diagnostics while the update is still executing.
        /// </summary>
        /// <param name="stage">Short stage label describing the next core update boundary.</param>
        void RecordUpdateStage(string stage) {
            ReportSceneTransitionStage(stage);
        }

        /// <summary>
        /// Stores one shared scene-transition stage and forwards it to hosts that render live diagnostics.
        /// </summary>
        /// <param name="stage">Short stage label describing the current runtime transition boundary.</param>
        internal void ReportSceneTransitionStage(string stage) {
            LastSceneTransitionStage = stage ?? string.Empty;
            UpdateStageDiagnosticsProviderValue?.ReportUpdateStage(LastSceneTransitionStage);
        }

        /// <summary>
        /// Creates one fixed-step scheduler from the supplied initialization options.
        /// </summary>
        /// <param name="options">Initialization options that provide the configured physics step.</param>
        /// <returns>Scheduler configured for the current core instance.</returns>
        PhysicsFixedStepScheduler CreatePhysicsScheduler(CoreInitializationOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            return new PhysicsFixedStepScheduler(options.PhysicsFixedStepSeconds);
        }

        /// <summary>
        /// Predicts the amount of fixed-step physics time the current update will actually consume after applying the scheduler accumulator and maximum catch-up step cap.
        /// </summary>
        /// <param name="elapsedSeconds">Elapsed host frame time in seconds for the current update.</param>
        /// <returns>Total fixed-step physics seconds expected to run during this update.</returns>
        double ResolvePredictedPhysicsStepSeconds(double elapsedSeconds) {
            if (PhysicsRuntimeValue == null) {
                return 0d;
            }

            double projectedAccumulatedSeconds = PhysicsSchedulerValue.AccumulatedSeconds + elapsedSeconds;
            int predictedStepCount = (int)(projectedAccumulatedSeconds / PhysicsSchedulerValue.StepSeconds);
            if (predictedStepCount <= 0) {
                return 0d;
            }

            predictedStepCount = Math.Min(predictedStepCount, InitializationOptions.PhysicsMaxStepsPerUpdate);
            return predictedStepCount * PhysicsSchedulerValue.StepSeconds;
        }

        /// <summary>
        /// Creates one runtime scene manager when packaged scene metadata has been injected into the core initialization options.
        /// </summary>
        /// <param name="contentManager">Runtime content manager rooted at the active content path.</param>
        /// <param name="sceneCatalog">Injected runtime scene catalog used to resolve built scenes by stable identifier.</param>
        /// <returns>Configured runtime scene manager, or null when no runtime scene catalog has been supplied.</returns>
        SceneManager CreateSceneManager(ContentManager contentManager, RuntimeSceneCatalog sceneCatalog) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }
            if (sceneCatalog == null && InitializationOptions.ScenePathResolver == null) {
                return null;
            }

            IRuntimeSceneTransitionDiagnosticsProvider sceneTransitionDiagnosticsProvider = null;
            if (InitializationOptions.RuntimeDiagnosticsProvider is IRuntimeSceneTransitionDiagnosticsProvider transitionDiagnosticsProvider) {
                sceneTransitionDiagnosticsProvider = transitionDiagnosticsProvider;
            }

            IRuntimeEntityDisposalDiagnosticsProvider entityDisposalDiagnosticsProvider = null;
            if (InitializationOptions.RuntimeDiagnosticsProvider is IRuntimeEntityDisposalDiagnosticsProvider disposalDiagnosticsProvider) {
                entityDisposalDiagnosticsProvider = disposalDiagnosticsProvider;
            }

            return new SceneManager(
                sceneCatalog,
                contentManager,
                SceneLoadService,
                ObjectManager,
                InitializationOptions.ScenePathResolver,
                sceneTransitionDiagnosticsProvider,
                entityDisposalDiagnosticsProvider);
        }

        /// <summary>
        /// Advances the attached physics runtime using the configured fixed-step scheduler.
        /// </summary>
        /// <param name="elapsedSeconds">Elapsed host frame time in seconds.</param>
        void UpdatePhysics(double elapsedSeconds) {
            if (PhysicsRuntimeValue == null) {
                return;
            }

            PhysicsSchedulerValue.AddElapsedSeconds(elapsedSeconds);
            int consumedStepCount = 0;
            while (consumedStepCount < InitializationOptions.PhysicsMaxStepsPerUpdate && PhysicsSchedulerValue.TryConsumeStep()) {
                PhysicsRuntimeValue.Step(PhysicsSchedulerValue.StepSeconds);
                consumedStepCount++;
            }

        }
    }
}
