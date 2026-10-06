namespace helengine.editor {
    /// <summary>
    /// Frames the current editor selection inside one scene viewport camera without changing runtime engine behavior.
    /// </summary>
    public sealed class EditorViewportSelectionFramingService {
        /// <summary>
        /// Extra distance added beyond the framed selection radius so the target does not touch the clip plane.
        /// </summary>
        const double FarPlaneMargin = 8.0;

        /// <summary>
        /// Fractional clearance applied around the resolved selection bounds.
        /// </summary>
        const double FocusPaddingFactor = 1.1;

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

            float4 viewport = sceneCamera.Viewport;
            if (!float.IsFinite(viewport.Z) || !float.IsFinite(viewport.W) || viewport.Z <= 1f || viewport.W <= 1f) {
                return;
            }

            float3 focusCenter;
            double focusRadius;
            ResolveFocusBounds(selectedEntity, out focusCenter, out focusRadius);
            if (!float.IsFinite(focusCenter.X) || !float.IsFinite(focusCenter.Y) || !float.IsFinite(focusCenter.Z) ||
                !double.IsFinite(focusRadius)) {
                return;
            }

            double viewportWidth = viewport.Z;
            double viewportHeight = viewport.W;
            double aspectRatio = viewportWidth / viewportHeight;
            if (!double.IsFinite(aspectRatio) || aspectRatio <= 0.0) {
                return;
            }

            double requiredDistance;
            double verticalSpan = 0.0;
            if (sceneCamera is ICameraProjectionSettings projectionSettings && projectionSettings.ProjectionMode == CameraProjectionMode.Orthographic) {
                verticalSpan = 2.0 * focusRadius * FocusPaddingFactor * Math.Max(1.0, 1.0 / aspectRatio);
                if (!double.IsFinite(verticalSpan) || verticalSpan < CameraProjectionUtils.MinimumOrthographicVerticalSpan || verticalSpan > float.MaxValue) {
                    return;
                }

                requiredDistance = Math.Max(focusRadius + Math.Max(0.1, sceneCamera.NearPlaneDistance),
                    GetDistance(cameraController.Parent.Position, cameraController.GetOrbitTarget()));
            } else {
                float fieldOfView = sceneCamera.FieldOfView;
                if (!float.IsFinite(fieldOfView) || fieldOfView <= 0f) {
                    return;
                }
                double halfVerticalFieldOfView = CameraProjectionUtils.ClampFieldOfView(fieldOfView) * 0.5;
                double halfHorizontalFieldOfView = Math.Atan(Math.Tan(halfVerticalFieldOfView) * aspectRatio);
                double limitingHalfFieldOfView = Math.Min(halfVerticalFieldOfView, halfHorizontalFieldOfView);
                requiredDistance = focusRadius * FocusPaddingFactor / Math.Sin(limitingHalfFieldOfView);
                if (requiredDistance < MinimumFocusRadius) {
                    requiredDistance = MinimumFocusRadius;
                }
            }

            requiredDistance = Math.Max(requiredDistance, focusRadius + Math.Max(0.1, sceneCamera.NearPlaneDistance));
            double requiredFarPlane = requiredDistance + focusRadius + FarPlaneMargin;
            // Reject bounds that cannot fit within the camera's single-precision world and clipping range before changing its state.
            if (!double.IsFinite(requiredFarPlane) || requiredFarPlane > float.MaxValue ||
                Math.Abs((double)focusCenter.X) + requiredDistance > float.MaxValue ||
                Math.Abs((double)focusCenter.Y) + requiredDistance > float.MaxValue ||
                Math.Abs((double)focusCenter.Z) + requiredDistance > float.MaxValue) {
                return;
            }

            ApplyFocusedCameraTransform(cameraController, selectedEntity, focusCenter, requiredDistance);
            if (verticalSpan > 0.0) {
                ((ICameraProjectionSettings)sceneCamera).OrthographicVerticalSpan = (float)verticalSpan;
            }
            if (requiredFarPlane > sceneCamera.FarPlaneDistance) {
                sceneCamera.FarPlaneDistance = (float)requiredFarPlane;
            }
        }

        /// <summary>Measures the visible hierarchy framed by F for perspective navigation speed.</summary>
        /// <param name="selectedEntity">Selected root, including its visible descendants.</param>
        /// <returns>Largest world-space bounds dimension, or zero for an unsupported selection.</returns>
        public double ResolveHierarchySelectionExtent(Entity selectedEntity) {
            if (selectedEntity == null) {
                return 0.0;
            }
            List<float3> points = new List<float3>();
            CollectVisibleBounds(selectedEntity, points);
            if (points.Count == 0) {
                return 0.0;
            }
            float3 minimum = points[0];
            float3 maximum = points[0];
            foreach (float3 point in points) {
                minimum = new float3(Math.Min(minimum.X, point.X), Math.Min(minimum.Y, point.Y), Math.Min(minimum.Z, point.Z));
                maximum = new float3(Math.Max(maximum.X, point.X), Math.Max(maximum.Y, point.Y), Math.Max(maximum.Z, point.Z));
            }
            return Math.Max((double)maximum.X - minimum.X, Math.Max((double)maximum.Y - minimum.Y, (double)maximum.Z - minimum.Z));
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
            List<float3> points = new List<float3>();
            CollectVisibleBounds(selectedEntity, points);
            if (points.Count > 0) {
                ResolveBoundsFromPoints(points.ToArray(), out focusCenter, out focusRadius);
                return;
            }
            focusCenter = EditorViewportDirect2DPresentationService.ResolvePresentedWorldPosition(selectedEntity);
            focusRadius = MinimumFocusRadius;
        }

        /// <summary>Collects the actual presented corners of visible authored content throughout a selection subtree.</summary>
        /// <param name="entity">Current selected entity or descendant.</param>
        /// <param name="points">Shared world-space corner collection.</param>
        void CollectVisibleBounds(Entity entity, List<float3> points) {
            if (!entity.IsHierarchyEnabled || entity is EditorEntity editorEntity && editorEntity.InternalEntity) {
                return;
            }
            TryResolveViewportBounds(entity, out _, out _, out _, points);
            TryResolveMeshBounds(entity, out _, out _, out _, points);
            TryResolveSpriteBounds(entity, out _, out _, out _, points);
            foreach (Entity child in entity.Children) {
                CollectVisibleBounds(child, points);
            }
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
        /// <param name="selectionExtent">Largest viewport dimension.</param>
        /// <param name="points">Optional collection receiving the presented corners.</param>
        /// <returns>True when viewport bounds were resolved successfully.</returns>
        bool TryResolveViewportBounds(Entity selectedEntity, out float3 focusCenter, out double focusRadius, out double selectionExtent, List<float3> points = null) {
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
            points?.AddRange(corners);
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
        /// <param name="selectionExtent">Largest scaled mesh dimension.</param>
        /// <param name="points">Optional collection receiving transformed model corners.</param>
        /// <returns>True when mesh bounds were resolved successfully.</returns>
        bool TryResolveMeshBounds(Entity selectedEntity, out float3 focusCenter, out double focusRadius, out double selectionExtent, List<float3> points = null) {
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
            points?.AddRange(corners);
            ResolveBoundsFromPoints(corners, out focusCenter, out focusRadius);
            double width = Math.Abs(((double)boundsMax.X - boundsMin.X) * selectedEntity.Scale.X);
            double height = Math.Abs(((double)boundsMax.Y - boundsMin.Y) * selectedEntity.Scale.Y);
            double depth = Math.Abs(((double)boundsMax.Z - boundsMin.Z) * selectedEntity.Scale.Z);
            selectionExtent = Math.Max(width, Math.Max(height, depth));
            return true;
        }

        /// <summary>
        /// Resolves the presented rectangle of a sprite, text, or rounded panel.
        /// </summary>
        /// <param name="selectedEntity">Selected entity that may own a sprite component.</param>
        /// <param name="focusCenter">Receives the resolved focus center.</param>
        /// <param name="focusRadius">Receives the resolved bounding radius.</param>
        /// <param name="selectionExtent">Largest presented rectangle dimension.</param>
        /// <param name="points">Optional collection receiving the presented corners.</param>
        /// <returns>True when supported 2D bounds were resolved successfully.</returns>
        bool TryResolveSpriteBounds(Entity selectedEntity, out float3 focusCenter, out double focusRadius, out double selectionExtent, List<float3> points = null) {
            int2 size;
            if (TryGetComponent(selectedEntity, out SpriteComponent spriteComponent)) {
                size = spriteComponent.Size;
            } else if (TryGetComponent(selectedEntity, out TextComponent textComponent)) {
                size = textComponent.Size;
            } else if (TryGetComponent(selectedEntity, out RoundedRectComponent roundedRectComponent)) {
                size = roundedRectComponent.Size;
            } else {
                focusCenter = float3.Zero;
                focusRadius = 0.0;
                selectionExtent = 0.0;
                return false;
            }
            int width = Math.Max(1, size.X);
            int height = Math.Max(1, size.Y);
            int2 presentedSize = EditorViewportDirect2DPresentationService.ResolvePresentedComponentSize(selectedEntity, new int2(width, height));
            float3[] corners = new[] {
                TransformViewportPoint(selectedEntity, new float3(0f, 0f, 0f)),
                TransformViewportPoint(selectedEntity, new float3(presentedSize.X, 0f, 0f)),
                TransformViewportPoint(selectedEntity, new float3(0f, presentedSize.Y, 0f)),
                TransformViewportPoint(selectedEntity, new float3(presentedSize.X, presentedSize.Y, 0f))
            };
            points?.AddRange(corners);
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
            cameraController.SetViewPose(focusCenter, cameraEntity.Orientation, requiredDistance);
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
                (float)(((double)minimum.X + maximum.X) * 0.5),
                (float)(((double)minimum.Y + maximum.Y) * 0.5),
                (float)(((double)minimum.Z + maximum.Z) * 0.5));
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
            return float3.Distance(left, right);
        }

    }
}
