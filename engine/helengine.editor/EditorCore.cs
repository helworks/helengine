using helengine.ui;

namespace helengine {
    public class EditorCore : Core {
        public ObjectManager EditorObjectManager { get; private set; }

        public Project Project { get; private set; }

        public EditorCore(Project project) {
            Project = project;
            EditorObjectManager = new ObjectManager();
        }

        public override void Update() {
            base.Update();

            EditorObjectManager.Update();
        }

        public override void Draw() {
            base.Draw();

            //EditorObjectManager.Draw();
        }
    }
}
