namespace helengine.editor {
    /// <summary>
    /// Rotates the selected entity while the user drags a hovered rotation gizmo ring.
    /// </summary>
    public class TransformRotationGizmoDragComponent : UpdateComponent {
        /// <summary>
        /// Smallest squared vector magnitude treated as non-zero during normalization.
        /// </summary>
        const double MinimumVectorLengthSquared = 0.000000000001;
        /// <summary>
        /// Smallest denominator accepted by ray-plane intersection solving.
        /// </summary>
        const double MinimumPlaneIntersectionDenominator = 0.000000000001;
        /// <summary>
        /// Scene camera used to convert mouse pointer positions into world-space rays.
        /// </summary>
        readonly CameraComponent SceneCamera;

        /// <summary>
        /// True while a rotation drag is currently active.
        /// </summary>
        bool IsDragging;
        /// <summary>
        /// Selected entity being rotated by the active drag.
        /// </summary>
        Entity DraggedEntity;
        /// <summary>
        /// Handle entity driving the active drag.
        /// </summary>
        Entity DragHandleEntity;
        /// <summary>
        /// World-space axis around which the selected entity rotates.
        /// </summary>
        float3 DragRotationAxis;
        /// <summary>
        /// World-space center point of the active rotation.
        /// </summary>
        float3 DragRotationCenter;
        /// <summary>
        /// Orientation captured from the selected entity when dragging started.
        /// </summary>
        float4 DragStartEntityOrientation;
        /// <summary>
        /// Total unsnapped signed drag angle accumulated since the drag started.
        /// </summary>
        double DragAccumulatedAngle;
        /// <summary>
        /// Last solved world-space vector from the rotation center to the pointer plane hit.
        /// </summary>
        float3 DragPreviousVector;
        /// <summary>
        /// Tracks whether the active drag produced an actual rotation change.
        /// </summary>
        bool DragChanged;

        /// <summary>
        /// Initializes a new rotation gizmo drag controller.
        /// </summary>
        /// <param name="sceneCamera">Scene camera used for mouse ray construction.</param>
        public TransformRotationGizmoDragComponent(CameraComponent sceneCamera) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
        }

        /// <summary>
        /// Updates drag activation and applies rotation while dragging.
        /// </summary>
        public override void Update() {
            if (!IsRotateToolActive()) {
                if (IsDragging) {
                    EndDrag();
                }
                return;
            }

            InputManager input = Core.Instance.InputManager;
            if (IsDragging) {
                UpdateActiveDrag(input);
                return;
            }

            TryBeginDrag(input);
        }

