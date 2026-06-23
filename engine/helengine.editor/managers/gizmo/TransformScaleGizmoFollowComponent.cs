namespace helengine.editor {
    /// <summary>
    /// Keeps the scale gizmo aligned to the currently selected entity.
    /// </summary>
    public class TransformScaleGizmoFollowComponent : UpdateComponent {
        /// <summary>
        /// Perspective vertical field of view used by the 3D renderer.
        /// </summary>
        const double PerspectiveVerticalFieldOfViewRadians = Math.PI / 4.0;
        /// <summary>
        /// Fraction of viewport height the scale axis should occupy on screen.
        /// </summary>
        const double TargetViewportHeightFraction = 0.20;
        /// <summary>
        /// Smallest camera distance value used to keep scale calculations stable.
        /// </summary>
        const double MinimumDistance = 0.001;
        /// <summary>
        /// Smallest allowed gizmo scale in world units.
        /// </summary>
        const double MinimumScale = 0.0001;
        /// <summary>
        /// Largest allowed gizmo scale in world units.
        /// </summary>
        const double MaximumScale = 100000.0;
        /// <summary>
        /// Smallest squared vector magnitude treated as non-zero for normalized direction solving.
        /// </summary>
        const double MinimumDirectionLengthSquared = 0.000000000001;
        /// <summary>
        /// Scene camera used to compute distance-based gizmo scaling.
        /// </summary>
        readonly CameraComponent SceneCamera;
        /// <summary>
        /// Root entity that owns all scale gizmo meshes.
        /// </summary>
        readonly EditorEntity GizmoRoot;
        /// <summary>
        /// Material used when a handle is not hovered.
        /// </summary>
        readonly RuntimeMaterial NormalAxisMaterial;
        /// <summary>
        /// Material used when a handle is hovered.
        /// </summary>
        readonly RuntimeMaterial HighlightAxisMaterial;
        /// <summary>
        /// Cached base local positions for direct gizmo-handle children.
        /// </summary>
        readonly Dictionary<Entity, float3> BaseHandlePositions;
        /// <summary>
        /// Cached base local orientations for direct gizmo-handle children.
        /// </summary>
        readonly Dictionary<Entity, float4> BaseHandleOrientations;
        /// <summary>
        /// True when base handle transforms were captured from the factory layout.
        /// </summary>
        bool HandleBaseTransformsCached;

        /// <summary>
        /// Initializes a new scale gizmo follow component.
        /// </summary>
        /// <param name="sceneCamera">Scene camera that views the gizmo.</param>
        /// <param name="gizmoRoot">Root entity for the scale gizmo.</param>
        /// <param name="normalAxisMaterial">Material used for non-hovered handle visuals.</param>
        /// <param name="highlightAxisMaterial">Material used for hovered handle visuals.</param>
        public TransformScaleGizmoFollowComponent(
            CameraComponent sceneCamera,
            EditorEntity gizmoRoot,
            RuntimeMaterial normalAxisMaterial,
            RuntimeMaterial highlightAxisMaterial) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            GizmoRoot = gizmoRoot ?? throw new ArgumentNullException(nameof(gizmoRoot));
            NormalAxisMaterial = normalAxisMaterial ?? throw new ArgumentNullException(nameof(normalAxisMaterial));
            HighlightAxisMaterial = highlightAxisMaterial ?? throw new ArgumentNullException(nameof(highlightAxisMaterial));
            BaseHandlePositions = new Dictionary<Entity, float3>();
            BaseHandleOrientations = new Dictionary<Entity, float4>();
            HandleBaseTransformsCached = false;
        }

        /// <summary>
        /// Updates gizmo visibility, position, and scale from the current editor selection.
        /// </summary>
        public override void Update() {
            if (!IsScaleToolActive()) {
                SetHandleVisualState(false);
                return;
            }

            Entity selectedEntity = EditorSelectionService.SelectedEntity;
            if (!ShouldDisplayForSelection(selectedEntity)) {
                SetHandleVisualState(false);
                return;
            }

            float3 selectedPosition = helengine.editor.EditorViewportDirect2DPresentationService.ResolvePresentedWorldAnchorPosition(selectedEntity);
            GizmoRoot.Orientation = float4.Identity;
            GizmoRoot.Position = selectedPosition;
            SetHandleVisualState(true);
            RestoreVisibleHandleLocalScales();

            Entity cameraEntity = SceneCamera.Parent;
            if (cameraEntity == null) {
                throw new InvalidOperationException("Scene camera must belong to an entity.");
            }

            EnsureHandleBaseTransformsCached();
            float4 yawFacingOrientation = TransformGizmoYawSnapper.ComputeSnappedYawFacingOrientation(selectedPosition, cameraEntity.Position);
            ApplyFacingToHandles(yawFacingOrientation);

            if (!EditorGizmoDragService.IsDragging(SceneCamera)) {
                float4 viewport = SceneCamera.Viewport;
                double viewportHeight = viewport.W;
                if (viewportHeight <= 0.0) {
                    throw new InvalidOperationException("Scene camera viewport height must be greater than zero.");
                }

                double targetAxisPixels = viewportHeight * TargetViewportHeightFraction;
                double scaleValue = ComputeScaleForTargetPixels(selectedPosition, cameraEntity.Position, viewportHeight, targetAxisPixels);
                if (scaleValue < MinimumScale) {
                    scaleValue = MinimumScale;
                }
                if (scaleValue > MaximumScale) {
                    scaleValue = MaximumScale;
                }

                float scale = (float)scaleValue;
                GizmoRoot.Scale = new float3(scale, scale, scale);
                UpdateAxisTipOffsets(scale);
            }

            UpdateHandleHighlightMaterials();
        }

        /// <summary>
        /// Enables or disables all scale gizmo handles and their mesh children.
        /// </summary>
        /// <param name="enabled">True to render handle visuals; false to hide them.</param>
        void SetHandleVisualState(bool enabled) {
            for (int handleIndex = 0; handleIndex < GizmoRoot.Children.Count; handleIndex++) {
                Entity handleEntity = GizmoRoot.Children[handleIndex];
                if (handleEntity == null) {
                    continue;
                }

                handleEntity.Enabled = enabled;
                if (handleEntity.Children == null) {
                    continue;
                }

                for (int childIndex = 0; childIndex < handleEntity.Children.Count; childIndex++) {
                    Entity childEntity = handleEntity.Children[childIndex];
                    if (childEntity == null) {
                        continue;
                    }

                    childEntity.Enabled = enabled;
                }
            }
        }

        /// <summary>
        /// Restores non-zero local scales for visible handles so authored zero-scale setup does not collapse their meshes at draw time.
        /// </summary>
        void RestoreVisibleHandleLocalScales() {
            for (int handleIndex = 0; handleIndex < GizmoRoot.Children.Count; handleIndex++) {
                if (GizmoRoot.Children[handleIndex] is not Entity handleEntity || !handleEntity.Enabled) {
                    continue;
                }

                handleEntity.LocalScale = float3.One;
                if (handleEntity.Children == null) {
                    continue;
                }

                for (int childIndex = 0; childIndex < handleEntity.Children.Count; childIndex++) {
                    if (handleEntity.Children[childIndex] is not Entity childEntity || !childEntity.Enabled) {
                        continue;
                    }

                    childEntity.LocalScale = float3.One;
                }
            }
        }

        /// <summary>
        /// Applies highlight material state based on the currently hovered scale handle.
        /// </summary>
        void UpdateHandleHighlightMaterials() {
            Entity hoveredHandle = EditorGizmoHoverService.HoveredHandleEntity;
            for (int handleIndex = 0; handleIndex < GizmoRoot.Children.Count; handleIndex++) {
                if (GizmoRoot.Children[handleIndex] is not EditorEntity handleEntity) {
                    continue;
                }

                bool isHoveredHandle = hoveredHandle != null && ReferenceEquals(handleEntity, hoveredHandle);
                RuntimeMaterial material = isHoveredHandle ? HighlightAxisMaterial : NormalAxisMaterial;
                ApplyHandleMaterial(handleEntity, material);
            }
        }

        /// <summary>
        /// Applies one material to all mesh children of a handle entity.
        /// </summary>
        /// <param name="handleEntity">Handle entity whose mesh children are updated.</param>
        /// <param name="material">Material to apply.</param>
        void ApplyHandleMaterial(EditorEntity handleEntity, RuntimeMaterial material) {
            if (handleEntity == null) {
                throw new ArgumentNullException(nameof(handleEntity));
            }

            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            MeshComponent selfMesh = FindMeshComponent(handleEntity);
            if (selfMesh != null && (selfMesh.Materials.Length == 0 || !ReferenceEquals(selfMesh.Materials[0], material))) {
                selfMesh.Materials = new[] { material };
            }

            for (int childIndex = 0; childIndex < handleEntity.Children.Count; childIndex++) {
                if (handleEntity.Children[childIndex] is not Entity childEntity) {
                    continue;
                }

                MeshComponent mesh = FindMeshComponent(childEntity);
                if (mesh == null) {
                    continue;
                }

                if (mesh.Materials.Length == 0 || !ReferenceEquals(mesh.Materials[0], material)) {
                    mesh.Materials = new[] { material };
                }
            }
        }

        /// <summary>
        /// Finds the first mesh component attached to an entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Mesh component when present; otherwise null.</returns>
        MeshComponent FindMeshComponent(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is MeshComponent meshComponent) {
                    return meshComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether the scale gizmo should be shown for the provided selection.
        /// </summary>
        /// <param name="selectedEntity">Currently selected entity.</param>
        /// <returns>True when the gizmo should be visible.</returns>
        bool ShouldDisplayForSelection(Entity selectedEntity) {
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
        /// Updates box-tip offsets so each axis tip remains aligned with its scaled shaft length.
        /// </summary>
        /// <param name="scale">Current gizmo world scale multiplier.</param>
        void UpdateAxisTipOffsets(float scale) {
            float axisOffset = TransformScaleGizmoFactory.ShaftLength * scale;
            for (int handleIndex = 0; handleIndex < GizmoRoot.Children.Count; handleIndex++) {
                if (GizmoRoot.Children[handleIndex] is not EditorEntity handleEntity) {
                    continue;
                }

                float3 tipOffset = ResolveAxisTipOffset(handleEntity, axisOffset);
                if (tipOffset == float3.Zero) {
                    continue;
                }

                for (int childIndex = 0; childIndex < handleEntity.Children.Count; childIndex++) {
                    if (handleEntity.Children[childIndex] is not EditorEntity childEntity) {
                        continue;
                    }

                    if (!childEntity.Name.EndsWith(" Tip", StringComparison.Ordinal)) {
                        continue;
                    }

                    childEntity.Position = tipOffset;
                    break;
                }
            }
        }

        /// <summary>
        /// Resolves the world-axis tip offset for a scale handle entity.
        /// </summary>
        /// <param name="handleEntity">Axis handle entity.</param>
        /// <param name="axisOffset">Offset magnitude from origin to shaft end.</param>
        /// <returns>Tip offset vector for the given axis.</returns>
        float3 ResolveAxisTipOffset(Entity handleEntity, float axisOffset) {
            if (handleEntity == null) {
                throw new ArgumentNullException(nameof(handleEntity));
            }

            float3 primaryDirection = ResolveHandleLocalPrimaryDirection(handleEntity);
            if (primaryDirection == float3.Zero) {
                return float3.Zero;
            }

            return primaryDirection * axisOffset;
        }

        /// <summary>
        /// Computes gizmo scale so the axis length stays at a constant on-screen pixel height fraction.
        /// </summary>
        /// <param name="origin">Gizmo origin in world space.</param>
        /// <param name="cameraPosition">Scene camera world position.</param>
        /// <param name="viewportHeight">Active camera viewport height in pixels.</param>
        /// <param name="targetAxisPixels">Desired axis size in pixels.</param>
        /// <returns>Scale factor applied to gizmo meshes.</returns>
        double ComputeScaleForTargetPixels(float3 origin, float3 cameraPosition, double viewportHeight, double targetAxisPixels) {
            if (viewportHeight <= 0.0) {
                throw new InvalidOperationException("Viewport height must be greater than zero.");
            }

            if (targetAxisPixels <= 0.0) {
                throw new InvalidOperationException("Target gizmo pixel size must be greater than zero.");
            }

            if (TransformScaleGizmoFactory.AxisLength <= 0f) {
                throw new InvalidOperationException("Scale gizmo axis length must be greater than zero.");
            }

            float3 offset = origin - cameraPosition;
            double distance = Math.Sqrt(
                offset.X * offset.X +
                offset.Y * offset.Y +
                offset.Z * offset.Z);
            if (distance < MinimumDistance) {
                distance = MinimumDistance;
            }

            double tanHalfFov = Math.Tan(PerspectiveVerticalFieldOfViewRadians * 0.5);
            if (tanHalfFov <= 0.0) {
                throw new InvalidOperationException("Perspective field of view must produce a positive tangent value.");
            }

            double targetWorldAxisLength = targetAxisPixels * (2.0 * distance * tanHalfFov) / viewportHeight;
            return targetWorldAxisLength / TransformScaleGizmoFactory.AxisLength;
        }

        /// <summary>
        /// Determines whether scale gizmos should be active for the current viewport camera.
        /// </summary>
        /// <returns>True when the viewport tool mode is scale.</returns>
        bool IsScaleToolActive() {
            return EditorViewportToolService.GetToolMode(SceneCamera) == EditorViewportToolMode.Scale;
        }

        /// <summary>
        /// Captures base local transforms for direct gizmo children on first use.
        /// </summary>
        void EnsureHandleBaseTransformsCached() {
            if (HandleBaseTransformsCached) {
                return;
            }

            if (GizmoRoot.Children == null) {
                throw new InvalidOperationException("Gizmo root children must be initialized.");
            }

            BaseHandlePositions.Clear();
            BaseHandleOrientations.Clear();
            for (int handleIndex = 0; handleIndex < GizmoRoot.Children.Count; handleIndex++) {
                Entity handleEntity = GizmoRoot.Children[handleIndex];
                if (handleEntity == null) {
                    continue;
                }

                float3 baseLocalPosition = handleEntity.Position - GizmoRoot.Position;
                BaseHandlePositions[handleEntity] = baseLocalPosition;
                BaseHandleOrientations[handleEntity] = handleEntity.Orientation;
            }

            HandleBaseTransformsCached = true;
        }

        /// <summary>
        /// Applies snapped yaw facing transforms to direct gizmo-handle children.
        /// </summary>
        /// <param name="yawFacingOrientation">Quaternion representing the snapped world-space Y-axis yaw orientation.</param>
        void ApplyFacingToHandles(float4 yawFacingOrientation) {
            if (GizmoRoot.Children == null) {
                throw new InvalidOperationException("Gizmo root children must be initialized.");
            }

            for (int handleIndex = 0; handleIndex < GizmoRoot.Children.Count; handleIndex++) {
                Entity handleEntity = GizmoRoot.Children[handleIndex];
                if (handleEntity == null) {
                    continue;
                }

                if (!BaseHandlePositions.TryGetValue(handleEntity, out float3 basePosition)) {
                    continue;
                }
                if (!BaseHandleOrientations.TryGetValue(handleEntity, out float4 baseOrientation)) {
                    continue;
                }

                handleEntity.Position = float4.RotateVector(basePosition, yawFacingOrientation);
                handleEntity.Orientation = yawFacingOrientation * baseOrientation;
            }
        }

        /// <summary>
        /// Resolves the local-space primary direction for a scale handle.
        /// </summary>
        /// <param name="handleEntity">Handle entity to evaluate.</param>
        /// <returns>Normalized local-space primary direction, or zero when unavailable.</returns>
        float3 ResolveHandleLocalPrimaryDirection(Entity handleEntity) {
            if (handleEntity == null) {
                throw new ArgumentNullException(nameof(handleEntity));
            }

            if (!TryFindTransformHandleComponent(handleEntity, out TransformGizmoHandleComponent handleComponent)) {
                return float3.Zero;
            }

            return NormalizeDirection(handleComponent.LocalPrimaryDirection);
        }

        /// <summary>
        /// Finds a transform-gizmo handle component on an entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <param name="handleComponent">Resolved handle component when present.</param>
        /// <returns>True when the component is present; otherwise false.</returns>
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
        /// Normalizes a direction vector and returns zero when its magnitude is too small.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <returns>Normalized vector, or zero when input magnitude is too small.</returns>
        float3 NormalizeDirection(float3 value) {
            double lengthSquared =
                (value.X * value.X) +
                (value.Y * value.Y) +
                (value.Z * value.Z);
            if (lengthSquared <= MinimumDirectionLengthSquared) {
                return float3.Zero;
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(value.X * inverseLength),
                (float)(value.Y * inverseLength),
                (float)(value.Z * inverseLength));
        }
    }
}
