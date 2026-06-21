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
        /// Smallest squared vector magnitude treated as non-zero for normalized direction solving.
        /// </summary>
        const double MinimumDirectionLengthSquared = 0.000000000001;
        /// <summary>
        /// Active translation gizmo follow components keyed by the viewport camera that owns them.
        /// </summary>
        static readonly Dictionary<CameraComponent, TransformTranslationGizmoFollowComponent> FollowComponentByCamera =
            new Dictionary<CameraComponent, TransformTranslationGizmoFollowComponent>();
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
        /// Material used when a plane handle is not hovered.
        /// </summary>
        readonly RuntimeMaterial NormalPlaneMaterial;
        /// <summary>
        /// Material used when a plane handle is hovered.
        /// </summary>
        readonly RuntimeMaterial HighlightPlaneMaterial;
        /// <summary>
        /// Reusable preview entity that visualizes active translation snapping.
        /// </summary>
        readonly EditorEntity SnapPreviewEntity;
        /// <summary>
        /// Mesh component used by the reusable translation snap-preview entity.
        /// </summary>
        readonly MeshComponent SnapPreviewMesh;
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
        /// <param name="snapPreviewEntity">Reusable grid-preview entity shown while snap modifiers are held.</param>
        public TransformTranslationGizmoFollowComponent(
            CameraComponent sceneCamera,
            EditorEntity gizmoRoot,
            RuntimeMaterial normalAxisMaterial,
            RuntimeMaterial highlightAxisMaterial,
            EditorEntity snapPreviewEntity) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }

            if (gizmoRoot == null) {
                throw new ArgumentNullException(nameof(gizmoRoot));
            }

            if (normalAxisMaterial == null) {
                throw new ArgumentNullException(nameof(normalAxisMaterial));
            }

            if (highlightAxisMaterial == null) {
                throw new ArgumentNullException(nameof(highlightAxisMaterial));
            }

            if (snapPreviewEntity == null) {
                throw new ArgumentNullException(nameof(snapPreviewEntity));
            }

            SceneCamera = sceneCamera;
            GizmoRoot = gizmoRoot;
            NormalAxisMaterial = normalAxisMaterial;
            HighlightAxisMaterial = highlightAxisMaterial;
            NormalPlaneMaterial = normalAxisMaterial;
            HighlightPlaneMaterial = highlightAxisMaterial;
            SnapPreviewEntity = snapPreviewEntity;
            SnapPreviewMesh = FindMeshComponent(snapPreviewEntity) ?? throw new InvalidOperationException("Translation snap-preview entity must include a mesh component.");
            BaseHandlePositions = new Dictionary<Entity, float3>();
            BaseHandleOrientations = new Dictionary<Entity, float4>();
            HandleBaseTransformsCached = false;
        }

        /// <summary>
        /// Initializes a new gizmo follow component with dedicated materials for plane handles.
        /// </summary>
        /// <param name="sceneCamera">Scene camera that views the gizmo.</param>
        /// <param name="gizmoRoot">Root entity for the translation gizmo.</param>
        /// <param name="normalAxisMaterial">Material used for non-hovered axis visuals.</param>
        /// <param name="highlightAxisMaterial">Material used for hovered axis visuals.</param>
        /// <param name="normalPlaneMaterial">Material used for non-hovered plane visuals.</param>
        /// <param name="highlightPlaneMaterial">Material used for hovered plane visuals.</param>
        /// <param name="snapPreviewEntity">Reusable grid-preview entity shown while snap modifiers are held.</param>
        public TransformTranslationGizmoFollowComponent(
            CameraComponent sceneCamera,
            EditorEntity gizmoRoot,
            RuntimeMaterial normalAxisMaterial,
            RuntimeMaterial highlightAxisMaterial,
            RuntimeMaterial normalPlaneMaterial,
            RuntimeMaterial highlightPlaneMaterial,
            EditorEntity snapPreviewEntity) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            GizmoRoot = gizmoRoot ?? throw new ArgumentNullException(nameof(gizmoRoot));
            NormalAxisMaterial = normalAxisMaterial ?? throw new ArgumentNullException(nameof(normalAxisMaterial));
            HighlightAxisMaterial = highlightAxisMaterial ?? throw new ArgumentNullException(nameof(highlightAxisMaterial));
            NormalPlaneMaterial = normalPlaneMaterial ?? throw new ArgumentNullException(nameof(normalPlaneMaterial));
            HighlightPlaneMaterial = highlightPlaneMaterial ?? throw new ArgumentNullException(nameof(highlightPlaneMaterial));
            SnapPreviewEntity = snapPreviewEntity ?? throw new ArgumentNullException(nameof(snapPreviewEntity));
            SnapPreviewMesh = FindMeshComponent(snapPreviewEntity) ?? throw new InvalidOperationException("Translation snap-preview entity must include a mesh component.");
            BaseHandlePositions = new Dictionary<Entity, float3>();
            BaseHandleOrientations = new Dictionary<Entity, float4>();
            HandleBaseTransformsCached = false;
        }

        /// <summary>
        /// Gets the viewport camera that drives this translation gizmo instance.
        /// </summary>
        public CameraComponent Camera => SceneCamera;

        /// <summary>
        /// Gets the current uniform gizmo scale applied to the translation handles.
        /// </summary>
        public float CurrentScale => GizmoRoot.Scale.X;

        /// <summary>
        /// Gets the registered translation-gizmo follow component for the supplied viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera that owns the translation gizmo.</param>
        /// <returns>Registered follow component when present; otherwise null.</returns>
        public static TransformTranslationGizmoFollowComponent GetForCamera(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            FollowComponentByCamera.TryGetValue(camera, out TransformTranslationGizmoFollowComponent followComponent);
            return followComponent;
        }

        /// <summary>
        /// Registers this follow component for its viewport camera when attached.
        /// </summary>
        /// <param name="entity">Owning gizmo root entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            FollowComponentByCamera[SceneCamera] = this;
        }

        /// <summary>
        /// Removes this follow component from the camera registry when detached.
        /// </summary>
        /// <param name="entity">Owning gizmo root entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);

            if (FollowComponentByCamera.TryGetValue(SceneCamera, out TransformTranslationGizmoFollowComponent followComponent) &&
                ReferenceEquals(followComponent, this)) {
                FollowComponentByCamera.Remove(SceneCamera);
            }
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

            float3 selectedPosition = helengine.editor.EditorViewportDirect2DPresentationService.ResolvePresentedWorldAnchorPosition(selectedEntity);
            GizmoRoot.Orientation = float4.Identity;
            GizmoRoot.Position = selectedPosition;
            SetAxisVisualState(true);

            Entity cameraEntity = SceneCamera.Parent;
            if (cameraEntity == null) {
                throw new InvalidOperationException("Scene camera must belong to an entity.");
            }

            bool isDragging = EditorGizmoDragService.IsDragging(SceneCamera);
            EnsureHandleBaseTransformsCached();
            if (!isDragging) {
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
                float4 yawFacingOrientation = TransformGizmoYawSnapper.ComputeSnappedYawFacingOrientation(selectedPosition, cameraEntity.Position);
                ApplyFacingToHandles(yawFacingOrientation, scale);
                UpdateAxisTipOffsets(scale);
            }

            UpdateAxisHighlightMaterials();
            UpdateSnapPreview(selectedPosition, cameraEntity.Position);
        }

        /// <summary>
        /// Enables or disables all gizmo axis entities and their mesh children.
        /// </summary>
        /// <param name="enabled">True to render axis visuals; false to hide them.</param>
        void SetAxisVisualState(bool enabled) {
            for (int axisIndex = 0; axisIndex < GizmoRoot.Children.Count; axisIndex++) {
                Entity axis = GizmoRoot.Children[axisIndex];
                if (axis == null || !IsHandleEntity(axis)) {
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

            if (!enabled) {
                SetSnapPreviewVisible(false);
            }
        }

        /// <summary>
        /// Applies highlight material state based on the currently hovered gizmo handle.
        /// </summary>
        void UpdateAxisHighlightMaterials() {
            Entity hoveredAxis = EditorGizmoHoverService.HoveredHandleEntity;
            for (int axisIndex = 0; axisIndex < GizmoRoot.Children.Count; axisIndex++) {
                if (GizmoRoot.Children[axisIndex] is not EditorEntity axisEntity || !IsHandleEntity(axisEntity)) {
                    continue;
                }

                bool isHoveredAxis = hoveredAxis != null && ReferenceEquals(axisEntity, hoveredAxis);
                RuntimeMaterial material = ResolveHandleMaterial(axisEntity, isHoveredAxis);
                ApplyAxisMaterial(axisEntity, material);
            }
        }

        /// <summary>
        /// Resolves the correct material for one handle entity based on its constraint type and hover state.
        /// </summary>
        /// <param name="handleEntity">Handle entity to inspect.</param>
        /// <param name="isHovered">True when the handle is currently hovered.</param>
        /// <returns>Material that should be applied to the handle visuals.</returns>
        RuntimeMaterial ResolveHandleMaterial(EditorEntity handleEntity, bool isHovered) {
            if (handleEntity == null) {
                throw new ArgumentNullException(nameof(handleEntity));
            }

            if (!TryFindTransformHandleComponent(handleEntity, out TransformGizmoHandleComponent handleComponent)) {
                throw new InvalidOperationException("Translation gizmo handle entity must expose a transform handle component.");
            }

            if (handleComponent.ConstraintType == TransformGizmoHandleConstraintType.Plane) {
                return isHovered ? HighlightPlaneMaterial : NormalPlaneMaterial;
            }

            return isHovered ? HighlightAxisMaterial : NormalAxisMaterial;
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
            if (selfMesh != null && (selfMesh.Materials.Length == 0 || !ReferenceEquals(selfMesh.Materials[0], material))) {
                selfMesh.Materials = new[] { material };
            }

            for (int childIndex = 0; childIndex < axisEntity.Children.Count; childIndex++) {
                if (axisEntity.Children[childIndex] is not Entity childEntity) {
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
                if (GizmoRoot.Children[axisIndex] is not EditorEntity axisEntity || !IsHandleEntity(axisEntity)) {
                    continue;
                }

                float3 tipOffset = ResolveAxisTipOffset(axisEntity, axisOffset);
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
        /// Resolves the world-axis tip offset for a translation handle entity.
        /// </summary>
        /// <param name="axisEntity">Axis handle entity.</param>
        /// <param name="axisOffset">Offset magnitude from origin to shaft end.</param>
        /// <returns>Tip offset vector for the given axis.</returns>
        float3 ResolveAxisTipOffset(Entity axisEntity, float axisOffset) {
            if (axisEntity == null) {
                throw new ArgumentNullException(nameof(axisEntity));
            }

            float3 primaryDirection = ResolveHandleLocalPrimaryDirection(axisEntity);
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
                if (handle == null || !IsHandleEntity(handle)) {
                    continue;
                }

                float3 baseLocalPosition = handle.LocalPosition;
                BaseHandlePositions[handle] = baseLocalPosition;
                BaseHandleOrientations[handle] = handle.LocalOrientation;
            }

            HandleBaseTransformsCached = true;
        }

        /// <summary>
        /// Applies snapped yaw facing transforms to direct gizmo-handle children.
        /// </summary>
        /// <param name="yawFacingOrientation">Quaternion representing the snapped world-space Y-axis yaw orientation.</param>
        /// <param name="scale">Current gizmo world scale used to keep plane-handle offsets aligned with the scaled gizmo.</param>
        void ApplyFacingToHandles(float4 yawFacingOrientation, float scale) {
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

                float3 resolvedBasePosition = basePosition;
                if (TryFindTransformHandleComponent(handle, out TransformGizmoHandleComponent handleComponent) &&
                    handleComponent.ConstraintType == TransformGizmoHandleConstraintType.Plane) {
                    resolvedBasePosition *= scale;
                }

                handle.LocalPosition = float4.RotateVector(resolvedBasePosition, yawFacingOrientation);
                handle.LocalOrientation = yawFacingOrientation * baseOrientation;
            }
        }

        /// <summary>
        /// Resolves the local-space primary direction for a transform gizmo handle.
        /// </summary>
        /// <param name="axisEntity">Handle entity to evaluate.</param>
        /// <returns>Normalized local-space primary direction, or zero when unavailable.</returns>
        float3 ResolveHandleLocalPrimaryDirection(Entity axisEntity) {
            if (axisEntity == null) {
                throw new ArgumentNullException(nameof(axisEntity));
            }

            if (!TryFindTransformHandleComponent(axisEntity, out TransformGizmoHandleComponent handleComponent)) {
                return float3.Zero;
            }

            return NormalizeDirection(handleComponent.LocalPrimaryDirection);
        }

        /// <summary>
        /// Updates the reusable snap-preview entity from the active modifier keys and hovered translation handle.
        /// </summary>
        /// <param name="selectedPosition">World-space gizmo origin at the selected entity.</param>
        /// <param name="cameraPosition">World-space camera position used for axis-plane orientation.</param>
        void UpdateSnapPreview(float3 selectedPosition, float3 cameraPosition) {
            InputSystem input = Core.Instance.Input;
            if (input == null) {
                SetSnapPreviewVisible(false);
                return;
            }

            double activeSnapValue = TransformGizmoActiveSnapValueResolver.ResolveActiveSnapValue(input, EditorViewportToolMode.Translate);
            if (activeSnapValue <= 0.0) {
                SetSnapPreviewVisible(false);
                return;
            }

            Entity hoveredHandle = EditorGizmoHoverService.HoveredHandleEntity;
            if (hoveredHandle == null || !IsOwnedHandleEntity(hoveredHandle)) {
                SetSnapPreviewVisible(false);
                return;
            }

            float currentScale = CurrentScale;
            if (currentScale <= 0f) {
                SetSnapPreviewVisible(false);
                return;
            }

            if (!TransformTranslationSnapPreviewResolver.TryResolvePreviewOrientation(
                hoveredHandle,
                selectedPosition,
                cameraPosition,
                out float4 previewOrientation)) {
                SetSnapPreviewVisible(false);
                return;
            }

            float desiredWorldGridScale = (float)activeSnapValue;
            if (desiredWorldGridScale <= 0f) {
                SetSnapPreviewVisible(false);
                return;
            }

            float localGridScale = desiredWorldGridScale / currentScale;
            ConfigureSnapPreviewMaterial(hoveredHandle);
            SnapPreviewEntity.Position = float3.Zero;
            SnapPreviewEntity.Orientation = previewOrientation;
            SnapPreviewEntity.Scale = new float3(localGridScale, localGridScale, localGridScale);
            SetSnapPreviewVisible(true);
        }

        /// <summary>
        /// Updates the snap-preview material parameters so single-axis previews emphasize the dragged axis and plane previews keep the full grid.
        /// </summary>
        /// <param name="hoveredHandle">Hovered translation handle that determines the preview mode.</param>
        void ConfigureSnapPreviewMaterial(Entity hoveredHandle) {
            if (hoveredHandle == null) {
                throw new ArgumentNullException(nameof(hoveredHandle));
            }

            if (SnapPreviewMesh.Materials.Length == 0) {
                throw new InvalidOperationException("Translation snap-preview mesh must include a material.");
            }

            if (!TryFindTransformHandleComponent(hoveredHandle, out TransformGizmoHandleComponent handleComponent)) {
                throw new InvalidOperationException("Translation snap-preview requires a hovered gizmo handle.");
            }

            if (handleComponent.ConstraintType == TransformGizmoHandleConstraintType.Axis) {
                TransformGizmoGridPreviewParameters.ApplySingleAxisFocus(SnapPreviewMesh.Materials[0]);
                return;
            }

            if (handleComponent.ConstraintType == TransformGizmoHandleConstraintType.Plane) {
                TransformGizmoGridPreviewParameters.ApplyFullGrid(SnapPreviewMesh.Materials[0]);
                return;
            }

            throw new InvalidOperationException("Transform gizmo handle constraint type is not supported.");
        }

        /// <summary>
        /// Enables or disables the reusable snap-preview entity.
        /// </summary>
        /// <param name="visible">True to render the preview grid; false to hide it.</param>
        void SetSnapPreviewVisible(bool visible) {
            if (SnapPreviewEntity == null) {
                return;
            }

            SnapPreviewEntity.Enabled = visible;
        }

        /// <summary>
        /// Determines whether the supplied entity belongs to this gizmo's direct-handle set.
        /// </summary>
        /// <param name="handleEntity">Entity to test.</param>
        /// <returns>True when the entity is a direct translation handle owned by this gizmo.</returns>
        bool IsOwnedHandleEntity(Entity handleEntity) {
            if (handleEntity == null) {
                throw new ArgumentNullException(nameof(handleEntity));
            }

            if (GizmoRoot.Children == null) {
                return false;
            }

            for (int childIndex = 0; childIndex < GizmoRoot.Children.Count; childIndex++) {
                Entity childEntity = GizmoRoot.Children[childIndex];
                if (childEntity == null || !ReferenceEquals(childEntity, handleEntity)) {
                    continue;
                }

                return IsHandleEntity(childEntity);
            }

            return false;
        }

        /// <summary>
        /// Determines whether the supplied entity is one of the translation gizmo's drag handles.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>True when the entity exposes a transform-gizmo handle component; otherwise false.</returns>
        bool IsHandleEntity(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            return TryFindTransformHandleComponent(entity, out TransformGizmoHandleComponent _);
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


