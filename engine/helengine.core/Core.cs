namespace helengine {
    public class Core : IDisposable {
        public static Core Instance { get; private set; }

        public ObjectManager ObjectManager { get; private set; }

        public RenderManager RenderManager { get; private set; }
        public InputManager InputManager { get; private set; }

        public Core() {
            Instance = this;
        }

        public virtual void Initialize(RenderManager render, InputManager input) {
            this.RenderManager = render;
            this.InputManager = input;
        
            ObjectManager = new ObjectManager();
        }

        public virtual void Update() {
            ObjectManager.Update();

            InputManager.Update();
        }

        public virtual void Draw() {
            RenderManager.Draw();
        }

        public void Dispose() {
            RenderManager.Dispose();
        }
    }
}
