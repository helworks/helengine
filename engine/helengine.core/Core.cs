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
        /// Gets the input manager handling keyboard and mouse.
        /// </summary>
        public InputManager InputManager { get; private set; }

        /// <summary>
        /// Initializes core systems with rendering and input managers.
        /// </summary>
        /// <param name="render3D">3D renderer instance.</param>
        /// <param name="render2D">2D renderer instance.</param>
        /// <param name="input">Input manager instance.</param>
        public virtual void Initialize(RenderManager3D render3D, RenderManager2D render2D, InputManager input) {
            Initialize(render3D, render2D, input, InitializationOptions);
        }

        /// <summary>
        /// Initializes core systems with rendering, input, and initialization options.
        /// </summary>
        /// <param name="render3D">3D renderer instance.</param>
        /// <param name="render2D">2D renderer instance.</param>
        /// <param name="input">Input manager instance.</param>
        /// <param name="options">Initialization options that control ordering and list sizing.</param>
        public virtual void Initialize(
            RenderManager3D render3D,
            RenderManager2D render2D,
            InputManager input,
            CoreInitializationOptions options) {
            RenderManager3D = render3D;
            RenderManager2D = render2D;
            InputManager = input;

            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            options.Normalize();
            InitializationOptions = options;

            ObjectManager = new ObjectManager(options);
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
            InputManager.EarlyUpdate();

            ObjectManager.Update();

            InputManager.Update();
        }

        /// <summary>
        /// Executes the engine draw cycle.
        /// </summary>
        public virtual void Draw() {
            RenderManager3D.Draw();
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
