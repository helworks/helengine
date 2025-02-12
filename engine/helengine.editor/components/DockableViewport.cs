
namespace helengine.editor {
    public class DockableViewport : DockableEntity {

        public CameraComponent Camera { get; private set; }

        public DockableViewport(CameraComponent camera, FontAsset font)
            : base(font) {
            Camera = camera;
        }

        public override float3 Position {
            get => base.Position;
            set {
                base.Position = value;
                Camera.Viewport = new float4(Position.X, Position.Y + 20, Size.X, Size.Y);
            }
        }
    }
}
