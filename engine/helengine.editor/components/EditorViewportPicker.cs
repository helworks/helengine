using helengine.directx11;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Triggers one-frame picker renders for scene selection and transform-axis hover detection.
    /// </summary>
    public class EditorViewportPicker : UpdateComponent {
        /// <summary>
        /// Picker mode used to resolve scene object selection from a click.
        /// </summary>
        const int PickModeSelection = 1;
        /// <summary>
        /// Picker mode used to resolve hovered transform gizmo axis from pointer position.
        /// </summary>
        const int PickModeHoverAxis = 2;
        /// <summary>
        /// Shader path used by picker passes.
        /// </summary>
        const string PickerShaderPath = "shaders\\PickerShader.fx";
        /// <summary>
        /// Layer mask used for transform gizmo handles.
        /// </summary>
        const ushort TransformGizmoLayerMask = EditorLayerMasks.SceneGizmo;
        /// <summary>
        /// Camera representing the active scene view.
        /// </summary>
        readonly CameraComponent SceneCamera;
        /// <summary>
        /// Camera used to render visible transform gizmos.
        /// </summary>
        readonly CameraComponent GizmoCamera;
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
        /// Pending pick mode for the readback currently queued.
        /// </summary>
        int PendingPickMode;

        /// <summary>
        /// Initializes a new picker controller for the specified cameras.
        /// </summary>
        /// <param name="sceneCamera">Scene view camera that provides scene-object viewport and transform.</param>
        /// <param name="gizmoCamera">Gizmo overlay camera that provides transform-axis viewport and transform.</param>
        /// <param name="pickerEntity">Entity owning the picker camera.</param>
        /// <param name="pickerCamera">Camera that renders the picker pass.</param>
        /// <param name="pickerRenderer">Renderer that executes the picker pass.</param>
        public EditorViewportPicker(
            CameraComponent sceneCamera,
            CameraComponent gizmoCamera,
            EditorEntity pickerEntity,
            CameraComponent pickerCamera,
            helengine.directx11.DirectX11Renderer3D pickerRenderer) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }
            if (gizmoCamera == null) {
                throw new ArgumentNullException(nameof(gizmoCamera));
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
            GizmoCamera = gizmoCamera;
            PickerEntity = pickerEntity;
            PickerCamera = pickerCamera;
            PickerRenderer = pickerRenderer;
            PickColors = new Dictionary<IDrawable3D, byte4>();
            PickEntitiesById = new Dictionary<int, Entity>();
        }

        /// <summary>
        /// Checks pointer state and queues picker renders for click selection and gizmo hover detection.
        /// </summary>
        public override void Update() {
            InputSystem input = Core.Instance.Input;
            if (PickReadbackPending) {
                ResolvePick();
            }

            bool isTransformGizmoToolActive = IsTransformGizmoToolActive();
            Entity hoveredAxis = isTransformGizmoToolActive ? EditorGizmoHoverService.HoveredAxisEntity : null;

            if (hoveredAxis != null && input.GetMouseLeftButtonState() == ButtonState.Pressed) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer)) {
                EditorGizmoHoverService.ClearHoveredHandle();
                return;
            }

            if (!IsPointerInsideViewport(input)) {
                EditorGizmoHoverService.ClearHoveredHandle();
                return;
            }

            if (input.WasMouseLeftButtonPressed()) {
                if (hoveredAxis != null) {
                    return;
                }

                EditorGizmoHoverService.ClearHoveredHandle();
                QueuePick(input, EditorLayerMasks.SceneObjects, PickModeSelection);
                return;
            }

            if (!isTransformGizmoToolActive) {
                return;
            }

            QueuePick(input, EditorLayerMasks.SceneGizmo, PickModeHoverAxis);
        }

        /// <summary>
        /// Releases any GPU resources owned by the picker when removed.
        /// </summary>
        /// <param name="entity">Entity losing the component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DisposeReadbackTexture();
            DisposePickerRenderTarget();
            EditorGizmoHoverService.ClearHoveredHandle();
        }

        /// <summary>
        /// Queues a picker render for the current pointer state.
        /// </summary>
        /// <param name="input">Input manager providing pointer data.</param>
        /// <param name="layerMask">Layer mask rendered by the picker camera for the request.</param>
        /// <param name="pickMode">Pick mode used to resolve readback results.</param>
        void QueuePick(InputSystem input, ushort layerMask, int pickMode) {
            if (input == null) {
                throw new ArgumentNullException(nameof(input));
            }

            ushort pickLayerMask = layerMask;
            if (pickMode == PickModeSelection) {
                pickLayerMask |= EditorLayerMasks.SceneCameraVisuals | EditorLayerMasks.SceneCanvasPlane;
            }

            CameraComponent sourceCamera = GetSourceCameraForMode(pickMode);
            Entity sourceCameraEntity = sourceCamera.Parent;
            if (sourceCameraEntity == null) {
                return;
            }

            PickerEntity.Position = sourceCameraEntity.Position;
            PickerEntity.Orientation = sourceCameraEntity.Orientation;
            PickerCamera.LayerMask = pickLayerMask;
            PendingPointer = input.GetMousePosition();
            PendingViewport = sourceCamera.Viewport;

            EnsurePickerRenderTargetSize(PendingViewport);
            RebuildPickerRenderQueue(pickLayerMask, pickMode);
            BuildPickColors(pickMode);
            PickerRenderer.RequestShaderPass(PickerCamera, PickerCamera.RenderQueue3D, PickerShaderPath, GetPickColor);
            PendingPickMode = pickMode;
            PickReadbackPending = true;
        }

        /// <summary>
        /// Resolves the most recent picker render into an entity selection or hovered gizmo axis.
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
            if (PendingPickMode == PickModeSelection) {
                ResolveSelectionPick(pickId);
                return;
            }

            if (PendingPickMode == PickModeHoverAxis) {
                ResolveHoverPick(pickId);
                return;
            }

            throw new InvalidOperationException("Picker mode is not supported.");
        }

        /// <summary>
        /// Resolves a selection pick identifier into editor selection state.
        /// </summary>
        /// <param name="pickId">Pick identifier read from the picker target.</param>
        void ResolveSelectionPick(int pickId) {
            if (pickId == 0) {
                ClearSelectionIfAllowed();
                return;
            }

            if (!PickEntitiesById.TryGetValue(pickId, out Entity entity)) {
                ClearSelectionIfAllowed();
                return;
            }

            if (IsCanvasPlaneEntity(entity)) {
                entity = ResolveCanvasPlaneSelection();
                if (entity == null) {
                    ClearSelectionIfAllowed();
                    return;
                }
            }

            string label = GetEntityLabel(entity);
            Console.WriteLine($"[Picker] Picked entity: {label}");
            Logger.WriteLine($"Picked entity: {label}");
            if (!EditorViewportSceneSelectionFilter.ShouldSelectEntity(entity)) {
                return;
            }

            EditorSelectionService.SetSelectedEntity(entity);
        }

        /// <summary>
        /// Resolves a hover pick identifier into transform gizmo hover state.
        /// </summary>
        /// <param name="pickId">Pick identifier read from the picker target.</param>
        void ResolveHoverPick(int pickId) {
            if (pickId == 0) {
                EditorGizmoHoverService.ClearHoveredHandle();
                return;
            }

            if (!PickEntitiesById.TryGetValue(pickId, out Entity entity)) {
                EditorGizmoHoverService.ClearHoveredHandle();
                return;
            }

            Entity hoveredAxis = ResolveTransformHandleEntity(entity);
            if (hoveredAxis == null) {
                EditorGizmoHoverService.ClearHoveredHandle();
                return;
            }

            EditorGizmoHoverService.SetHoveredHandle(hoveredAxis);
        }

        /// <summary>
        /// Ensures the picker camera render target and viewport match the active scene viewport size.
        /// </summary>
        /// <param name="viewport">Scene viewport used for picking.</param>
        void EnsurePickerRenderTargetSize(float4 viewport) {
            double viewportWidth = Math.Max(1.0, viewport.Z);
            double viewportHeight = Math.Max(1.0, viewport.W);
            int targetWidth = Math.Max(1, (int)Math.Ceiling(viewportWidth));
            int targetHeight = Math.Max(1, (int)Math.Ceiling(viewportHeight));

            bool requiresResize = true;
            if (PickerCamera.RenderTarget is DirectX11RenderTargetResource currentTarget) {
                requiresResize = currentTarget.Width != targetWidth || currentTarget.Height != targetHeight;
            }

            if (requiresResize) {
                DisposePickerRenderTarget();
                PickerCamera.RenderTarget = PickerRenderer.CreateRenderTarget(targetWidth, targetHeight);
            }

            PickerCamera.Viewport = new float4(0f, 0f, (float)viewportWidth, (float)viewportHeight);
        }

        /// <summary>
        /// Disposes the picker camera render target when it is a DirectX11 resource.
        /// </summary>
        void DisposePickerRenderTarget() {
            if (PickerCamera.RenderTarget is DirectX11RenderTargetResource target) {
                target.Dispose();
            }

            PickerCamera.RenderTarget = null;
        }

        /// <summary>
        /// Rebuilds the picker camera render queue for the requested layer mask.
        /// </summary>
        /// <param name="layerMask">Layer mask to include in the queue.</param>
        void RebuildPickerRenderQueue(ushort layerMask, int pickMode) {
            IRenderQueue3D queue = PickerCamera.RenderQueue3D;
            if (queue == null) {
                throw new InvalidOperationException("Picker camera must provide a render queue.");
            }

            queue.Clear();

            List<IDrawable3D> drawables = Core.Instance.ObjectManager.Drawables3D;
            for (int i = 0; i < drawables.Count; i++) {
                IDrawable3D drawable = drawables[i];
                if (drawable == null || drawable.Parent == null || !drawable.Parent.Enabled) {
                    continue;
                }

                if ((drawable.Parent.LayerMask & layerMask) == 0) {
                    continue;
                }
                if (pickMode == PickModeSelection && !ShouldIncludeDrawableForSelection(drawable)) {
                    continue;
                }

                queue.Add(drawable);
            }
        }

        /// <summary>
        /// Builds the pick color table for drawables visible to the picker camera.
        /// </summary>
        void BuildPickColors(int pickMode) {
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
                if (pickMode == PickModeSelection && !ShouldIncludeDrawableForSelection(drawable)) {
                    continue;
                }

                Entity selectedEntity;
                if (pickMode == PickModeHoverAxis) {
                    selectedEntity = ResolveTransformHandleEntity(drawable.Parent);
                } else if (IsCanvasPlaneEntity(drawable.Parent)) {
                    selectedEntity = drawable.Parent;
                } else {
                    selectedEntity = EditorViewportSceneSelectionFilter.ResolveSelectableEntity(drawable.Parent);
                }
                if (selectedEntity == null) {
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
                PickEntitiesById[id] = selectedEntity;
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
            double clampedNormalizedX = Math.Clamp(normalizedX, 0.0, 0.999999999);
            double clampedNormalizedY = Math.Clamp(normalizedY, 0.0, 0.999999999);

            int mappedX = ClampToRange((int)Math.Floor(clampedNormalizedX * targetWidth), 0, targetWidth - 1);
            int mappedY = ClampToRange((int)Math.Floor(clampedNormalizedY * targetHeight), 0, targetHeight - 1);
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
        bool IsPointerInsideViewport(InputSystem input) {
            int2 pointer = input.GetMousePosition();
            float4 viewport = SceneCamera.Viewport;
            return pointer.X >= viewport.X &&
                   pointer.X < viewport.X + viewport.Z &&
                   pointer.Y >= viewport.Y &&
                   pointer.Y < viewport.Y + viewport.W;
        }

        /// <summary>
        /// Resolves a picked gizmo sub-entity to its owning handle entity.
        /// </summary>
        /// <param name="pickedEntity">Picked entity from the picker map.</param>
        /// <returns>Handle entity when found; otherwise null.</returns>
        Entity ResolveTransformHandleEntity(Entity pickedEntity) {
            Entity current = pickedEntity;
            while (current != null) {
                if (IsTransformHandleEntity(current)) {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }

        /// <summary>
        /// Determines whether an entity is a transform gizmo handle root.
        /// </summary>
        /// <param name="entity">Entity to evaluate.</param>
        /// <returns>True when the entity represents a handle root.</returns>
        bool IsTransformHandleEntity(Entity entity) {
            if (entity is not EditorEntity) {
                return false;
            }

            if (entity.LayerMask != TransformGizmoLayerMask) {
                return false;
            }

            TransformGizmoHandleComponent handleComponent = FindTransformHandleComponent(entity);
            return handleComponent != null;
        }

        /// <summary>
        /// Finds the transform-gizmo handle component on an entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Handle component when found; otherwise null.</returns>
        TransformGizmoHandleComponent FindTransformHandleComponent(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Components == null) {
                return null;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is TransformGizmoHandleComponent handleComponent) {
                    return handleComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves which camera should drive picker alignment for the specified pick mode.
        /// </summary>
        /// <param name="pickMode">Pick mode being queued.</param>
        /// <returns>Camera used to align picker transform and viewport.</returns>
        CameraComponent GetSourceCameraForMode(int pickMode) {
            if (pickMode == PickModeHoverAxis) {
                return GizmoCamera;
            }

            if (pickMode == PickModeSelection) {
                return SceneCamera;
            }

            throw new InvalidOperationException("Picker mode is not supported.");
        }

        /// <summary>
        /// Determines whether transform-gizmo hover picking should be active.
        /// </summary>
        /// <returns>True when the scene viewport tool mode is currently backed by a live gizmo.</returns>
        bool IsTransformGizmoToolActive() {
            EditorViewportToolMode toolMode = EditorViewportToolService.GetToolMode(SceneCamera);
            return toolMode == EditorViewportToolMode.Translate ||
                   toolMode == EditorViewportToolMode.Rotate ||
                   toolMode == EditorViewportToolMode.Scale;
        }

        /// <summary>
        /// Determines whether a missed selection pick should clear the current selection.
        /// </summary>
        /// <returns>True when the original pick request came from an unblocked scene-viewport click.</returns>
        bool ShouldClearSelectionForMissedPick() {
            if (EditorInputCaptureService.IsPointerBlocked(PendingPointer)) {
                return false;
            }

            return IsPointerInsideViewport(PendingPointer, PendingViewport);
        }

        /// <summary>
        /// Clears the current scene selection when the originating pick request still represents a valid viewport click.
        /// </summary>
        void ClearSelectionIfAllowed() {
            if (!ShouldClearSelectionForMissedPick()) {
                return;
            }

            EditorSelectionService.ClearSelection();
        }

        /// <summary>
        /// Determines whether one drawable should participate in scene selection, including the editor canvas plane bridge.
        /// </summary>
        /// <param name="drawable">Drawable candidate to evaluate.</param>
        /// <returns>True when the drawable should be selectable through the picker.</returns>
        bool ShouldIncludeDrawableForSelection(IDrawable3D drawable) {
            if (drawable == null) {
                return false;
            }

            return EditorViewportSceneSelectionFilter.ShouldIncludeDrawableForSelection(drawable) ||
                   IsCanvasPlaneEntity(drawable.Parent);
        }

        /// <summary>
        /// Resolves the current plane hit into a selectable 2D scene entity using the simulated canvas hit-test path.
        /// </summary>
        /// <returns>Selectable 2D scene entity under the pointer, or null when the plane region is empty.</returns>
        Entity ResolveCanvasPlaneSelection() {
            EditorViewportCanvasPlanePreviewComponent previewComponent = FindCanvasPlanePreviewComponent();
            if (previewComponent == null) {
                return null;
            }

            return EditorViewportCanvasPlaneSelectionService.ResolveSelectableEntityAtPointer(
                previewComponent,
                PickerEntity,
                PendingViewport,
                PendingPointer);
        }

        /// <summary>
        /// Determines whether one entity is the internal world-space canvas preview plane.
        /// </summary>
        /// <param name="entity">Entity candidate to evaluate.</param>
        /// <returns>True when the entity matches the current canvas preview plane.</returns>
        bool IsCanvasPlaneEntity(Entity entity) {
            EditorViewportCanvasPlanePreviewComponent previewComponent = FindCanvasPlanePreviewComponent();
            if (previewComponent == null) {
                return false;
            }

            return ReferenceEquals(previewComponent.PlaneEntity, entity);
        }

        /// <summary>
        /// Finds the canvas preview component attached to the same scene-camera entity that owns the picker.
        /// </summary>
        /// <returns>Canvas preview component when available; otherwise null.</returns>
        EditorViewportCanvasPlanePreviewComponent FindCanvasPlanePreviewComponent() {
            if (Parent == null || Parent.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < Parent.Components.Count; componentIndex++) {
                if (Parent.Components[componentIndex] is EditorViewportCanvasPlanePreviewComponent previewComponent) {
                    return previewComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether a pointer is inside a viewport rectangle.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="viewport">Viewport rectangle in window coordinates.</param>
        /// <returns>True when the pointer lies inside the viewport bounds.</returns>
        bool IsPointerInsideViewport(int2 pointer, float4 viewport) {
            return pointer.X >= viewport.X &&
                   pointer.X < viewport.X + viewport.Z &&
                   pointer.Y >= viewport.Y &&
                   pointer.Y < viewport.Y + viewport.W;
        }
    }
}


