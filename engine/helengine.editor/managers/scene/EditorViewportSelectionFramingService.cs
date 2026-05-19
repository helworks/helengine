namespace helengine.editor {
    /// <summary>
    /// Frames the current editor selection inside one scene viewport camera without changing runtime engine behavior.
    /// </summary>
    public sealed class EditorViewportSelectionFramingService {
        /// <summary>
        /// Perspective field of view currently used by the editor scene viewport renderer.
        /// </summary>
        const double PerspectiveFieldOfViewRadians = Math.PI / 4.0;

        /// <summary>
        /// Extra distance added beyond the framed selection radius so the target does not touch the clip plane.
        /// </summary>
        const double FarPlaneMargin = 8.0;

        /// <summary>
        /// Minimum radius used when framing point-like selections with no meaningful spatial extent.
        /// </summary>
        const double MinimumFocusRadius = 1.0;

        /// <summary>
        /// Repositions one viewport camera so the supplied selection fits inside the current scene view.
        /// </summary>
        /// <param name="sceneCamera">Scene camera that should frame the selection.</param>
        /// <param name="cameraController">Viewport-local camera controller that owns orbit state.</param>
        /// <param name="selectedEntity">Currently selected entity to frame.</param>
        public void FocusSelection(CameraComponent sceneCamera, EditorViewportCameraController cameraController, Entity selectedEntity) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }
            if (cameraController == null) {
                throw new ArgumentNullException(nameof(cameraController));
            }
            if (selectedEntity == null) {
                return;
            }
            if (cameraController.Parent == null) {
                throw new InvalidOperationException("Viewport camera controller must be attached to a camera entity before focus operations can run.");
            }

            float3 focusCenter;
            double focusRadius;
            ResolveFocusBounds(selectedEntity, out focusCenter, out focusRadius);

            float4 viewport = sceneCamera.Viewport;
            double viewportWidth = Math.Max(1.0, viewport.Z);
            double viewportHeight = Math.Max(1.0, viewport.W);
            double aspectRatio = viewportWidth / viewportHeight;
            double halfVerticalFieldOfView = PerspectiveFieldOfViewRadians * 0.5;
            double halfHorizontalFieldOfView = Math.Atan(Math.Tan(halfVerticalFieldOfView) * aspectRatio);
            double limitingHalfFieldOfView = Math.Min(halfVerticalFieldOfView, halfHorizontalFieldOfView);
            double requiredDistance = focusRadius / Math.Tan(limitingHalfFieldOfView);
            if (requiredDistance < MinimumFocusRadius) {
                requiredDistance = MinimumFocusRadius;
            }

            ApplyFocusedCameraTransform(cameraController, selectedEntity, focusCenter, requiredDistance);

            double requiredFarPlane = requiredDistance + focusRadius + FarPlaneMargin;
            if (requiredFarPlane > sceneCamera.FarPlaneDistance) {
                sceneCamera.FarPlaneDistance = (float)requiredFarPlane;
            }
        }

        /// <summary>
        /// Resolves one scalar selection extent for editor-only camera behavior tests and adaptive speed.
        /// </summary>
        /// <param name="selectedEntity">Selected entity whose bounds should be measured.</param>
        /// <returns>Largest supported selection dimension, or zero when no supported bounds exist.</returns>
        public double ResolveSelectionExtentForTest(Entity selectedEntity) {
            return ResolveSelectionExtent(selectedEntity);
        }

        /// <summary>
        /// Resolves one focus center and radius for the supplied entity.
        /// </summary>
        /// <param name="selectedEntity">Selected entity that should be framed.</param>
        /// <param name="focusCenter">Receives the resolved focus center.</param>
        /// <param name="focusRadius">Receives the resolved bounding radius.</param>
        void ResolveFocusBounds(Entity selectedEntity, out float3 focusCenter, out double focusRadius) {
            if (TryResolveViewportBounds(selectedEntity, out focusCenter, out focusRadius, out _)) {
                return;
            }
            if (TryResolveMeshBounds(selectedEntity, out focusCenter, out focusRadius, out _)) {
                return;
            }
            if (TryResolveSpriteBounds(selectedEntity, out focusCenter, out focusRadius, out _)) {
                return;
            }

            focusCenter = EditorViewportDirect2DPresentationService.ResolvePresentedWorldPosition(selectedEntity);
            focusRadius = MinimumFocusRadius;
        }

        /// <summary>
        /// Resolves one scalar selection extent from the supplied entity using the editor-supported bounds sources.
        /// </summary>
        /// <param name="selectedEntity">Selected entity whose extent should be resolved.</param>
        /// <returns>Largest supported selection dimension, or zero when no supported bounds exist.</returns>
        double ResolveSelectionExtent(Entity selectedEntity) {
            if (selectedEntity == null) {
                return 0.0;
            }

            if (TryResolveViewportBounds(selectedEntity, out _, out _, out double viewportExtent)) {
                return viewportExtent;
            }
            if (TryResolveMeshBounds(selectedEntity, out _, out _, out double meshExtent)) {
                return meshExtent;
            }
            if (TryResolveSpriteBounds(selectedEntity, out _, out _, out double spriteExtent)) {
                return spriteExtent;
            }

            return 0.0;
        }

        /// <summary>
        /// Resolves framing bounds for one authored viewport entity.
        /// </summary>
        /// <param name="selectedEntity">Selected entity that may own a viewport component.</param>
        /// <param name="focusCenter">Receives the resolved focus center.</param>
        /// <param name="focusRadius">Receives the resolved bounding radius.</param>
        /// <returns>True when viewport bounds were resolved successfully.</returns>
        bool TryResolveViewportBounds(Entity selectedEntity, out float3 focusCenter, out double focusRadius, out double selectionExtent) {
            if (!TryGetComponent(selectedEntity, out ViewportComponent viewportComponent)) {
                focusCenter = float3.Zero;
                focusRadius = 0.0;
                selectionExtent = 0.0;
                return false;
            }

            int2 viewportSize = EditorViewportDirect2DPresentationService.ResolvePresentedWorldSize(selectedEntity, viewportComponent);
            float3[] corners = new[] {
                TransformViewportPoint(selectedEntity, new float3(0f, 0f, 0f)),
                TransformViewportPoint(selectedEntity, new float3(viewportSize.X, 0f, 0f)),
                TransformViewportPoint(selectedEntity, new float3(0f, viewportSize.Y, 0f)),
                TransformViewportPoint(selectedEntity, new float3(viewportSize.X, viewportSize.Y, 0f))
            };
            ResolveBoundsFromPoints(corners, out focusCenter, out focusRadius);
            selectionExtent = Math.Max(viewportSize.X, viewportSize.Y);
            return true;
        }

        /// <summary>
        /// Resolves framing bounds from one mesh component and its runtime model bounds.
        /// </summary>
        /// <param name="selectedEntity">Selected entity that may own a mesh component.</param>
        /// <param name="focusCenter">Receives the resolved focus center.</param>
        /// <param name="focusRadius">Receives the resolved bounding radius.</param>
        /// <returns>True when mesh bounds were resolved successfully.</returns>
        bool TryResolveMeshBounds(Entity selectedEntity, out float3 focusCenter, out double focusRadius, out double selectionExtent) {
            if (!TryGetComponent(selectedEntity, out MeshComponent meshComponent) || meshComponent.Model == null) {
                focusCenter = float3.Zero;
                focusRadius = 0.0;
                selectionExtent = 0.0;
                return false;
            }

            float3 boundsMin = meshComponent.Model.BoundsMin;
            float3 boundsMax = meshComponent.Model.BoundsMax;
            float3[] corners = new[] {
                TransformModelPoint(selectedEntity, new float3(boundsMin.X, boundsMin.Y, boundsMin.Z)),
                TransformModelPoint(selectedEntity, new float3(boundsMax.X, boundsMin.Y, boundsMin.Z)),
                TransformModelPoint(selectedEntity, new float3(boundsMin.X, boundsMax.Y, boundsMin.Z)),
                TransformModelPoint(selectedEntity, new float3(boundsMax.X, boundsMax.Y, boundsMin.Z)),
                TransformModelPoint(selectedEntity, new float3(boundsMin.X, boundsMin.Y, boundsMax.Z)),
                TransformModelPoint(selectedEntity, new float3(boundsMax.X, boundsMin.Y, boundsMax.Z)),
                TransformModelPoint(selectedEntity, new float3(boundsMin.X, boundsMax.Y, boundsMax.Z)),
                TransformModelPoint(selectedEntity, new float3(boundsMax.X, boundsMax.Y, boundsMax.Z))
            };
            ResolveBoundsFromPoints(corners, out focusCenter, out focusRadius);
            double width = Math.Abs((boundsMax.X - boundsMin.X) * selectedEntity.Scale.X);
            double height = Math.Abs((boundsMax.Y - boundsMin.Y) * selectedEntity.Scale.Y);
            double depth = Math.Abs((boundsMax.Z - boundsMin.Z) * selectedEntity.Scale.Z);
            selectionExtent = Math.Max(width, Math.Max(height, depth));
            return true;
        }

        /// <summary>
        /// Resolves framing bounds from one sprite component when no viewport or mesh bounds are available.
        /// </summary>
        /// <param name="selectedEntity">Selected entity that may own a sprite component.</param>
        /// <param name="focusCenter">Receives the resolved focus center.</param>
        /// <param name="focusRadius">Receives the resolved bounding radius.</param>
        /// <returns>True when sprite bounds were resolved successfully.</returns>
        bool TryResolveSpriteBounds(Entity selectedEntity, out float3 focusCenter, out double focusRadius, out double selectionExtent) {
            if (!TryGetComponent(selectedEntity, out SpriteComponent spriteComponent)) {
                focusCenter = float3.Zero;
                focusRadius = 0.0;
                selectionExtent = 0.0;
                return false;
            }

            int width = Math.Max(1, spriteComponent.Size.X);
            int height = Math.Max(1, spriteComponent.Size.Y);
            int2 presentedSize = EditorViewportDirect2DPresentationService.ResolvePresentedComponentSize(selectedEntity, new int2(width, height));
            float3[] corners = new[] {
                TransformViewportPoint(selectedEntity, new float3(0f, 0f, 0f)),
                TransformViewportPoint(selectedEntity, new float3(presentedSize.X, 0f, 0f)),
                TransformViewportPoint(selectedEntity, new float3(0f, presentedSize.Y, 0f)),
                TransformViewportPoint(selectedEntity, new float3(presentedSize.X, presentedSize.Y, 0f))
            };
            ResolveBoundsFromPoints(corners, out focusCenter, out focusRadius);
            selectionExtent = Math.Max(presentedSize.X, presentedSize.Y);
            return true;
        }

        /// <summary>
        /// Applies one focused camera position while preserving the current camera orientation.
        /// </summary>
        /// <param name="cameraController">Viewport-local controller that should receive the new orbit target.</param>
        /// <param name="selectedEntity">Selected entity that owns the focused bounds.</param>
        /// <param name="focusCenter">World-space selection center.</param>
        /// <param name="requiredDistance">Required camera distance from the focus center.</param>
        void ApplyFocusedCameraTransform(EditorViewportCameraController cameraController, Entity selectedEntity, float3 focusCenter, double requiredDistance) {
            Entity cameraEntity = cameraController.Parent;
            float3 cameraForward = float4.RotateVector(new float3(0f, 0f, -1f), cameraEntity.Orientation);
            cameraEntity.Position = focusCenter - (cameraForward * (float)requiredDistance);
            cameraController.SetSelectionOrbitTargetOverride(selectedEntity, focusCenter);
        }

        /// <summary>
        /// Resolves one world-space bounds center and radius from a point cloud.
        /// </summary>
        /// <param name="points">World-space points that should be enclosed.</param>
        /// <param name="focusCenter">Receives the resolved center.</param>
        /// <param name="focusRadius">Receives the resolved radius.</param>
        void ResolveBoundsFromPoints(float3[] points, out float3 focusCenter, out double focusRadius) {
            if (points == null) {
                throw new ArgumentNullException(nameof(points));
            }
            if (points.Length == 0) {
                throw new InvalidOperationException("At least one point is required to resolve focus bounds.");
            }

            float3 minimum = points[0];
            float3 maximum = points[0];
            for (int pointIndex = 1; pointIndex < points.Length; pointIndex++) {
                float3 point = points[pointIndex];
                minimum = new float3(
                    Math.Min(minimum.X, point.X),
                    Math.Min(minimum.Y, point.Y),
                    Math.Min(minimum.Z, point.Z));
                maximum = new float3(
                    Math.Max(maximum.X, point.X),
                    Math.Max(maximum.Y, point.Y),
                    Math.Max(maximum.Z, point.Z));
            }

            focusCenter = new float3(
                (minimum.X + maximum.X) * 0.5f,
                (minimum.Y + maximum.Y) * 0.5f,
                (minimum.Z + maximum.Z) * 0.5f);
            focusRadius = 0.0;
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++) {
                double distance = GetDistance(points[pointIndex], focusCenter);
                if (distance > focusRadius) {
                    focusRadius = distance;
                }
            }

            if (focusRadius < MinimumFocusRadius) {
                focusRadius = MinimumFocusRadius;
            }
        }

        /// <summary>
        /// Transforms one viewport-local corner into world space using the authored viewport transform.
        /// </summary>
        /// <param name="entity">Entity that owns the viewport.</param>
        /// <param name="localPoint">Viewport-local point to transform.</param>
        /// <returns>World-space point.</returns>
        float3 TransformViewportPoint(Entity entity, float3 localPoint) {
            if (EditorViewportDirect2DPresentationService.TryResolveViewportOwner(entity, out Entity viewportOwner, out _)) {
                if (ReferenceEquals(entity, viewportOwner)) {
                    return EditorViewportDirect2DPresentationService.TransformPresentedViewportPoint(viewportOwner, localPoint);
                }

                return EditorViewportDirect2DPresentationService.TransformPresentedEntityLocalPoint(entity, localPoint);
            }

            float3 rotatedPoint = float4.RotateVector(localPoint, entity.Orientation);
            return entity.Position + rotatedPoint;
        }

        /// <summary>
        /// Transforms one model-space point into world space using the selected entity transform.
        /// </summary>
        /// <param name="entity">Entity that owns the runtime model.</param>
        /// <param name="localPoint">Model-space point to transform.</param>
        /// <returns>World-space point.</returns>
        float3 TransformModelPoint(Entity entity, float3 localPoint) {
            float3 scaledPoint = new float3(
                localPoint.X * entity.Scale.X,
                localPoint.Y * entity.Scale.Y,
                localPoint.Z * entity.Scale.Z);
            float3 rotatedPoint = float4.RotateVector(scaledPoint, entity.Orientation);
            return entity.Position + rotatedPoint;
        }

        /// <summary>
        /// Resolves the first component of the requested type from one entity.
        /// </summary>
        /// <typeparam name="T">Component type to resolve.</typeparam>
        /// <param name="entity">Entity that owns the component collection.</param>
        /// <param name="component">Receives the resolved component instance.</param>
        /// <returns>True when the entity owns one component of the requested type.</returns>
        bool TryGetComponent<T>(Entity entity, out T component) where T : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is T typedComponent) {
                        component = typedComponent;
                        return true;
                    }
                }
            }

            component = null;
            return false;
        }

        /// <summary>
        /// Computes the Euclidean distance between two world-space points.
        /// </summary>
        /// <param name="left">First point.</param>
        /// <param name="right">Second point.</param>
        /// <returns>Distance between the two points.</returns>
        double GetDistance(float3 left, float3 right) {
            double deltaX = left.X - right.X;
            double deltaY = left.Y - right.Y;
            double deltaZ = left.Z - right.Z;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
        }
    }
}
