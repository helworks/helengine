using helengine.directx11;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Triggers a one-frame picker render when the user clicks inside a scene viewport.
    /// </summary>
    public class EditorViewportPicker : UpdateComponent {
        /// <summary>
        /// Camera representing the active scene view.
        /// </summary>
        readonly CameraComponent SceneCamera;
        /// <summary>
        /// Entity that owns the picker camera.
        /// </summary>
        readonly EditorEntity PickerEntity;
        /// <summary>
        /// Camera used for picker rendering.
        /// </summary>
        readonly CameraComponent PickerCamera;
        /// <summary>
        /// Renderer used to execute picker passes.
        /// </summary>
        readonly helengine.directx11.DirectX11Renderer3D PickerRenderer;
        /// <summary>
        /// Cached pick colors for the current picker pass.
        /// </summary>
        readonly Dictionary<IDrawable3D, byte4> PickColors;
        /// <summary>
        /// Mapping of pick identifiers to entities for selection resolution.
        /// </summary>
        readonly Dictionary<int, Entity> PickEntitiesById;
        /// <summary>
        /// Pointer position captured at the time of the pick request.
        /// </summary>
        int2 PendingPointer;
        /// <summary>
        /// Viewport captured at the time of the pick request.
        /// </summary>
        float4 PendingViewport;
        /// <summary>
        /// Staging texture used for CPU readback of pick results.
        /// </summary>
        Texture2D ReadbackTexture;
        /// <summary>
        /// Cached readback texture width.
        /// </summary>
        int ReadbackWidth;
        /// <summary>
        /// Cached readback texture height.
        /// </summary>
        int ReadbackHeight;
        /// <summary>
        /// Cached readback texture format.
        /// </summary>
        Format ReadbackFormat;
        /// <summary>
        /// True when a pick render has completed and readback is pending.
        /// </summary>
        bool PickReadbackPending;
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
            helengine.directx11.DirectX11Renderer3D pickerRenderer) {
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

            SceneCamera = sceneCamera;
            PickerEntity = pickerEntity;
            PickerCamera = pickerCamera;
            PickerRenderer = pickerRenderer;
            PickColors = new Dictionary<IDrawable3D, byte4>();
            PickEntitiesById = new Dictionary<int, Entity>();
        }

        /// <summary>
        /// Checks for left-clicks inside the scene viewport and triggers a pick render.
        /// </summary>
        public override void Update() {
            InputManager input = Core.Instance.InputManager;
            if (PickReadbackPending) {
                ResolvePick();
            }

            if (EditorInputCaptureService.IsPointerBlocked(input.GetMousePosition())) {
                return;
            }

            if (!input.WasMouseLeftButtonPressed()) {
                return;
            }

            if (!IsPointerInsideViewport(input)) {
                return;
            }

            QueuePick(input);
        }

        /// <summary>
        /// Releases any GPU resources owned by the picker when removed.
        /// </summary>
        /// <param name="entity">Entity losing the component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DisposeReadbackTexture();
        }

        /// <summary>
        /// Queues a picker render for the current pointer state.
        /// </summary>
        /// <param name="input">Input manager providing pointer data.</param>
        void QueuePick(InputManager input) {
            if (input == null) {
                throw new ArgumentNullException(nameof(input));
            }

            Entity sceneEntity = SceneCamera.Parent;
            if (sceneEntity == null) {
                return;
            }

            PickerEntity.Position = sceneEntity.Position;
            PickerEntity.Orientation = sceneEntity.Orientation;
            PendingPointer = input.GetMousePosition();
            PendingViewport = SceneCamera.Viewport;

            BuildPickColors();
            PickerRenderer.RequestShaderPass(PickerCamera, SceneCamera.RenderQueue3D, "shaders\\PickerShader.fx", GetPickColor);
            PickReadbackPending = true;
        }

        /// <summary>
        /// Resolves the most recent picker render into an entity selection.
        /// </summary>
        void ResolvePick() {
            PickReadbackPending = false;

            RenderTarget renderTarget = PickerCamera.RenderTarget;
            if (renderTarget == null) {
                throw new InvalidOperationException("Picker camera must have a render target.");
            }

            if (renderTarget is not DirectX11RenderTargetResource directX11Target) {
                throw new InvalidOperationException("Picker render target must be a DirectX11 render target.");
            }

            int pickId = ReadPickId(directX11Target);
            if (pickId == 0) {
                EditorSelectionService.ClearSelection();
                return;
            }

            if (PickEntitiesById.TryGetValue(pickId, out Entity entity)) {
                string label = GetEntityLabel(entity);
                Console.WriteLine($"[Picker] Picked entity: {label}");
                Logger.WriteLine($"Picked entity: {label}");
                EditorSelectionService.SetSelectedEntity(entity);
            } else {
                EditorSelectionService.ClearSelection();
            }
        }

        /// <summary>
        /// Builds the pick color table for drawables visible to the picker camera.
        /// </summary>
        void BuildPickColors() {
            PickColors.Clear();
            PickEntitiesById.Clear();

            List<IDrawable3D> drawables = Core.Instance.ObjectManager.Drawables3D;
            int colorIndex = 1;
            for (int i = 0; i < drawables.Count; i++) {
                IDrawable3D drawable = drawables[i];
                if (drawable == null || drawable.Parent == null || !drawable.Parent.Enabled) {
                    continue;
                }
                if ((drawable.Parent.LayerMask & PickerCamera.LayerMask) == 0) {
                    continue;
                }

                int id = colorIndex;
                if (id > 0xFFFFFF) {
                    throw new InvalidOperationException("Pick id exceeded the maximum supported color range.");
                }

                byte r = (byte)(id & 0xFF);
                byte g = (byte)((id >> 8) & 0xFF);
                byte b = (byte)((id >> 16) & 0xFF);
                PickColors[drawable] = new byte4(r, g, b, 255);
                PickEntitiesById[id] = drawable.Parent;
                colorIndex++;
            }
        }

        /// <summary>
        /// Gets the pick color assigned to the specified drawable.
        /// </summary>
        /// <param name="drawable">Drawable to evaluate.</param>
        /// <returns>Assigned pick color, or transparent when missing.</returns>
        byte4 GetPickColor(IDrawable3D drawable) {
            if (drawable != null && PickColors.TryGetValue(drawable, out byte4 color)) {
                return color;
            }

            return new byte4(0, 0, 0, 0);
        }

        /// <summary>
        /// Reads the pick identifier from the picker render target.
        /// </summary>
        /// <param name="target">Render target containing pick colors.</param>
        /// <returns>Pick identifier derived from the target pixel.</returns>
        int ReadPickId(DirectX11RenderTargetResource target) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            byte4 color = ReadPickColor(target);
            return BuildPickId(color);
        }

        /// <summary>
        /// Reads the pick color from the picker render target for the pending pointer.
        /// </summary>
        /// <param name="target">Render target containing pick colors.</param>
        /// <returns>Pick color from the target.</returns>
        byte4 ReadPickColor(DirectX11RenderTargetResource target) {
            EnsureReadbackTexture(target);

            var context = PickerRenderer.Device.ImmediateContext;
            context.CopyResource(target.ColorTexture, ReadbackTexture);

            DataBox dataBox = context.MapSubresource(ReadbackTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
            try {
                int2 pixel = MapPointerToTarget(PendingPointer, PendingViewport, target.Width, target.Height);
                return ReadColorFromDataBox(dataBox, pixel, target.ColorFormat);
            } finally {
                context.UnmapSubresource(ReadbackTexture, 0);
            }
        }

        /// <summary>
        /// Maps a pointer location in the scene viewport to a pixel in the pick target.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="viewport">Scene viewport rect.</param>
        /// <param name="targetWidth">Pick target width.</param>
        /// <param name="targetHeight">Pick target height.</param>
        /// <returns>Mapped pixel coordinate in target space.</returns>
        int2 MapPointerToTarget(int2 pointer, float4 viewport, int targetWidth, int targetHeight) {
            if (targetWidth <= 0) {
                throw new ArgumentOutOfRangeException(nameof(targetWidth), "Pick target width must be positive.");
            }
            if (targetHeight <= 0) {
                throw new ArgumentOutOfRangeException(nameof(targetHeight), "Pick target height must be positive.");
            }

            double viewportWidth = Math.Max(1.0, viewport.Z);
            double viewportHeight = Math.Max(1.0, viewport.W);
            double localX = pointer.X - viewport.X;
            double localY = pointer.Y - viewport.Y;
            double normalizedX = localX / viewportWidth;
            double normalizedY = localY / viewportHeight;
            double scaledX = normalizedX * (targetWidth - 1);
            double scaledY = normalizedY * (targetHeight - 1);

            int mappedX = ClampToRange((int)Math.Round(scaledX), 0, targetWidth - 1);
            int mappedY = ClampToRange((int)Math.Round(scaledY), 0, targetHeight - 1);
            return new int2(mappedX, mappedY);
        }

        /// <summary>
        /// Reads a color from the mapped staging texture data.
        /// </summary>
        /// <param name="dataBox">Mapped data box from the staging texture.</param>
        /// <param name="pixel">Pixel coordinate to sample.</param>
        /// <param name="format">Texture format used for the pick target.</param>
        /// <returns>Decoded color at the requested pixel.</returns>
        byte4 ReadColorFromDataBox(DataBox dataBox, int2 pixel, Format format) {
            int offset = pixel.Y * dataBox.RowPitch + pixel.X * 4;
            byte c0 = Marshal.ReadByte(dataBox.DataPointer, offset);
            byte c1 = Marshal.ReadByte(dataBox.DataPointer, offset + 1);
            byte c2 = Marshal.ReadByte(dataBox.DataPointer, offset + 2);
            byte c3 = Marshal.ReadByte(dataBox.DataPointer, offset + 3);

            if (format == Format.R8G8B8A8_UNorm) {
                return new byte4(c0, c1, c2, c3);
            }

            if (format == Format.B8G8R8A8_UNorm) {
                return new byte4(c2, c1, c0, c3);
            }

            throw new InvalidOperationException("Pick target format is not supported for readback.");
        }

        /// <summary>
        /// Builds a pick identifier from a color.
        /// </summary>
        /// <param name="color">Color encoded in the pick buffer.</param>
        /// <returns>Integer pick identifier.</returns>
        int BuildPickId(byte4 color) {
            return color.X | (color.Y << 8) | (color.Z << 16);
        }

        /// <summary>
        /// Ensures the staging texture matches the picker render target.
        /// </summary>
        /// <param name="target">Render target used for picking.</param>
        void EnsureReadbackTexture(DirectX11RenderTargetResource target) {
            if (ReadbackTexture != null) {
                if (ReadbackWidth == target.Width && ReadbackHeight == target.Height && ReadbackFormat == target.ColorFormat) {
                    return;
                }

                DisposeReadbackTexture();
            }

            Texture2DDescription description = target.ColorTexture.Description;
            if (description.SampleDescription.Count != 1 || description.SampleDescription.Quality != 0) {
                throw new InvalidOperationException("Picker readback does not support multisampled render targets.");
            }

            description.Usage = ResourceUsage.Staging;
            description.BindFlags = BindFlags.None;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            description.OptionFlags = ResourceOptionFlags.None;

            ReadbackTexture = new Texture2D(PickerRenderer.Device, description);
            ReadbackWidth = target.Width;
            ReadbackHeight = target.Height;
            ReadbackFormat = target.ColorFormat;
        }

        /// <summary>
        /// Disposes the staging texture used for pick readback.
        /// </summary>
        void DisposeReadbackTexture() {
            if (ReadbackTexture == null) {
                return;
            }

            ReadbackTexture.Dispose();
            ReadbackTexture = null;
            ReadbackWidth = 0;
            ReadbackHeight = 0;
            ReadbackFormat = Format.Unknown;
        }

        /// <summary>
        /// Gets a display label for a picked entity.
        /// </summary>
        /// <param name="entity">Entity to label.</param>
        /// <returns>Entity label for logging.</returns>
        string GetEntityLabel(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity is EditorEntity editorEntity && !string.IsNullOrWhiteSpace(editorEntity.Name)) {
                return editorEntity.Name;
            }

            return entity.GetType().Name;
        }

        /// <summary>
        /// Clamps an integer value between inclusive bounds.
        /// </summary>
        /// <param name="value">Value to clamp.</param>
        /// <param name="min">Inclusive minimum.</param>
        /// <param name="max">Inclusive maximum.</param>
        /// <returns>Clamped value.</returns>
        int ClampToRange(int value, int min, int max) {
            if (value < min) {
                return min;
            }
            if (value > max) {
                return max;
            }

            return value;
        }

        /// <summary>
        /// Determines whether the mouse cursor is inside the scene camera viewport.
        /// </summary>
        /// <param name="input">Input manager providing cursor state.</param>
        /// <returns>True when the cursor is inside the viewport.</returns>
        bool IsPointerInsideViewport(InputManager input) {
            int2 pointer = input.GetMousePosition();
            float4 viewport = SceneCamera.Viewport;
            return pointer.X >= viewport.X &&
                   pointer.X < viewport.X + viewport.Z &&
                   pointer.Y >= viewport.Y &&
                   pointer.Y < viewport.Y + viewport.W;
        }
    }
}
