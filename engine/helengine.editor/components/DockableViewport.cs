namespace helengine.editor {
    /// <summary>
    /// Dockable editor window that hosts a camera viewport and keeps its rectangle in sync with layout changes.
    /// </summary>
    public class DockableViewport : DockableEntity {
        /// <summary>
        /// Initializes a new dockable viewport and binds it to the provided camera.
        /// </summary>
        /// <param name="camera">Camera rendering into the viewport.</param>
        /// <param name="font">Font used by the base dockable entity title bar.</param>
        public DockableViewport(CameraComponent camera, FontAsset font)
            : base(font) {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            Title = "Viewport";
            SetContentBackgroundColor(new byte4(0, 0, 0, 0));
            updateViewport();
        }

        /// <summary>
        /// Gets the camera used to render into this viewport.
        /// </summary>
        public CameraComponent Camera { get; private set; }

        /// <summary>
        /// Gets or sets the viewport position, updating the underlying camera viewport rectangle.
        /// </summary>
        public override float3 Position {
            get => base.Position;
            set {
                base.Position = value;
                updateViewport();
            }
        }

        /// <summary>
        /// Re-applies viewport layout when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            updateViewport();
        }

        /// <summary>
        /// Updates the camera viewport using the dockable's position and size.
        /// </summary>
        void updateViewport() {
            if (Camera == null) {
                return;
            }

            Camera.Viewport = new float4(Position.X, Position.Y + TitleBarHeight, Size.X, Size.Y);
        }
    }
}
