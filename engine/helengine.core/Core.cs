namespace helengine {
    public class Core : IDisposable {
        public static Core Instance { get; private set; }

        public ObjectManager ObjectManager { get; private set; }

        public RenderManager3D RenderManager3D { get; private set; }
        public RenderManager2D RenderManager2D { get; private set; }

        public InputManager InputManager { get; private set; }

        public Core() {
            Instance = this;
        }

        public virtual void Initialize(RenderManager3D render3D, RenderManager2D render2D, InputManager input) {
            this.RenderManager3D = render3D;
            this.RenderManager2D = render2D;
            this.InputManager = input;

            ObjectManager = new ObjectManager();
        }

        public virtual void Update() {
            ObjectManager.Update();

            InputManager.Update();
        }

        public virtual void Draw() {
            RenderManager3D.Draw();
        }

        public void Dispose() {
            RenderManager3D.Dispose();
            RenderManager2D.Dispose();
        }
    }
}
