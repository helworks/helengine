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
        /// Collector that resolves the gizmo drawables owned by this viewport only.
        /// </summary>
        readonly EditorViewportGizmoDrawableCollector GizmoDrawableCollector;
        /// <summary>
        /// Entity that owns the picker camera.
        /// </summary>
        readonly EditorEntity PickerEntity;
        /// <summary>
        /// Camera used for picker rendering.
        /// </summary>
        readonly CameraComponent PickerCamera;
        /// <summary>
        /// Host-owned capability that executes picker passes and reads target pixels.
        /// </summary>
        readonly IEditorPickingBackend PickingBackend;
        /// <summary>Session-owned object and preview graph used for selection resolution.</summary>
        readonly EditorSessionRendererResources RendererResources;
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
        /// <param name="gizmoDrawableCollector">Collector that resolves the viewport-owned gizmo drawables used for hover picking.</param>
        /// <param name="pickerEntity">Entity owning the picker camera.</param>
        /// <param name="pickerCamera">Camera that renders the picker pass.</param>
        /// <param name="pickingBackend">Backend that executes picker passes and reads results.</param>
        public EditorViewportPicker(
            CameraComponent sceneCamera,
            CameraComponent gizmoCamera,
            EditorViewportGizmoDrawableCollector gizmoDrawableCollector,
            EditorEntity pickerEntity,
            CameraComponent pickerCamera,
            IEditorPickingBackend pickingBackend,
            EditorSessionRendererResources rendererResources) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }
            if (gizmoCamera == null) {
                throw new ArgumentNullException(nameof(gizmoCamera));
            }
            if (gizmoDrawableCollector == null) {
                throw new ArgumentNullException(nameof(gizmoDrawableCollector));
            }
            if (pickerEntity == null) {
                throw new ArgumentNullException(nameof(pickerEntity));
            }
            if (pickerCamera == null) {
                throw new ArgumentNullException(nameof(pickerCamera));
            }
            if (pickingBackend == null) {
                throw new ArgumentNullException(nameof(pickingBackend));
            }
            if (rendererResources == null) {
                throw new ArgumentNullException(nameof(rendererResources));
            }

            SceneCamera = sceneCamera;
            GizmoCamera = gizmoCamera;
            GizmoDrawableCollector = gizmoDrawableCollector;
            PickerEntity = pickerEntity;
            PickerCamera = pickerCamera;
            PickingBackend = pickingBackend;
            RendererResources = rendererResources;
            PickColors = new Dictionary<IDrawable3D, byte4>();
            PickEntitiesById = new Dictionary<int, Entity>();
        }

        /// <summary>
        /// Checks pointer state and queues picker renders for click selection and gizmo hover detection.
        /// </summary>
        public override void Update() {
            InputSystem input = RendererResources.Input ?? throw new InvalidOperationException("The session renderer graph must provide input.");
            if (PickReadbackPending) {
                ResolvePick();
            }

            bool isTransformGizmoToolActive = IsTransformGizmoToolActive();
            Entity hoveredAxis = isTransformGizmoToolActive ? EditorSessionInteractionServices.From(Parent).GizmoHover.GetHoveredAxis(SceneCamera) : null;

            if (hoveredAxis != null && input.GetMouseLeftButtonState() == ButtonState.Pressed) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (EditorSessionInteractionServices.From(Parent).InputCapture.IsPointerBlocked(pointer)) {
                EditorSessionInteractionServices.From(Parent).GizmoHover.ClearHoveredHandle(SceneCamera);
                return;
            }

            if (!IsPointerInsideViewport(input)) {
                EditorSessionInteractionServices.From(Parent).GizmoHover.ClearHoveredHandle(SceneCamera);
                return;
            }

            if (input.WasMouseLeftButtonPressed()) {
                if (hoveredAxis != null) {
                    return;
                }

                EditorSessionInteractionServices.From(Parent).GizmoHover.ClearHoveredHandle(SceneCamera);
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
            PickingBackend.Dispose();
            EditorSessionInteractionServices.From(Parent).GizmoHover.ClearHoveredHandle(SceneCamera);
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
                pickLayerMask |= EditorLayerMasks.SceneCameraVisuals;
            }

            CameraComponent sourceCamera = GetSourceCameraForMode(pickMode);
            Entity sourceCameraEntity = sourceCamera.Parent;
            if (sourceCameraEntity == null) {
                return;
            }

            PickerEntity.Position = sourceCameraEntity.Position;
            PickerEntity.Orientation = sourceCameraEntity.Orientation;
            PickerCamera.LayerMask = pickLayerMask;
            SynchronizePickerCameraProjection(sourceCamera);
            PendingPointer = input.GetMousePosition();
            PendingViewport = sourceCamera.Viewport;
            RebuildPickerRenderQueue(pickLayerMask, pickMode);
            BuildPickColors(pickMode);
            double viewportWidth = Math.Max(1.0, PendingViewport.Z);
            double viewportHeight = Math.Max(1.0, PendingViewport.W);
            PickerCamera.Viewport = new float4(0f, 0f, (float)viewportWidth, (float)viewportHeight);
            PickingBackend.Render(PickerCamera, PickColors);
            PendingPickMode = pickMode;
            PickReadbackPending = true;
        }

        /// <summary>
        /// Mirrors the active source camera clip-plane settings onto the hidden picker camera so hover and selection picking operate over the same visible depth range as the rendered viewport.
        /// </summary>
        /// <param name="sourceCamera">Scene or gizmo camera that defines the visible projection range for the current pick request.</param>
        void SynchronizePickerCameraProjection(CameraComponent sourceCamera) {
            if (sourceCamera == null) {
                throw new ArgumentNullException(nameof(sourceCamera));
            }

            PickerCamera.NearPlaneDistance = sourceCamera.NearPlaneDistance;
            PickerCamera.FarPlaneDistance = sourceCamera.FarPlaneDistance;
        }

        /// <summary>
        /// Resolves the most recent picker render into an entity selection or hovered gizmo axis.
        /// </summary>
        void ResolvePick() {
            int targetWidth = Math.Max(1, (int)Math.Ceiling(Math.Max(1.0, PickerCamera.Viewport.Z)));
            int targetHeight = Math.Max(1, (int)Math.Ceiling(Math.Max(1.0, PickerCamera.Viewport.W)));
            int2 pixel = MapPointerToTarget(PendingPointer, PendingViewport, targetWidth, targetHeight);
            if (!PickingBackend.TryReadPixel(pixel, out byte4 color)) {
                return;
            }

            PickReadbackPending = false;
            int pickId = BuildPickId(color);
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
            Entity screenSpace2DEntity = ResolveScreenSpace2DSelection();
            if (screenSpace2DEntity != null) {
                SelectEntity(screenSpace2DEntity);
                return;
            }

            Entity worldPreviewEntity = ResolveWorldPreview2DSelection();
            if (worldPreviewEntity != null) {
                SelectEntity(worldPreviewEntity);
                return;
            }

            if (pickId == 0) {
                ClearSelectionIfAllowed();
                return;
            }

            if (!PickEntitiesById.TryGetValue(pickId, out Entity entity)) {
                ClearSelectionIfAllowed();
                return;
            }

            SelectEntity(entity);
        }

        /// <summary>
        /// Resolves a hover pick identifier into transform gizmo hover state.
        /// </summary>
        /// <param name="pickId">Pick identifier read from the picker target.</param>
        void ResolveHoverPick(int pickId) {
            if (pickId == 0) {
                EditorSessionInteractionServices.From(Parent).GizmoHover.ClearHoveredHandle(SceneCamera);
                return;
            }

            if (!PickEntitiesById.TryGetValue(pickId, out Entity entity)) {
                EditorSessionInteractionServices.From(Parent).GizmoHover.ClearHoveredHandle(SceneCamera);
                return;
            }

            Entity hoveredAxis = ResolveTransformHandleEntity(entity);
            if (hoveredAxis == null) {
                EditorSessionInteractionServices.From(Parent).GizmoHover.ClearHoveredHandle(SceneCamera);
                return;
            }

            EditorSessionInteractionServices.From(Parent).GizmoHover.SetHoveredHandle(SceneCamera, hoveredAxis);
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
            if (pickMode == PickModeHoverAxis) {
                GizmoDrawableCollector.PopulateRenderQueue(queue);
                return;
            }

            List<IDrawable3D> drawables = RendererResources.ObjectManager.Drawables3D;
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
            if (pickMode == PickModeHoverAxis) {
                BuildHoverPickColors();
                return;
            }

            List<IDrawable3D> drawables = RendererResources.ObjectManager.Drawables3D;
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
        /// Builds the pick-color table for only the gizmo drawables owned by this viewport.
        /// </summary>
        void BuildHoverPickColors() {
            IReadOnlyList<IDrawable3D> drawables = GizmoDrawableCollector.CaptureOwnedDrawables();
            int colorIndex = 1;
            for (int index = 0; index < drawables.Count; index++) {
                IDrawable3D drawable = drawables[index];
                if (drawable == null || drawable.Parent == null || !drawable.Parent.Enabled) {
                    continue;
                }

                Entity selectedEntity = ResolveTransformHandleEntity(drawable.Parent);
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
        /// Builds a pick identifier from a color.
        /// </summary>
        /// <param name="color">Color encoded in the pick buffer.</param>
        /// <returns>Integer pick identifier.</returns>
        int BuildPickId(byte4 color) {
            return color.X | (color.Y << 8) | (color.Z << 16);
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
            EditorViewportToolMode toolMode = EditorSessionInteractionServices.From(Parent).ViewportTool.GetToolMode(SceneCamera);
            return toolMode == EditorViewportToolMode.Translate ||
                   toolMode == EditorViewportToolMode.Rotate ||
                   toolMode == EditorViewportToolMode.Scale;
        }

        /// <summary>
        /// Determines whether a missed selection pick should clear the current selection.
        /// </summary>
        /// <returns>True when the original pick request came from an unblocked scene-viewport click.</returns>
        bool ShouldClearSelectionForMissedPick() {
            if (EditorSessionInteractionServices.From(Parent).InputCapture.IsPointerBlocked(PendingPointer)) {
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

            EditorSessionInteractionServices.From(Parent).Selection.ClearSelection();
        }

        /// <summary>
        /// Determines whether one drawable should participate in scene selection.
        /// </summary>
        /// <param name="drawable">Drawable candidate to evaluate.</param>
        /// <returns>True when the drawable should be selectable through the picker.</returns>
        bool ShouldIncludeDrawableForSelection(IDrawable3D drawable) {
            if (drawable == null) {
                return false;
            }

            return EditorViewportSceneSelectionFilter.ShouldIncludeDrawableForSelection(drawable);
        }

        /// <summary>
        /// Resolves one screen-space 2D scene selection from the current viewport pointer before world-preview and generic 3D fallback are considered.
        /// </summary>
        /// <returns>Selectable screen-space 2D scene entity under the pointer, or null when no screen-space 2D scene entity is hit.</returns>
        Entity ResolveScreenSpace2DSelection() {
            return EditorViewportDirect2DPresentationService.ResolveSelectableEntityAtPointer(
                SceneCamera,
                PendingViewport,
                PendingPointer,
                RendererResources.ObjectManager);
        }

        /// <summary>
        /// Resolves one authored world-preview 2D scene selection from the current viewport pointer before generic 3D mesh selection is considered.
        /// </summary>
        /// <returns>Selectable world-preview 2D scene entity under the pointer, or null when no world-preview entity is hit.</returns>
        Entity ResolveWorldPreview2DSelection() {
            return EditorViewportDirect2DPresentationService.ResolveSelectableWorldPreviewEntityAtPointer(
                SceneCamera,
                PendingViewport,
                PendingPointer,
                RendererResources.ObjectManager);
        }

        /// <summary>
        /// Applies one selected entity to the editor selection service after standard selectability checks and logging.
        /// </summary>
        /// <param name="entity">Entity selected by the picker flow.</param>
        void SelectEntity(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            string label = GetEntityLabel(entity);
            Console.WriteLine($"[Picker] Picked entity: {label}");
            Logger.WriteLine($"Picked entity: {label}");
            if (!EditorViewportSceneSelectionFilter.ShouldSelectEntity(entity)) {
                return;
            }

            EditorSessionInteractionServices.From(Parent).Selection.SetSelectedEntity(entity);
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


