namespace helengine {
    /// <summary>
    /// Stores authored scene-id remapping entries and can optionally redirect startup through one logical initial scene id.
    /// </summary>
    public sealed class SceneMapComponent : UpdateComponent {
        /// <summary>
        /// Tracks the active scene-map singleton when one is loaded.
        /// </summary>
        public static SceneMapComponent Instance { get; private set; }

        /// <summary>
        /// Tracks whether startup redirection already ran for the current process lifetime.
        /// </summary>
        static bool StartupSceneWasRequested;

        /// <summary>
        /// Authored mapping entries keyed by logical source scene id.
        /// </summary>
        Dictionary<string, string> MappingsValue;

        /// <summary>
        /// Initializes one empty scene-map component.
        /// </summary>
        public SceneMapComponent() {
            MappingsValue = new Dictionary<string, string>(StringComparer.Ordinal);
            InitialSceneId = string.Empty;
        }

        /// <summary>
        /// Gets or sets the logical scene id that should be loaded once after the singleton becomes active.
        /// </summary>
        [EditorPropertyDisplayName("Initial Scene Id")]
        [EditorPropertyOrder(1)]
        public string InitialSceneId { get; set; }

        /// <summary>
        /// Gets or sets the authored mapping entries keyed by logical source scene id.
        /// </summary>
        [EditorPropertyDisplayName("Scene Mappings")]
        [EditorPropertyOrder(0)]
        public Dictionary<string, string> Mappings {
            get => MappingsValue;
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                MappingsValue = value;
            }
        }

        /// <summary>
        /// Resolves one logical scene id through the active singleton mapping table when present.
        /// </summary>
        /// <param name="sceneId">Logical scene id requested by gameplay or menu code.</param>
        /// <returns>Mapped scene id when a mapping exists; otherwise the original scene id.</returns>
        public static string ResolveSceneId(string sceneId) {
            string mappedSceneId;
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            } else if (Instance == null) {
                return sceneId;
            } else if (Instance.Mappings.TryGetValue(sceneId, out mappedSceneId) && !string.IsNullOrWhiteSpace(mappedSceneId)) {
                return mappedSceneId;
            }

            return sceneId;
        }

        /// <summary>
        /// Registers the singleton as soon as the component attach lifecycle becomes active.
        /// </summary>
        /// <param name="entity">Entity receiving the scene-map component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            RegisterSingleton();
        }

        /// <summary>
        /// Clears the singleton when the component is removed from its entity.
        /// </summary>
        /// <param name="entity">Entity losing the scene-map component.</param>
        public override void ComponentRemoved(Entity entity) {
            ClearSingletonIfOwned();
            base.ComponentRemoved(entity);
        }

        /// <summary>
        /// Requests the authored startup scene once the runtime update loop begins.
        /// </summary>
        public override void Update() {
            base.Update();
            RequestInitialSceneLoad();
        }

        /// <summary>
        /// Releases singleton ownership when the component is disposed.
        /// </summary>
        public override void Dispose() {
            ClearSingletonIfOwned();
            base.Dispose();
        }

        /// <summary>
        /// Registers the singleton instance or fails immediately when a second active scene-map component appears.
        /// </summary>
        void RegisterSingleton() {
            if (Instance == null) {
                Instance = this;
            } else if (!ReferenceEquals(Instance, this)) {
                throw new InvalidOperationException("Only one active SceneMapComponent may exist at a time.");
            }
        }

        /// <summary>
        /// Loads the authored initial scene exactly once through the current scene map.
        /// </summary>
        void RequestInitialSceneLoad() {
            if (StartupSceneWasRequested) {
                return;
            } else if (string.IsNullOrWhiteSpace(InitialSceneId)) {
                return;
            } else if (Core.Instance == null || Core.Instance.SceneManager == null) {
                throw new InvalidOperationException("SceneMapComponent startup redirection requires an initialized SceneManager.");
            }

            string resolvedSceneId = ResolveSceneId(InitialSceneId);
            StartupSceneWasRequested = true;
            Core.Instance.SceneManager.LoadScene(resolvedSceneId, SceneLoadMode.Single);
        }

        /// <summary>
        /// Clears the singleton when this component currently owns it.
        /// </summary>
        void ClearSingletonIfOwned() {
            if (ReferenceEquals(Instance, this)) {
                Instance = null;
            }
        }
    }
}
