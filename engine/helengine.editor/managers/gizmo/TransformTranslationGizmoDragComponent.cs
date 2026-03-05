namespace helengine.editor {
    /// <summary>
    /// Translates the selected entity while the user drags a hovered translation gizmo handle.
    /// </summary>
    public class TransformTranslationGizmoDragComponent : UpdateComponent {
        /// <summary>
        /// Perspective vertical field of view used by scene camera rendering.
        /// </summary>
        const double PerspectiveVerticalFieldOfViewRadians = Math.PI / 4.0;
        /// <summary>
        /// Smallest squared vector magnitude treated as non-zero during normalization.
        /// </summary>
        const double MinimumVectorLengthSquared = 0.000000000001;
        /// <summary>
        /// Smallest denominator accepted by closest-point line solving.
        /// </summary>
        const double MinimumClosestPointDenominator = 0.000000000001;
        /// <summary>
        /// Smallest denominator accepted by ray-plane intersection solving.
        /// </summary>
        const double MinimumPlaneIntersectionDenominator = 0.000000000001;
        /// <summary>
        /// Forward axis used by cameras before orientation is applied.
        /// </summary>
        static readonly float3 CameraForwardAxis = new float3(0f, 0f, -1f);

        /// <summary>
        /// Scene camera used to convert mouse pointer positions into world-space rays.
        /// </summary>
        readonly CameraComponent SceneCamera;

        /// <summary>
        /// True while a gizmo drag is currently active.
        /// </summary>
        bool IsDragging;
        /// <summary>
        /// Selected entity being translated by the active drag.
        /// </summary>
        Entity DraggedEntity;
        /// <summary>
        /// Handle entity driving the active drag constraint.
        /// </summary>
        Entity DragHandleEntity;
        /// <summary>
        /// Active drag constraint type.
        /// </summary>
        TransformGizmoHandleConstraintType DragConstraintType;
        /// <summary>
        /// World-space primary drag direction used by axis constraints.
        /// </summary>
        float3 DragPrimaryDirection;
        /// <summary>
        /// World-space plane normal used by plane constraints.
        /// </summary>
        float3 DragPlaneNormal;
        /// <summary>
        /// Selected entity position captured when dragging started.
        /// </summary>
        float3 DragStartEntityPosition;
        /// <summary>
        /// Axis parameter value captured from pointer position when axis dragging started.
        /// </summary>
        double DragStartAxisParameter;
        /// <summary>
        /// World-space point captured on the drag plane when plane dragging started.
        /// </summary>
        float3 DragStartPlanePoint;

        /// <summary>
        /// Initializes a new gizmo drag controller.
        /// </summary>
        /// <param name="sceneCamera">Scene camera used for mouse ray construction.</param>
        public TransformTranslationGizmoDragComponent(CameraComponent sceneCamera) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
        }

        /// <summary>
        /// Updates drag activation and applies translation while dragging.
        /// </summary>
        public override void Update() {
            if (!IsTranslateToolActive()) {
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
            if (!CanTranslateSelection(selectedEntity)) {
                return;
            }

            if (!TryResolveHandleConstraint(
                hoveredHandle,
                out TransformGizmoHandleConstraintType constraintType,
                out float3 primaryDirection,
                out float3 planeNormal)) {
                return;
            }

            float3 selectionStartPosition = selectedEntity.Position;
            if (constraintType == TransformGizmoHandleConstraintType.Axis) {
                if (!TryComputeAxisParameter(pointer, selectionStartPosition, primaryDirection, out double axisParameter)) {
                    return;
                }

                DragStartAxisParameter = axisParameter;
                DragStartPlanePoint = float3.Zero;
            } else if (constraintType == TransformGizmoHandleConstraintType.Plane) {
                if (!TryComputePlanePoint(pointer, selectionStartPosition, planeNormal, out float3 planePoint)) {
                    return;
                }

                DragStartPlanePoint = planePoint;
                DragStartAxisParameter = 0.0;
            } else {
                throw new InvalidOperationException("Transform gizmo handle constraint type is not supported.");
            }

            IsDragging = true;
            DraggedEntity = selectedEntity;
            DragHandleEntity = hoveredHandle;
            DragConstraintType = constraintType;
            DragPrimaryDirection = primaryDirection;
            DragPlaneNormal = planeNormal;
            DragStartEntityPosition = selectionStartPosition;
            EditorGizmoDragService.BeginDrag(SceneCamera, selectedEntity);
            EditorGizmoHoverService.SetHoveredHandle(hoveredHandle);
        }

        /// <summary>
        /// Updates the active drag and applies the translated position to the selected entity.
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
            if (DragConstraintType == TransformGizmoHandleConstraintType.Axis) {
                if (!TryComputeAxisParameter(pointer, DragStartEntityPosition, DragPrimaryDirection, out double currentAxisParameter)) {
                    EditorGizmoHoverService.SetHoveredHandle(DragHandleEntity);
                    return;
                }

                double deltaParameter = currentAxisParameter - DragStartAxisParameter;
                float3 axisOffset = DragPrimaryDirection * (float)deltaParameter;
                DraggedEntity.Position = DragStartEntityPosition + axisOffset;
            } else if (DragConstraintType == TransformGizmoHandleConstraintType.Plane) {
                if (!TryComputePlanePoint(pointer, DragStartEntityPosition, DragPlaneNormal, out float3 currentPlanePoint)) {
                    EditorGizmoHoverService.SetHoveredHandle(DragHandleEntity);
                    return;
                }

                float3 delta = currentPlanePoint - DragStartPlanePoint;
                float3 planeOffset = ProjectVectorOntoPlane(delta, DragPlaneNormal);
                DraggedEntity.Position = DragStartEntityPosition + planeOffset;
            } else {
                throw new InvalidOperationException("Transform gizmo handle constraint type is not supported.");
            }

            EditorGizmoHoverService.SetHoveredHandle(DragHandleEntity);
        }

        /// <summary>
        /// Ends the active drag and clears cached drag state.
        /// </summary>
        void EndDrag() {
            EditorGizmoDragService.EndDrag(SceneCamera);
            IsDragging = false;
            DraggedEntity = null;
            DragHandleEntity = null;
            DragConstraintType = TransformGizmoHandleConstraintType.Axis;
            DragPrimaryDirection = float3.Zero;
            DragPlaneNormal = float3.Zero;
            DragStartEntityPosition = float3.Zero;
            DragStartAxisParameter = 0.0;
            DragStartPlanePoint = float3.Zero;
        }

        /// <summary>
        /// Determines whether an entity can be translated through gizmo interaction.
        /// </summary>
        /// <param name="selectedEntity">Entity currently selected in the editor.</param>
        /// <returns>True when the entity is valid for translation.</returns>
        bool CanTranslateSelection(Entity selectedEntity) {
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
        /// Resolves world-space drag constraint data from a hovered handle entity.
        /// </summary>
        /// <param name="handleEntity">Hovered handle entity to resolve.</param>
        /// <param name="constraintType">Resolved constraint type.</param>
        /// <param name="primaryDirection">Resolved primary world-space direction.</param>
        /// <param name="planeNormal">Resolved world-space plane normal for plane constraints.</param>
        /// <returns>True when valid constraint data is available.</returns>
        bool TryResolveHandleConstraint(
            Entity handleEntity,
            out TransformGizmoHandleConstraintType constraintType,
            out float3 primaryDirection,
            out float3 planeNormal) {
            if (handleEntity == null) {
                constraintType = TransformGizmoHandleConstraintType.Axis;
                primaryDirection = float3.Zero;
                planeNormal = float3.Zero;
                return false;
            }

            if (!TryFindTransformHandleComponent(handleEntity, out TransformGizmoHandleComponent handleComponent)) {
                constraintType = TransformGizmoHandleConstraintType.Axis;
                primaryDirection = float3.Zero;
                planeNormal = float3.Zero;
                return false;
            }

            float4 handleOrientation = handleEntity.Orientation;
            float3 worldPrimary = NormalizeSafe(float4.RotateVector(handleComponent.LocalPrimaryDirection, handleOrientation), float3.Zero);
            if (worldPrimary == float3.Zero) {
                constraintType = TransformGizmoHandleConstraintType.Axis;
                primaryDirection = float3.Zero;
                planeNormal = float3.Zero;
                return false;
            }

            if (handleComponent.ConstraintType == TransformGizmoHandleConstraintType.Axis) {
                constraintType = TransformGizmoHandleConstraintType.Axis;
                primaryDirection = worldPrimary;
                planeNormal = float3.Zero;
                return true;
            }

            if (handleComponent.ConstraintType == TransformGizmoHandleConstraintType.Plane) {
                float3 worldSecondary = NormalizeSafe(float4.RotateVector(handleComponent.LocalSecondaryDirection, handleOrientation), float3.Zero);
                if (worldSecondary == float3.Zero) {
                    constraintType = TransformGizmoHandleConstraintType.Axis;
                    primaryDirection = float3.Zero;
                    planeNormal = float3.Zero;
                    return false;
                }

                float3 normal = NormalizeSafe(float3.Cross(worldPrimary, worldSecondary), float3.Zero);
                if (normal == float3.Zero) {
                    constraintType = TransformGizmoHandleConstraintType.Axis;
                    primaryDirection = float3.Zero;
                    planeNormal = float3.Zero;
                    return false;
                }

                constraintType = TransformGizmoHandleConstraintType.Plane;
                primaryDirection = worldPrimary;
                planeNormal = normal;
                return true;
            }

            throw new InvalidOperationException("Transform gizmo handle constraint type is not supported.");
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

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is TransformGizmoHandleComponent transformHandle) {
                    handleComponent = transformHandle;
                    return true;
                }
            }

            handleComponent = null;
            return false;
        }

        /// <summary>
        /// Solves for the closest-point parameter on the drag axis for the current pointer ray.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="axisOrigin">Origin point of the drag axis.</param>
        /// <param name="axisDirection">Normalized drag axis direction.</param>
        /// <param name="axisParameter">Solved parameter along the drag axis.</param>
        /// <returns>True when the parameter is solvable from the current camera ray.</returns>
        bool TryComputeAxisParameter(
            int2 pointer,
            float3 axisOrigin,
            float3 axisDirection,
            out double axisParameter) {
            if (!TryBuildCameraRay(pointer, out float3 rayOrigin, out float3 rayDirection)) {
                axisParameter = 0.0;
                return false;
            }

            double rayAxisDot = float3.Dot(rayDirection, axisDirection);
            double denominator = 1.0 - (rayAxisDot * rayAxisDot);
            if (Math.Abs(denominator) <= MinimumClosestPointDenominator) {
                axisParameter = 0.0;
                return false;
            }

            float3 cameraToAxis = rayOrigin - axisOrigin;
            double rayCameraDot = float3.Dot(rayDirection, cameraToAxis);
            double axisCameraDot = float3.Dot(axisDirection, cameraToAxis);
            axisParameter = (axisCameraDot - (rayAxisDot * rayCameraDot)) / denominator;
            return true;
        }

        /// <summary>
        /// Computes the pointer ray intersection point on a world-space drag plane.
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
            if (!TryBuildCameraRay(pointer, out float3 rayOrigin, out float3 rayDirection)) {
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
        /// Builds a normalized world-space camera ray from the current pointer location.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="rayOrigin">Ray origin in world space.</param>
        /// <param name="rayDirection">Ray direction in world space.</param>
        /// <returns>True when the camera ray can be constructed.</returns>
        bool TryBuildCameraRay(int2 pointer, out float3 rayOrigin, out float3 rayDirection) {
            Entity cameraEntity = SceneCamera.Parent;
            if (cameraEntity == null) {
                throw new InvalidOperationException("Scene camera must belong to an entity.");
            }

            float4 viewport = SceneCamera.Viewport;
            if (viewport.Z <= 1f || viewport.W <= 1f) {
                rayOrigin = float3.Zero;
                rayDirection = float3.Zero;
                return false;
            }

            double normalizedX = (pointer.X - viewport.X) / viewport.Z;
            double normalizedY = (pointer.Y - viewport.Y) / viewport.W;
            double clampedX = Math.Clamp(normalizedX, 0.0, 1.0);
            double clampedY = Math.Clamp(normalizedY, 0.0, 1.0);
            double ndcX = (clampedX * 2.0) - 1.0;
            double ndcY = 1.0 - (clampedY * 2.0);
            double aspect = viewport.Z / viewport.W;
            if (aspect <= 0.0) {
                throw new InvalidOperationException("Scene camera viewport aspect ratio must be positive.");
            }

            double tanHalfFov = Math.Tan(PerspectiveVerticalFieldOfViewRadians * 0.5);
            float3 cameraSpaceDirection = new float3(
                (float)(ndcX * aspect * tanHalfFov),
                (float)(ndcY * tanHalfFov),
                -1f);
            cameraSpaceDirection = NormalizeSafe(cameraSpaceDirection, CameraForwardAxis);

            float4 cameraOrientation = cameraEntity.Orientation;
            float3 forwardFallback = float4.RotateVector(CameraForwardAxis, cameraOrientation);
            rayDirection = NormalizeSafe(float4.RotateVector(cameraSpaceDirection, cameraOrientation), forwardFallback);
            rayOrigin = cameraEntity.Position;
            return rayDirection != float3.Zero;
        }

        /// <summary>
        /// Projects a vector onto a plane by removing its normal component.
        /// </summary>
        /// <param name="value">Vector to project.</param>
        /// <param name="planeNormal">Normalized plane normal.</param>
        /// <returns>Projected vector that lies on the plane.</returns>
        float3 ProjectVectorOntoPlane(float3 value, float3 planeNormal) {
            double normalDot = float3.Dot(value, planeNormal);
            return value - (planeNormal * (float)normalDot);
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
        /// Determines whether translation drag interactions should be active for the scene camera viewport.
        /// </summary>
        /// <returns>True when the viewport tool mode is translation.</returns>
        bool IsTranslateToolActive() {
            return EditorViewportToolService.GetToolMode(SceneCamera) == EditorViewportToolMode.Translate;
        }
    }
}
