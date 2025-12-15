
namespace helengine.editor {
    public class DockableViewport : DockableEntity {

        public CameraComponent Camera { get; private set; }

        public DockableViewport(CameraComponent camera, FontAsset font)
            : base(font) {
            Camera = camera;
            updateViewport();
        }

        public override float3 Position {
            get => base.Position;
            set {
                base.Position = value;
                updateViewport();
            }
        }

        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            updateViewport();
        }

        void updateViewport() {
            if (Camera == null) {
                return;
            }

            Camera.Viewport = new float4(Position.X, Position.Y + TitleBarHeight, Size.X, Size.Y);
        }
    }
}
