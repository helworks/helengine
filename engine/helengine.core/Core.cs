namespace helengine {
    public class Core : IDisposable {
        public static Core Instance { get; private set; }

        public ObjectManager ObjectManager { get; private set; }
        public RenderManager RenderManager { get; private set; }

        public Core() {
            Instance = this;

            ObjectManager = new ObjectManager();
        }

        public virtual void AssignRenderManager(RenderManager render) {
            this.RenderManager = render;
        }

        public virtual void Update() {
            ObjectManager.Update();
        }

        public virtual void Draw() {
            RenderManager.Draw();
        }

        public void Dispose() {
            RenderManager.Dispose();
        }
    }
}
