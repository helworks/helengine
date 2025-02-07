namespace helengine {
    public abstract class RenderManager : IDisposable {
        public RenderManager() {
        }

        public virtual void AddWindow(IntPtr handle, int width, int height) {
        }

        public virtual void Update() {
        }

        public virtual void Draw() {
        }

        public virtual void Dispose() {
        }
    }
}
