namespace helengine.editor {
    /// <summary>
    /// Keeps the translation gizmo aligned to the currently selected entity.
    /// </summary>
    public class TransformTranslationGizmoFollowComponent : UpdateComponent {
        /// <summary>
        /// Perspective vertical field of view used by the 3D renderer.
        /// </summary>
        const double PerspectiveVerticalFieldOfViewRadians = Math.PI / 4.0;
        /// <summary>
        /// Fraction of viewport height the translation axis should occupy on screen.
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
        /// Smallest horizontal camera-to-gizmo magnitude used for facing-orientation decisions.
        /// </summary>
        const double MinimumHorizontalFacingLengthSquared = 0.000000000001;
        /// <summary>
        /// World-space up axis used for gizmo yaw rotations.
        /// </summary>
        static readonly float3 WorldUpAxis = new float3(0f, 1f, 0f);
        /// <summary>
        /// Horizontal forward reference used to decide whether the gizmo should be flipped.
        /// </summary>
        static readonly float3 HorizontalForwardAxis = new float3(0f, 0f, 1f);
        /// <summary>
        /// Half-turn around world up used to flip the gizmo in 180-degree intervals.
        /// </summary>
        static readonly float4 FlippedFacingOrientation = CreateFlippedFacingOrientation();
        /// <summary>
        /// Scene camera used to compute distance-based gizmo scaling.
        /// </summary>
        readonly CameraComponent SceneCamera;
        /// <summary>
        /// Root entity that owns all translation gizmo meshes.
        /// </summary>
        readonly EditorEntity GizmoRoot;
        /// <summary>
        /// Material used when an axis is not hovered.
        /// </summary>
        readonly RuntimeMaterial NormalAxisMaterial;
        /// <summary>
        /// Material used when an axis is hovered.
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
        /// Initializes a new gizmo follow component.
        /// </summary>
        /// <param name="sceneCamera">Scene camera that views the gizmo.</param>
        /// <param name="gizmoRoot">Root entity for the translation gizmo.</param>
        /// <param name="normalAxisMaterial">Material used for non-hovered axis visuals.</param>
        /// <param name="highlightAxisMaterial">Material used for hovered axis visuals.</param>
        public TransformTranslationGizmoFollowComponent(
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
            if (!IsTranslateToolActive()) {
                SetAxisVisualState(false);
                return;
            }

            Entity selectedEntity = EditorSelectionService.SelectedEntity;
            if (!ShouldDisplayForSelection(selectedEntity)) {
                SetAxisVisualState(false);
                return;
            }

            float3 selectedPosition = selectedEntity.Position;
            GizmoRoot.Orientation = float4.Identity;
            GizmoRoot.Position = selectedPosition;
            SetAxisVisualState(true);

            Entity cameraEntity = SceneCamera.Parent;
            if (cameraEntity == null) {
                throw new InvalidOperationException("Scene camera must belong to an entity.");
            }

            EnsureHandleBaseTransformsCached();
            bool useFlippedFacing = ShouldUseFlippedFacing(selectedPosition, cameraEntity.Position);
            ApplyFacingToHandles(useFlippedFacing);

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
            UpdateAxisHighlightMaterials();
        }

        /// <summary>
        /// Enables or disables all gizmo axis entities and their mesh children.
        /// </summary>
        /// <param name="enabled">True to render axis visuals; false to hide them.</param>
        void SetAxisVisualState(bool enabled) {
            for (int axisIndex = 0; axisIndex < GizmoRoot.Children.Count; axisIndex++) {
                Entity axis = GizmoRoot.Children[axisIndex];
                if (axis == null) {
                    continue;
                }

                axis.Enabled = enabled;
                if (axis.Children == null) {
                    continue;
                }

                for (int childIndex = 0; childIndex < axis.Children.Count; childIndex++) {
                    Entity axisChild = axis.Children[childIndex];
                    if (axisChild == null) {
                        continue;
                    }

                    axisChild.Enabled = enabled;
                }
            }
        }

