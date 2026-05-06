namespace helengine.editor {
    /// <summary>
    /// Resolves editor scene selection for pointer hits that land on the world-space 2D canvas preview plane.
    /// </summary>
    public static class EditorViewportCanvasPlaneSelectionService {
        /// <summary>
        /// Default camera forward axis before viewport-camera rotation is applied.
        /// </summary>
        static readonly float3 DefaultForward = new float3(0f, 0f, -1f);
        /// <summary>
        /// Default camera up axis before viewport-camera rotation is applied.
        /// </summary>
        static readonly float3 DefaultUp = new float3(0f, 1f, 0f);
        /// <summary>
        /// Perspective field of view used by the editor scene camera renderer.
        /// </summary>
        const double SceneViewportFieldOfViewRadians = Math.PI / 4.0;

        /// <summary>
        /// Resolves one selectable 2D scene entity for a pointer that hit the world-space canvas plane.
        /// </summary>
        /// <param name="previewComponent">Canvas preview component that owns the plane entity and offscreen preview camera.</param>
        /// <param name="viewportCameraEntity">Viewport camera entity captured at the time the plane was picked.</param>
        /// <param name="viewport">Viewport rectangle captured at the time the plane was picked.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <returns>Selectable 2D scene entity under the pointer, or null when the plane region was empty.</returns>
        public static Entity ResolveSelectableEntityAtPointer(
            EditorViewportCanvasPlanePreviewComponent previewComponent,
            Entity viewportCameraEntity,
            float4 viewport,
            int2 pointer) {
            if (previewComponent == null) {
                throw new ArgumentNullException(nameof(previewComponent));
            }
            if (viewportCameraEntity == null) {
                throw new ArgumentNullException(nameof(viewportCameraEntity));
            }

            EditorEntity planeEntity = previewComponent.PlaneEntity;
            CameraComponent previewCamera = previewComponent.PreviewCamera;
            if (planeEntity == null || previewCamera == null) {
                return null;
            }

            if (!TryMapPointerToCanvas(previewComponent, viewportCameraEntity, viewport, pointer, out int2 canvasPoint)) {
                return null;
            }

            IInteractable2D interactable = PointerInteractableHitResolver.ResolveTopInteractableAt(
                Core.Instance.ObjectManager.Interactables,
                Core.Instance.ObjectManager.Drawables2D,
                previewCamera,
                canvasPoint.X,
                canvasPoint.Y);
            if (interactable == null) {
                return null;
            }

            return EditorViewportSceneSelectionFilter.ResolveSelectableEntity(interactable.Parent);
        }

        /// <summary>
        /// Converts one viewport pointer position into simulated canvas pixel coordinates when the ray intersects the preview plane.
        /// </summary>
        /// <param name="previewComponent">Canvas preview component that owns the plane transform and preview camera.</param>
        /// <param name="viewportCameraEntity">Viewport camera entity captured at the time the plane was picked.</param>
        /// <param name="viewport">Viewport rectangle captured at the time the plane was picked.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="canvasPoint">Receives the mapped simulated canvas pixel coordinate.</param>
        /// <returns>True when the pointer ray intersects the plane bounds; otherwise false.</returns>
        public static bool TryMapPointerToCanvas(
            EditorViewportCanvasPlanePreviewComponent previewComponent,
            Entity viewportCameraEntity,
            float4 viewport,
            int2 pointer,
            out int2 canvasPoint) {
            if (previewComponent == null) {
                throw new ArgumentNullException(nameof(previewComponent));
            }
            if (viewportCameraEntity == null) {
                throw new ArgumentNullException(nameof(viewportCameraEntity));
            }

            EditorEntity planeEntity = previewComponent.PlaneEntity;
            CameraComponent previewCamera = previewComponent.PreviewCamera;
            if (planeEntity == null || previewCamera == null) {
                canvasPoint = default;
                return false;
            }

            if (!TryIntersectPointerWithPlane(viewportCameraEntity, viewport, pointer, planeEntity, out float3 hitPoint)) {
                canvasPoint = default;
                return false;
            }

            canvasPoint = MapPlaneHitToCanvas(hitPoint, planeEntity, previewCamera);
            return true;
        }

        /// <summary>
        /// Intersects one viewport pointer ray with the fixed XY canvas plane and rejects hits outside the plane bounds.
        /// </summary>
        /// <param name="viewportCameraEntity">Viewport camera entity captured at pick time.</param>
        /// <param name="viewport">Viewport rectangle captured at pick time.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="planeEntity">Canvas plane entity whose bounds should be intersected.</param>
        /// <param name="hitPoint">Receives the world-space hit point on the plane.</param>
        /// <returns>True when the ray hits the plane bounds; otherwise false.</returns>
        static bool TryIntersectPointerWithPlane(
            Entity viewportCameraEntity,
            float4 viewport,
            int2 pointer,
            EditorEntity planeEntity,
            out float3 hitPoint) {
            float3 rayOrigin = viewportCameraEntity.Position;
            float3 rayDirection = BuildPointerRayDirection(viewportCameraEntity, viewport, pointer);
            double directionZ = rayDirection.Z;
            if (Math.Abs(directionZ) <= double.Epsilon) {
                hitPoint = default;
                return false;
            }

            double planeZ = planeEntity.Position.Z;
            double distance = (planeZ - rayOrigin.Z) / directionZ;
            if (distance < 0.0) {
                hitPoint = default;
                return false;
            }

            hitPoint = rayOrigin + (rayDirection * (float)distance);
            return IsInsidePlaneBounds(hitPoint, planeEntity);
        }

        /// <summary>
        /// Builds one normalized world-space pointer ray direction for the captured viewport camera state.
        /// </summary>
        /// <param name="viewportCameraEntity">Viewport camera entity captured at pick time.</param>
        /// <param name="viewport">Viewport rectangle captured at pick time.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <returns>Normalized ray direction that leaves the viewport camera through the pointer location.</returns>
        static float3 BuildPointerRayDirection(Entity viewportCameraEntity, float4 viewport, int2 pointer) {
            if (viewport.Z <= 0f) {
                throw new InvalidOperationException("Viewport width must be positive.");
            }
            if (viewport.W <= 0f) {
                throw new InvalidOperationException("Viewport height must be positive.");
            }

            double viewportWidth = viewport.Z;
            double viewportHeight = viewport.W;
            double normalizedX = ((pointer.X - viewport.X) / viewportWidth) * 2.0 - 1.0;
            double normalizedY = 1.0 - (((pointer.Y - viewport.Y) / viewportHeight) * 2.0);
            double tangent = Math.Tan(SceneViewportFieldOfViewRadians * 0.5);
            double aspectRatio = viewportWidth / viewportHeight;

            float4 orientation = viewportCameraEntity.Orientation;
            float3 forward = float4.RotateVector(DefaultForward, orientation);
            float3 up = float4.RotateVector(DefaultUp, orientation);
            float3 right = float3.Normalize(float3.Cross(forward, up));
            double offsetX = normalizedX * tangent * aspectRatio;
            double offsetY = normalizedY * tangent;
            float3 direction = forward + (right * (float)offsetX) + (up * (float)offsetY);
            return float3.Normalize(direction);
        }

        /// <summary>
        /// Determines whether one world-space hit point falls inside the current canvas plane bounds.
        /// </summary>
        /// <param name="hitPoint">World-space hit point on the canvas plane.</param>
        /// <param name="planeEntity">Canvas plane entity whose transform defines the valid bounds.</param>
        /// <returns>True when the hit lies inside the plane rectangle; otherwise false.</returns>
        static bool IsInsidePlaneBounds(float3 hitPoint, EditorEntity planeEntity) {
            GetPlaneBounds(planeEntity, out float minX, out float maxX, out float minY, out float maxY);
            return hitPoint.X >= minX &&
                   hitPoint.X <= maxX &&
                   hitPoint.Y >= minY &&
                   hitPoint.Y <= maxY;
        }

        /// <summary>
        /// Maps one world-space plane hit into simulated canvas pixel coordinates using the preview camera target size.
        /// </summary>
        /// <param name="hitPoint">World-space hit point on the plane.</param>
        /// <param name="planeEntity">Canvas plane entity whose bounds define the local canvas area.</param>
        /// <param name="previewCamera">Offscreen preview camera whose viewport size defines the simulated canvas size.</param>
        /// <returns>Simulated canvas pixel coordinate clamped to the preview target bounds.</returns>
        static int2 MapPlaneHitToCanvas(float3 hitPoint, EditorEntity planeEntity, CameraComponent previewCamera) {
            GetPlaneBounds(planeEntity, out float minX, out _, out float minY, out _);

            double planeWidth = planeEntity.LocalScale.X;
            double planeHeight = planeEntity.LocalScale.Y;
            int canvasWidth = Math.Max(1, (int)Math.Round(previewCamera.Viewport.Z));
            int canvasHeight = Math.Max(1, (int)Math.Round(previewCamera.Viewport.W));
            double localX = (hitPoint.X - minX) / planeWidth;
            double localY = (hitPoint.Y - minY) / planeHeight;
            int canvasX = ClampToRange((int)Math.Round(localX * canvasWidth), 0, canvasWidth - 1);
            int canvasY = ClampToRange(canvasHeight - (int)Math.Round(localY * canvasHeight), 0, canvasHeight - 1);
            return new int2(canvasX, canvasY);
        }

        /// <summary>
        /// Computes the current world-space bounds of the centered preview plane.
        /// </summary>
        /// <param name="planeEntity">Canvas plane entity whose transform defines the bounds.</param>
        /// <param name="minX">Receives the minimum X bound.</param>
        /// <param name="maxX">Receives the maximum X bound.</param>
        /// <param name="minY">Receives the minimum Y bound.</param>
        /// <param name="maxY">Receives the maximum Y bound.</param>
        static void GetPlaneBounds(
            EditorEntity planeEntity,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY) {
            if (planeEntity == null) {
                throw new ArgumentNullException(nameof(planeEntity));
            }

            float halfWidth = planeEntity.LocalScale.X * 0.5f;
            float halfHeight = planeEntity.LocalScale.Y * 0.5f;
            minX = planeEntity.Position.X - halfWidth;
            maxX = planeEntity.Position.X + halfWidth;
            minY = planeEntity.Position.Y - halfHeight;
            maxY = planeEntity.Position.Y + halfHeight;
        }

        /// <summary>
        /// Clamps one integer value between inclusive minimum and maximum bounds.
        /// </summary>
        /// <param name="value">Value to clamp.</param>
        /// <param name="minimum">Inclusive minimum value.</param>
        /// <param name="maximum">Inclusive maximum value.</param>
        /// <returns>Clamped integer value.</returns>
        static int ClampToRange(int value, int minimum, int maximum) {
            if (value < minimum) {
                return minimum;
            }
            if (value > maximum) {
                return maximum;
            }

            return value;
        }
    }
}
