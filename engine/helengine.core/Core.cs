using System.Diagnostics;

namespace helengine {
    /// <summary>
    /// Central runtime coordinating managers, lifecycle, and shared services.
    /// </summary>
    public class Core : IDisposable {
        /// <summary>
        /// Cached content managers keyed by normalized root path.
        /// </summary>
        readonly Dictionary<string, ContentManager> ContentManagersByRootPath;

        /// <summary>
        /// Synchronizes content-manager creation for shared roots.
        /// </summary>
        readonly object ContentManagerLock;

        /// <summary>
        /// Fixed-step scheduler used to feed attached physics runtimes from host frame time.
        /// </summary>
        PhysicsFixedStepScheduler PhysicsSchedulerValue;

        /// <summary>
        /// Backing field for the default font asset used by text-heavy components.
        /// </summary>
        FontAsset DefaultFontAssetValue;

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
        /// Stores whether one previous measured update timestamp has been captured yet.
        /// </summary>
        bool HasPreviousMeasuredUpdateSeconds;
        /// <summary>
        /// Stores the previous measured elapsed update time returned by the host clock.
        /// </summary>
        double PreviousMeasuredUpdateSeconds;

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

            ContentManagersByRootPath = new Dictionary<string, ContentManager>(StringComparer.OrdinalIgnoreCase);
            ContentManagerLock = new object();
            Instance = this;
            InitializationOptions = options;
            InitializationOptions.Normalize();
            PhysicsSchedulerValue = CreatePhysicsScheduler(InitializationOptions);
            Input = new InputSystem();
            PointerInteractionSystem = new PointerInteractionSystem(this, Input);
            TextClipboardServiceValue = new NullTextClipboardService();
            TextBoxShortcutRegistryValue = new TextBoxShortcutRegistry();
            UpdateStopwatchValue = Stopwatch.StartNew();
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
        /// Gets the default content manager rooted at <see cref="CoreInitializationOptions.ContentRootPath"/>.
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
        /// Gets the currently attached pluggable physics runtime, when one has been configured.
        /// </summary>
        public IPhysicsRuntime PhysicsRuntime => PhysicsRuntimeValue;

        /// <summary>
        /// Gets or sets the default font asset used by components that need a text font without being configured explicitly.
        /// </summary>
        public FontAsset DefaultFontAsset {
            get { return DefaultFontAssetValue; }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                DefaultFontAssetValue = value;
            }
        }

        /// <summary>
        /// Gets the packaged scene asset resolver configured for the current runtime target.
        /// </summary>
        public RuntimeSceneAssetReferenceResolver SceneAssetReferenceResolver { get; private set; }

        /// <summary>
        /// Gets the runtime scene loader configured for packaged player scene assets.
        /// </summary>
        public RuntimeSceneLoadService SceneLoadService { get; private set; }

        /// <summary>
        /// Gets the runtime scene manager configured for built scene tracking and notifications.
        /// </summary>
        public SceneManager SceneManager { get; private set; }

        /// <summary>
        /// Gets the runtime component registry used to materialize packaged scene components.
        /// </summary>
        public RuntimeComponentRegistry SceneRuntimeComponentRegistry { get; private set; }

        /// <summary>
        /// Gets the clipboard service used by textbox shortcut commands.
        /// </summary>
        public ITextClipboardService TextClipboardService => TextClipboardServiceValue;

        /// <summary>
        /// Gets the registry that stores the active textbox shortcut bindings.
        /// </summary>
        public TextBoxShortcutRegistry TextBoxShortcutRegistry => TextBoxShortcutRegistryValue;

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

            ObjectManager = new ObjectManager(options);
            EntityFactory = CreateEntityFactory();
            if (EntityFactory == null) {
                throw new InvalidOperationException("Core entity factory creation must return one factory instance.");
            }

            ContentManager contentManager = GetContentManager();
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            SceneAssetReferenceResolver = new RuntimeSceneAssetReferenceResolver(
                contentManager,
                InitializationOptions.ContentRootPath,
                ShaderCompileTarget.DirectX11);
            SceneRuntimeComponentRegistry = RuntimeComponentRegistry.CreateDefault();
            SceneLoadService = new RuntimeSceneLoadService(SceneAssetReferenceResolver, SceneRuntimeComponentRegistry);
            SceneManager = CreateSceneManager(contentManager, InitializationOptions.SceneCatalog);
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
        /// <returns>Cached content manager rooted at the configured content root path.</returns>
        public ContentManager GetContentManager() {
            return GetContentManager(InitializationOptions.ContentRootPath);
        }

        /// <summary>
        /// Gets a cached content manager for a specific root directory, creating it the first time that root is requested.
        /// </summary>
        /// <param name="rootDirectory">Directory used to resolve relative content paths.</param>
        /// <returns>Cached content manager for the requested root.</returns>
        public ContentManager GetContentManager(string rootDirectory) {
            if (string.IsNullOrWhiteSpace(rootDirectory)) {
                throw new ArgumentException("Root directory must be provided.", nameof(rootDirectory));
            }

            string normalizedRootDirectory = Path.GetFullPath(rootDirectory);
            lock (ContentManagerLock) {
                if (ContentManagersByRootPath.TryGetValue(normalizedRootDirectory, out ContentManager contentManager)) {
                    return contentManager;
                }

                contentManager = new ContentManager(normalizedRootDirectory);
                ContentManagersByRootPath.Add(normalizedRootDirectory, contentManager);
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
        }

        /// <summary>
        /// Detaches the current physics runtime and clears any unconsumed fixed-step accumulator state.
        /// </summary>
        public void DetachPhysicsRuntime() {
            PhysicsRuntimeValue = null;
            PhysicsSchedulerValue.Reset();
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
            RenderManager3D.Draw();
            FPSComponent.RecordRenderFrame();
        }

        /// <summary>
        /// Releases managed resources for render managers.
        /// </summary>
        public void Dispose() {
            RenderManager3D.Dispose();
            RenderManager2D.Dispose();
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

            Input.EarlyUpdate();
            FPSComponent.RecordUpdateFrame();

            ObjectManager.Update();
            if (SceneManager != null) {
                SceneManager.FlushPendingOperations();
            }
            UpdatePhysics(elapsedSeconds);

            Input.Update();
            PointerInteractionSystem.Update();
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
        /// Creates one runtime scene manager when packaged scene metadata has been injected into the core initialization options.
        /// </summary>
        /// <param name="contentManager">Runtime content manager rooted at the active content path.</param>
        /// <param name="sceneCatalog">Injected runtime scene catalog used to resolve built scenes by stable identifier.</param>
        /// <returns>Configured runtime scene manager, or null when no runtime scene catalog has been supplied.</returns>
        SceneManager CreateSceneManager(ContentManager contentManager, RuntimeSceneCatalog sceneCatalog) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }
            if (sceneCatalog == null) {
                return null;
            }

            return new SceneManager(sceneCatalog, contentManager, SceneLoadService, ObjectManager);
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
            while (PhysicsSchedulerValue.TryConsumeStep()) {
                PhysicsRuntimeValue.Step(PhysicsSchedulerValue.StepSeconds);
            }
        }
    }
}
