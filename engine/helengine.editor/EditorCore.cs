using helengine.ui;

namespace helengine {
    /// <summary>
    /// Provides the core editor loop responsible for updating and drawing editor-owned systems.
    /// </summary>
    public class EditorCore : Core {
        /// <summary>
        /// Stores the editor UI font used by editor-only systems and generated asset references.
        /// </summary>
        FontAsset DefaultFontAssetForEditorValue;

        /// <summary>
        /// Creates a new editor core instance for the specified project.
        /// </summary>
        /// <param name="project">The project to open in the editor.</param>
        public EditorCore(Project project)
            : base(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            }) {
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

        /// <summary>
        /// Gets the allocator that owns numeric scene entity ids for the active editor host.
        /// </summary>
        public global::helengine.editor.EditorSceneEntityIdAllocator SceneEntityIdAllocator { get; private set; }

        /// <summary>
        /// Gets the editor-owned font asset used by editor UI and generated editor font references.
        /// </summary>
        public FontAsset DefaultFontAssetForEditor {
            get { return DefaultFontAssetForEditorValue; }
        }

        /// <summary>
        /// Stores the editor-owned font asset used by editor UI and generated editor font references.
        /// </summary>
        /// <param name="font">Editor-owned font asset.</param>
        public void SetDefaultFontAssetForEditor(FontAsset font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            DefaultFontAssetForEditorValue = font;
        }

        /// <inheritdoc />
        public override void Initialize(
            RenderManager3D render3D,
            RenderManager2D render2D,
            IInputBackend input,
            PlatformInfo platformInfo,
            CoreInitializationOptions options) {
            SceneEntityIdAllocator = new global::helengine.editor.EditorSceneEntityIdAllocator();
            base.Initialize(render3D, render2D, input, platformInfo, options);

            EditorObjectManager = new ObjectManager(InitializationOptions);
        }

        /// <summary>
        /// Creates the authored entity factory exposed by editor hosts.
        /// </summary>
        /// <returns>Editor-authored entity factory.</returns>
        protected override IEntityFactory CreateEntityFactory() {
            if (SceneEntityIdAllocator == null) {
                throw new InvalidOperationException("EditorCore must initialize SceneEntityIdAllocator before creating the entity factory.");
            }

            return new global::helengine.editor.EditorEntityFactory(SceneEntityIdAllocator);
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