        /// <summary>
        /// Ends any active drag when this component is removed.
        /// </summary>
        /// <param name="entity">Entity losing this component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            EndDrag();
        }

        /// <summary>
        /// Attempts to begin a drag from the current pointer and hover state.
        /// </summary>
        /// <param name="input">Input manager used to query pointer and button state.</param>
        void TryBeginDrag(InputManager input) {
            if (input == null) {
                throw new ArgumentNullException(nameof(input));
            }

            if (!input.WasMouseLeftButtonPressed()) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer)) {
                return;
            }

            if (!IsPointerInsideViewport(pointer)) {
                return;
            }

            Entity hoveredHandle = EditorGizmoHoverService.HoveredHandleEntity;
            if (hoveredHandle == null) {
                return;
            }

            Entity selectedEntity = EditorSelectionService.SelectedEntity;
            if (!CanRotateSelection(selectedEntity)) {
                return;
            }

            if (!TryResolveHandleAxis(hoveredHandle, out float3 rotationAxis)) {
                return;
            }

            float3 rotationCenter = selectedEntity.Position;
            if (!TryComputePlanePoint(pointer, rotationCenter, rotationAxis, out float3 planePoint)) {
                return;
            }

            float3 dragVector = NormalizeSafe(planePoint - rotationCenter, float3.Zero);
            if (dragVector == float3.Zero) {
                return;
            }

            IsDragging = true;
            DraggedEntity = selectedEntity;
            DragHandleEntity = hoveredHandle;
            DragRotationAxis = rotationAxis;
            DragRotationCenter = rotationCenter;
            DragStartEntityOrientation = selectedEntity.Orientation;
            DragAccumulatedAngle = 0.0;
            DragPreviousVector = dragVector;
            EditorGizmoDragService.BeginDrag(SceneCamera, selectedEntity);
            EditorGizmoHoverService.SetHoveredHandle(hoveredHandle);
        }

        /// <summary>
        /// Updates the active drag and applies snapped or unsnapped rotation to the selected entity.
        /// </summary>
        /// <param name="input">Input manager used to query pointer and button state.</param>
        void UpdateActiveDrag(InputManager input) {
            if (input == null) {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.WasMouseLeftButtonReleased() || input.GetMouseLeftButtonState() == ButtonState.Released) {
                EndDrag();
                return;
            }

            if (DraggedEntity == null || DragHandleEntity == null) {
                EndDrag();
                return;
            }

            if (!ReferenceEquals(EditorSelectionService.SelectedEntity, DraggedEntity)) {
                EndDrag();
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (!TryComputePlanePoint(pointer, DragRotationCenter, DragRotationAxis, out float3 planePoint)) {
                EditorGizmoHoverService.SetHoveredHandle(DragHandleEntity);
                return;
            }

            float3 currentVector = NormalizeSafe(planePoint - DragRotationCenter, float3.Zero);
            if (currentVector == float3.Zero) {
                EditorGizmoHoverService.SetHoveredHandle(DragHandleEntity);
                return;
            }

            double deltaAngle = ComputeSignedAngle(DragPreviousVector, currentVector, DragRotationAxis);
            if (Math.Abs(deltaAngle) > 0.0) {
                DragAccumulatedAngle += deltaAngle;
                DragPreviousVector = currentVector;
            }

            double resolvedAngle = ResolveActiveRotationAngle(input);
            float3 rotationAxis = DragRotationAxis;
            float4 deltaRotation;
            float4.CreateFromAxisAngle(ref rotationAxis, (float)resolvedAngle, out deltaRotation);
            float4 newOrientation = deltaRotation * DragStartEntityOrientation;
            if (!DraggedEntity.Orientation.Equals(newOrientation)) {
                DraggedEntity.Orientation = newOrientation;
            }
            DragChanged = DragChanged || !newOrientation.Equals(DragStartEntityOrientation);
            EditorGizmoHoverService.SetHoveredHandle(DragHandleEntity);
        }

        /// <summary>
        /// Ends the active drag and clears cached drag state.
        /// </summary>
        void EndDrag() {
            if (DragChanged) {
                EditorSceneMutationService.MarkSceneMutated();
            }

            EditorGizmoDragService.EndDrag(SceneCamera);
            IsDragging = false;
            DragChanged = false;
            DraggedEntity = null;
            DragHandleEntity = null;
            DragRotationAxis = float3.Zero;
            DragRotationCenter = float3.Zero;
            DragStartEntityOrientation = float4.Identity;
            DragAccumulatedAngle = 0.0;
            DragPreviousVector = float3.Zero;
        }

        /// <summary>
        /// Determines whether an entity can be rotated through gizmo interaction.
        /// </summary>
        /// <param name="selectedEntity">Entity currently selected in the editor.</param>
        /// <returns>True when the entity is valid for rotation.</returns>
        bool CanRotateSelection(Entity selectedEntity) {
            if (selectedEntity == null) {
                return false;
            }

            if (!selectedEntity.Enabled) {
                return false;
            }

            if (selectedEntity is EditorEntity editorEntity && editorEntity.InternalEntity) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the pointer is currently inside the scene camera viewport.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <returns>True when the pointer is inside the viewport rectangle.</returns>
        bool IsPointerInsideViewport(int2 pointer) {
            float4 viewport = SceneCamera.Viewport;
            return pointer.X >= viewport.X &&
                   pointer.X < viewport.X + viewport.Z &&
                   pointer.Y >= viewport.Y &&
                   pointer.Y < viewport.Y + viewport.W;
        }

        /// <summary>
        /// Resolves the world-space rotation axis from a hovered handle entity.
        /// </summary>
        /// <param name="handleEntity">Hovered handle entity to resolve.</param>
        /// <param name="rotationAxis">Resolved world-space rotation axis.</param>
        /// <returns>True when valid axis data is available.</returns>
        bool TryResolveHandleAxis(Entity handleEntity, out float3 rotationAxis) {
            if (handleEntity == null) {
                rotationAxis = float3.Zero;
                return false;
            }

            if (!TryFindTransformHandleComponent(handleEntity, out TransformGizmoHandleComponent handleComponent)) {
                rotationAxis = float3.Zero;
                return false;
            }

            if (handleComponent.ConstraintType != TransformGizmoHandleConstraintType.Axis) {
                rotationAxis = float3.Zero;
                return false;
            }

            float4 handleOrientation = handleEntity.Orientation;
            float3 worldAxis = NormalizeSafe(float4.RotateVector(handleComponent.LocalPrimaryDirection, handleOrientation), float3.Zero);
            if (worldAxis == float3.Zero) {
                rotationAxis = float3.Zero;
                return false;
            }

            rotationAxis = worldAxis;
            return true;
        }

        /// <summary>
        /// Finds the transform-gizmo handle component on an entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <param name="handleComponent">Resolved handle component.</param>
        /// <returns>True when the component was found.</returns>
        bool TryFindTransformHandleComponent(Entity entity, out TransformGizmoHandleComponent handleComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Components == null) {
                handleComponent = null;
                return false;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is TransformGizmoHandleComponent transformHandle) {
                    handleComponent = transformHandle;
                    return true;
                }
            }

            handleComponent = null;
            return false;
        }

        /// <summary>
        /// Computes the pointer ray intersection point on a world-space rotation plane.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="planeOrigin">Plane origin point in world space.</param>
        /// <param name="planeNormal">Normalized plane normal in world space.</param>
        /// <param name="planePoint">Solved world-space point on the plane.</param>
        /// <returns>True when the ray intersects the plane.</returns>
        bool TryComputePlanePoint(
            int2 pointer,
            float3 planeOrigin,
            float3 planeNormal,
            out float3 planePoint) {
            if (!EditorViewportPointerRayBuilder.TryBuildPerspectiveCameraRay(SceneCamera, pointer, out float3 rayOrigin, out float3 rayDirection)) {
                planePoint = float3.Zero;
                return false;
            }

            double denominator = float3.Dot(planeNormal, rayDirection);
            if (Math.Abs(denominator) <= MinimumPlaneIntersectionDenominator) {
                planePoint = float3.Zero;
                return false;
            }

            float3 planeDelta = planeOrigin - rayOrigin;
            double distanceAlongRay = float3.Dot(planeDelta, planeNormal) / denominator;
            planePoint = rayOrigin + (rayDirection * (float)distanceAlongRay);
            return true;
        }

        /// <summary>
        /// Computes the signed angle from one on-plane direction to another around the supplied axis.
        /// </summary>
        /// <param name="from">Starting normalized direction.</param>
        /// <param name="to">Ending normalized direction.</param>
        /// <param name="axis">Normalized rotation axis.</param>
        /// <returns>Signed angle in radians.</returns>
        double ComputeSignedAngle(float3 from, float3 to, float3 axis) {
            double dot = Math.Clamp(float3.Dot(from, to), -1.0, 1.0);
            float3 cross = float3.Cross(from, to);
            double signedMagnitude = float3.Dot(cross, axis);
            return Math.Atan2(signedMagnitude, dot);
        }

        /// <summary>
        /// Resolves the rotation angle that should be applied for the current drag update.
        /// </summary>
        /// <param name="input">Input manager used to read snap modifiers.</param>
        /// <returns>Signed rotation angle in radians after optional snapping.</returns>
        double ResolveActiveRotationAngle(InputManager input) {
            double activeSnapValue = TransformGizmoActiveSnapValueResolver.ResolveActiveSnapValue(input, EditorViewportToolMode.Rotate);
            if (activeSnapValue <= 0.0) {
                return DragAccumulatedAngle;
            }

            return TransformRotationGizmoSnapResolver.ResolveSnappedDeltaAngle(DragAccumulatedAngle, activeSnapValue);
        }

        /// <summary>
        /// Normalizes a vector or returns a fallback when the magnitude is too small.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <param name="fallback">Fallback direction returned for near-zero vectors.</param>
        /// <returns>Normalized vector when valid; otherwise the fallback value.</returns>
        float3 NormalizeSafe(float3 value, float3 fallback) {
            double lengthSquared =
                (value.X * value.X) +
                (value.Y * value.Y) +
                (value.Z * value.Z);
            if (lengthSquared <= MinimumVectorLengthSquared) {
                return fallback;
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(value.X * inverseLength),
                (float)(value.Y * inverseLength),
                (float)(value.Z * inverseLength));
        }

        /// <summary>
        /// Determines whether rotation drag interactions should be active for the scene camera viewport.
        /// </summary>
        /// <returns>True when the viewport tool mode is rotation.</returns>
        bool IsRotateToolActive() {
            return EditorViewportToolService.GetToolMode(SceneCamera) == EditorViewportToolMode.Rotate;
        }
    }
}