        /// <summary>
        /// Applies highlight material state based on the currently hovered gizmo handle.
        /// </summary>
        void UpdateAxisHighlightMaterials() {
            Entity hoveredAxis = EditorGizmoHoverService.HoveredHandleEntity;
            for (int axisIndex = 0; axisIndex < GizmoRoot.Children.Count; axisIndex++) {
                if (GizmoRoot.Children[axisIndex] is not EditorEntity axisEntity) {
                    continue;
                }

                bool isHoveredAxis = hoveredAxis != null && ReferenceEquals(axisEntity, hoveredAxis);
                RuntimeMaterial material = isHoveredAxis ? HighlightAxisMaterial : NormalAxisMaterial;
                ApplyAxisMaterial(axisEntity, material);
            }
        }

        /// <summary>
        /// Applies one material to all mesh children of an axis entity.
        /// </summary>
        /// <param name="axisEntity">Axis entity whose mesh children are updated.</param>
        /// <param name="material">Material to apply.</param>
        void ApplyAxisMaterial(EditorEntity axisEntity, RuntimeMaterial material) {
            if (axisEntity == null) {
                throw new ArgumentNullException(nameof(axisEntity));
            }

            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            MeshComponent selfMesh = FindMeshComponent(axisEntity);
            if (selfMesh != null && !ReferenceEquals(selfMesh.Material, material)) {
                selfMesh.Material = material;
            }

            for (int childIndex = 0; childIndex < axisEntity.Children.Count; childIndex++) {
                if (axisEntity.Children[childIndex] is not Entity childEntity) {
                    continue;
                }

                MeshComponent mesh = FindMeshComponent(childEntity);
                if (mesh == null) {
                    continue;
                }

                if (!ReferenceEquals(mesh.Material, material)) {
                    mesh.Material = material;
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
        /// Determines whether the translation gizmo should be shown for the provided selection.
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
        /// Updates cone tip offsets so each axis tip remains aligned with its scaled shaft length.
        /// </summary>
        /// <param name="scale">Current gizmo world scale multiplier.</param>
        void UpdateAxisTipOffsets(float scale) {
            float axisOffset = TransformTranslationGizmoFactory.ShaftLength * scale;
            for (int axisIndex = 0; axisIndex < GizmoRoot.Children.Count; axisIndex++) {
                if (GizmoRoot.Children[axisIndex] is not EditorEntity axisEntity) {
                    continue;
                }

                float3 tipOffset = ResolveAxisTipOffset(axisEntity.Name, axisOffset);
                if (tipOffset == float3.Zero) {
                    continue;
                }

                for (int childIndex = 0; childIndex < axisEntity.Children.Count; childIndex++) {
                    if (axisEntity.Children[childIndex] is not EditorEntity axisChildEntity) {
                        continue;
                    }

                    if (!axisChildEntity.Name.EndsWith(" Tip", StringComparison.Ordinal)) {
                        continue;
                    }

                    axisChildEntity.Position = tipOffset;
                    break;
                }
            }
        }

        /// <summary>
        /// Resolves the world-axis tip offset for a translation axis name.
        /// </summary>
        /// <param name="axisName">Axis entity display name.</param>
        /// <param name="axisOffset">Offset magnitude from origin to shaft end.</param>
        /// <returns>Tip offset vector for the given axis.</returns>
        float3 ResolveAxisTipOffset(string axisName, float axisOffset) {
            if (axisName.EndsWith(" X", StringComparison.Ordinal)) {
                return new float3(axisOffset, 0f, 0f);
            }

            if (axisName.EndsWith(" Y", StringComparison.Ordinal)) {
                return new float3(0f, axisOffset, 0f);
            }

            if (axisName.EndsWith(" Z", StringComparison.Ordinal)) {
                return new float3(0f, 0f, axisOffset);
            }

            return float3.Zero;
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

            if (TransformTranslationGizmoFactory.AxisLength <= 0f) {
                throw new InvalidOperationException("Transform gizmo axis length must be greater than zero.");
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
            return targetWorldAxisLength / TransformTranslationGizmoFactory.AxisLength;
        }

        /// <summary>
        /// Determines whether translation gizmos should be active for the current viewport camera.
        /// </summary>
        /// <returns>True when the viewport tool mode is translation.</returns>
        bool IsTranslateToolActive() {
            return EditorViewportToolService.GetToolMode(SceneCamera) == EditorViewportToolMode.Translate;
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
                Entity handle = GizmoRoot.Children[handleIndex];
                if (handle == null) {
                    continue;
                }

                float3 baseLocalPosition = handle.Position - GizmoRoot.Position;
                BaseHandlePositions[handle] = baseLocalPosition;
                BaseHandleOrientations[handle] = handle.Orientation;
            }

            HandleBaseTransformsCached = true;
        }

        /// <summary>
        /// Applies snapped 0/180-degree facing transforms to direct gizmo-handle children.
        /// </summary>
        /// <param name="useFlippedFacing">True to apply the 180-degree yaw flip; false for default orientation.</param>
        void ApplyFacingToHandles(bool useFlippedFacing) {
            if (GizmoRoot.Children == null) {
                throw new InvalidOperationException("Gizmo root children must be initialized.");
            }

            for (int handleIndex = 0; handleIndex < GizmoRoot.Children.Count; handleIndex++) {
                Entity handle = GizmoRoot.Children[handleIndex];
                if (handle == null) {
                    continue;
                }

                if (!BaseHandlePositions.TryGetValue(handle, out float3 basePosition)) {
                    continue;
                }
                if (!BaseHandleOrientations.TryGetValue(handle, out float4 baseOrientation)) {
                    continue;
                }

                if (useFlippedFacing) {
                    handle.Position = new float3(-basePosition.X, basePosition.Y, -basePosition.Z);
                    handle.Orientation = FlippedFacingOrientation * baseOrientation;
                } else {
                    handle.Position = basePosition;
                    handle.Orientation = baseOrientation;
                }
            }
        }

        /// <summary>
        /// Determines whether the gizmo should use the 180-degree flipped yaw facing.
        /// </summary>
        /// <param name="gizmoPosition">Current gizmo world position.</param>
        /// <param name="cameraPosition">Scene camera world position.</param>
        /// <returns>True when the flipped facing should be used.</returns>
        bool ShouldUseFlippedFacing(float3 gizmoPosition, float3 cameraPosition) {
            float3 toCamera = cameraPosition - gizmoPosition;
            float3 horizontalToCamera = new float3(toCamera.X, 0f, toCamera.Z);
            double horizontalLengthSquared =
                (horizontalToCamera.X * horizontalToCamera.X) +
                (horizontalToCamera.Z * horizontalToCamera.Z);
            if (horizontalLengthSquared <= MinimumHorizontalFacingLengthSquared) {
                return false;
            }

            double inverseLength = 1.0 / Math.Sqrt(horizontalLengthSquared);
            float3 horizontalDirection = new float3(
                (float)(horizontalToCamera.X * inverseLength),
                0f,
                (float)(horizontalToCamera.Z * inverseLength));
            double facingDot = float3.Dot(horizontalDirection, HorizontalForwardAxis);
            return facingDot < 0.0;
        }

        /// <summary>
        /// Creates a quaternion that rotates 180 degrees around world up.
        /// </summary>
        /// <returns>Half-turn quaternion around the world Y axis.</returns>
        static float4 CreateFlippedFacingOrientation() {
            float3 axis = WorldUpAxis;
            float4 orientation;
            float4.CreateFromAxisAngle(ref axis, (float)Math.PI, out orientation);
            return orientation;
        }
    }
}
