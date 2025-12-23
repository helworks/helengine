namespace helengine {
    /// <summary>
    /// Central runtime coordinating managers, lifecycle, and shared services.
    /// </summary>
    public class Core : IDisposable {
        /// <summary>
        /// Initializes a new core instance and registers the static singleton reference.
        /// </summary>
        public Core() {
            Instance = this;
        }

        /// <summary>
        /// Gets the singleton core instance.
        /// </summary>
        public static Core Instance { get; private set; }

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
            this.RenderManager3D = render3D;
            this.RenderManager2D = render2D;
            this.InputManager = input;

            ObjectManager = new ObjectManager();
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
