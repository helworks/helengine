namespace helengine {
    public abstract class RenderManager : IDisposable {
        public RenderManager() {
        }

        public virtual void AddWindow(IntPtr handle, int width, int height) {
        }

        public abstract RenderModelData BuildFromRaw(RawModelData data);

        public virtual void Update() {
        }

        public virtual void Draw() {
        }

        public virtual void Dispose() {
        }
    }
}
