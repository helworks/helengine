namespace helengine.editor {
    /// <summary>
    /// Triggers a one-frame picker render when the user clicks inside a scene viewport.
    /// </summary>
    public class EditorViewportPicker : UpdateComponent {
        /// <summary>
        /// Camera representing the active scene view.
        /// </summary>
        readonly CameraComponent sceneCamera;
        /// <summary>
        /// Entity that owns the picker camera.
        /// </summary>
        readonly EditorEntity pickerEntity;
        /// <summary>
        /// Camera used for picker rendering.
        /// </summary>
        readonly CameraComponent pickerCamera;
        /// <summary>
        /// Renderer used to execute picker passes.
        /// </summary>
        readonly helengine.sharpdx.SharpDXRenderer3D pickerRenderer;
        /// <summary>
        /// Cached pick colors for the current picker pass.
        /// </summary>
        readonly Dictionary<IDrawable3D, byte4> pickColors;
        /// <summary>
        /// Initializes a new picker controller for the specified cameras.
        /// </summary>
        /// <param name="sceneCamera">Scene view camera that provides the viewport and transform.</param>
        /// <param name="pickerEntity">Entity owning the picker camera.</param>
        /// <param name="pickerCamera">Camera that renders the picker pass.</param>
        /// <param name="pickerRenderer">Renderer that executes the picker pass.</param>
        public EditorViewportPicker(
            CameraComponent sceneCamera,
            EditorEntity pickerEntity,
            CameraComponent pickerCamera,
            helengine.sharpdx.SharpDXRenderer3D pickerRenderer) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }
            if (pickerEntity == null) {
                throw new ArgumentNullException(nameof(pickerEntity));
            }
            if (pickerCamera == null) {
                throw new ArgumentNullException(nameof(pickerCamera));
            }
            if (pickerRenderer == null) {
                throw new ArgumentNullException(nameof(pickerRenderer));
            }

            this.sceneCamera = sceneCamera;
            this.pickerEntity = pickerEntity;
            this.pickerCamera = pickerCamera;
            this.pickerRenderer = pickerRenderer;
            pickColors = new Dictionary<IDrawable3D, byte4>();
        }

        /// <summary>
        /// Checks for left-clicks inside the scene viewport and triggers a pick render.
        /// </summary>
        public override void Update() {
            InputManager input = Core.Instance.InputManager;
            if (!input.WasMouseLeftButtonPressed()) {
                return;
            }

            if (!IsPointerInsideViewport(input)) {
                return;
            }

            Entity sceneEntity = sceneCamera.Parent;
            if (sceneEntity == null) {
                return;
            }

            pickerEntity.Position = sceneEntity.Position;
            pickerEntity.Orientation = sceneEntity.Orientation;
            BuildPickColors();
            pickerRenderer.RequestShaderPass(pickerCamera, sceneCamera.RenderQueue3D, "shaders\\PickerShader.fx", GetPickColor);
        }

        /// <summary>
        /// Builds the pick color table for drawables visible to the picker camera.
        /// </summary>
        void BuildPickColors() {
            pickColors.Clear();

            List<IDrawable3D> drawables = Core.Instance.ObjectManager.Drawables3D;
            int colorIndex = 1;
            for (int i = 0; i < drawables.Count; i++) {
                IDrawable3D drawable = drawables[i];
                if (drawable == null || drawable.Parent == null) {
                    continue;
                }
                if ((drawable.Parent.LayerMask & pickerCamera.LayerMask) == 0) {
                    continue;
                }

                int id = colorIndex;
                byte r = (byte)(id & 0xFF);
                byte g = (byte)((id >> 8) & 0xFF);
                byte b = (byte)((id >> 16) & 0xFF);
                pickColors[drawable] = new byte4(r, g, b, 255);
                colorIndex++;
            }
        }

        /// <summary>
        /// Gets the pick color assigned to the specified drawable.
        /// </summary>
        /// <param name="drawable">Drawable to evaluate.</param>
        /// <returns>Assigned pick color, or transparent when missing.</returns>
        byte4 GetPickColor(IDrawable3D drawable) {
            if (drawable != null && pickColors.TryGetValue(drawable, out byte4 color)) {
                return color;
            }

            return new byte4(0, 0, 0, 0);
        }

        /// <summary>
        /// Determines whether the mouse cursor is inside the scene camera viewport.
        /// </summary>
        /// <param name="input">Input manager providing cursor state.</param>
        /// <returns>True when the cursor is inside the viewport.</returns>
        bool IsPointerInsideViewport(InputManager input) {
            int2 pointer = input.GetMousePosition();
            float4 viewport = sceneCamera.Viewport;
            return pointer.X >= viewport.X &&
                   pointer.X < viewport.X + viewport.Z &&
                   pointer.Y >= viewport.Y &&
                   pointer.Y < viewport.Y + viewport.W;
        }
    }
}
