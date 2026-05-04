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
        /// Backing field for the default font asset used by text-heavy components.
        /// </summary>
        FontAsset DefaultFontAssetValue;

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
            Input = new InputSystem();
            PointerInteractionSystem = new PointerInteractionSystem(this, Input);
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
        /// Initializes core systems with rendering and input capture.
        /// </summary>
        /// <param name="render3D">3D renderer instance.</param>
        /// <param name="render2D">2D renderer instance.</param>
        /// <param name="input">Platform-specific input backend instance.</param>
        public virtual void Initialize(RenderManager3D render3D, RenderManager2D render2D, IInputBackend input) {
            Initialize(render3D, render2D, input, InitializationOptions);
        }

        /// <summary>
        /// Initializes core systems with rendering, input, and initialization options.
        /// </summary>
        /// <param name="render3D">3D renderer instance.</param>
        /// <param name="render2D">2D renderer instance.</param>
        /// <param name="input">Platform-specific input backend instance.</param>
        /// <param name="options">Initialization options that control ordering and list sizing.</param>
        public virtual void Initialize(
            RenderManager3D render3D,
            RenderManager2D render2D,
            IInputBackend input,
            CoreInitializationOptions options) {
            RenderManager3D = render3D;
            RenderManager2D = render2D;
            Input.SetBackend(input);

            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            options.Normalize();
            InitializationOptions = options;

            ObjectManager = new ObjectManager(options);
            ContentManager contentManager = GetContentManager();
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            SceneAssetReferenceResolver = new RuntimeSceneAssetReferenceResolver(
                contentManager,
                InitializationOptions.ContentRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeComponentRegistry runtimeComponentRegistry = RuntimeComponentRegistry.CreateDefault();
            SceneLoadService = new RuntimeSceneLoadService(SceneAssetReferenceResolver, runtimeComponentRegistry);
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
        /// Advances the engine update loop for objects and input.
        /// </summary>
        public virtual void Update() {
            Input.EarlyUpdate();
            FPSComponent.RecordUpdateFrame();

            ObjectManager.Update();

            Input.Update();
            PointerInteractionSystem.Update();
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
    }
}
