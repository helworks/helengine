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
            EditorObjectManager = new ObjectManager();
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
        public override void Update() {
            base.Update();

            EditorObjectManager.Update();
        }

        /// <inheritdoc />
        public override void Draw() {
            base.Draw();

            //EditorObjectManager.Draw();
        }
    }
}
