using helengine.ui;

namespace helengine {
    /// <summary>
    /// Provides the core editor loop responsible for updating and drawing editor-owned systems.
    /// </summary>
    public class EditorCore : Core {
        /// <summary>
        /// Creates a new editor core instance for the specified project.
        /// </summary>
        /// <param name="project">The project to open in the editor.</param>
        public EditorCore(Project project) {
            Project = project;
        }

        /// <summary>
        /// Gets the object manager that tracks editor objects.
        /// </summary>
        public ObjectManager EditorObjectManager { get; private set; }

        /// <summary>
        /// Gets the project currently loaded into the editor.
        /// </summary>
        public Project Project { get; private set; }

        /// <inheritdoc />
        public override void Initialize(
            RenderManager3D render3D,
            RenderManager2D render2D,
            IInputBackend input,
            PlatformInfo platformInfo,
            CoreInitializationOptions options) {
            base.Initialize(render3D, render2D, input, platformInfo, options);

            EditorObjectManager = new ObjectManager(InitializationOptions);
        }

        /// <summary>
        /// Creates the authored entity factory exposed by editor hosts.
        /// </summary>
        /// <returns>Editor-authored entity factory.</returns>
        protected override IEntityFactory CreateEntityFactory() {
            return new global::helengine.editor.EditorEntityFactory();
        }

        /// <inheritdoc />
        public override void Update() {
            ComponentExecutionContext.EnterEditor();
            try {
                base.Update();
                EditorObjectManager.Update();
            } finally {
                ComponentExecutionContext.ExitEditor();
            }
        }

        /// <inheritdoc />
        public override void Update(double elapsedSeconds) {
            ComponentExecutionContext.EnterEditor();
            try {
                base.Update(elapsedSeconds);
                EditorObjectManager.Update();
            } finally {
                ComponentExecutionContext.ExitEditor();
            }
        }

        /// <inheritdoc />
        public override void Draw() {
            base.Draw();

            //EditorObjectManager.Draw();
        }
    }
}
