namespace helengine.editor {
    /// <summary>
    /// Keeps the rotation gizmo aligned to the currently selected entity.
    /// </summary>
    public class TransformRotationGizmoFollowComponent : UpdateComponent {
        /// <summary>
        /// Perspective vertical field of view used by the 3D renderer.
        /// </summary>
        const double PerspectiveVerticalFieldOfViewRadians = Math.PI / 4.0;
        /// <summary>
        /// Fraction of viewport height the rotation gizmo should occupy on screen.
        /// </summary>
        const double TargetViewportHeightFraction = 0.44;
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
        /// Scene camera used to compute distance-based gizmo scaling.
        /// </summary>
        readonly CameraComponent SceneCamera;
        /// <summary>
        /// Renderer used to build cached preview models for active snap values.
        /// </summary>
        readonly RenderManager3D Render3D;
        /// <summary>
        /// Root entity that owns all rotation gizmo meshes.
        /// </summary>
        readonly EditorEntity GizmoRoot;
        /// <summary>
        /// Material used when a ring is not hovered.
        /// </summary>
        readonly RuntimeMaterial NormalAxisMaterial;
        /// <summary>
        /// Material used when a ring is hovered.
        /// </summary>
        readonly RuntimeMaterial HighlightAxisMaterial;
        /// <summary>
        /// Reusable preview entity that visualizes active rotation snapping.
        /// </summary>
        readonly EditorEntity SnapPreviewEntity;
        /// <summary>
        /// Mesh component used by the reusable rotation snap-preview entity.
        /// </summary>
        readonly MeshComponent SnapPreviewMesh;
        /// <summary>
        /// Cached runtime preview models keyed by their encoded snap angle in degrees.
        /// </summary>
        readonly Dictionary<double, RuntimeModel> PreviewModelsBySnapDegrees;

        /// <summary>
        /// Initializes a new rotation gizmo follow component.
        /// </summary>
        /// <param name="sceneCamera">Scene camera that views the gizmo.</param>
        /// <param name="render3D">Renderer used to build cached preview meshes.</param>
        /// <param name="gizmoRoot">Root entity for the rotation gizmo.</param>
        /// <param name="normalAxisMaterial">Material used for non-hovered axis visuals.</param>
        /// <param name="highlightAxisMaterial">Material used for hovered axis visuals.</param>
        /// <param name="snapPreviewEntity">Reusable disc-preview entity shown while snap modifiers are held.</param>
        public TransformRotationGizmoFollowComponent(
            CameraComponent sceneCamera,
            RenderManager3D render3D,
            EditorEntity gizmoRoot,
            RuntimeMaterial normalAxisMaterial,
            RuntimeMaterial highlightAxisMaterial,
            EditorEntity snapPreviewEntity) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            Render3D = render3D ?? throw new ArgumentNullException(nameof(render3D));
            GizmoRoot = gizmoRoot ?? throw new ArgumentNullException(nameof(gizmoRoot));
            NormalAxisMaterial = normalAxisMaterial ?? throw new ArgumentNullException(nameof(normalAxisMaterial));
            HighlightAxisMaterial = highlightAxisMaterial ?? throw new ArgumentNullException(nameof(highlightAxisMaterial));
            SnapPreviewEntity = snapPreviewEntity ?? throw new ArgumentNullException(nameof(snapPreviewEntity));
            SnapPreviewMesh = FindMeshComponent(snapPreviewEntity) ?? throw new InvalidOperationException("Rotation snap-preview entity must include a mesh component.");
            PreviewModelsBySnapDegrees = new Dictionary<double, RuntimeModel>();
        }

        /// <summary>
        /// Updates gizmo visibility, position, and scale from the current editor selection.
        /// </summary>
        public override void Update() {
            if (!IsRotateToolActive()) {
                SetHandleVisualState(false);
                return;
            }

            Entity selectedEntity = EditorSelectionService.SelectedEntity;
            if (!ShouldDisplayForSelection(selectedEntity)) {
                SetHandleVisualState(false);
                return;
            }

            Entity cameraEntity = SceneCamera.Parent;
            if (cameraEntity == null) {
                throw new InvalidOperationException("Scene camera must belong to an entity.");
            }

            GizmoRoot.Position = selectedEntity.Position;
            GizmoRoot.Orientation = float4.Identity;
            SetHandleVisualState(true);

            if (!EditorGizmoDragService.IsDragging(SceneCamera)) {
                double scaleValue = ComputeScaleForTargetPixels(selectedEntity.Position, cameraEntity.Position);
                if (scaleValue < MinimumScale) {
                    scaleValue = MinimumScale;
                }
                if (scaleValue > MaximumScale) {
                    scaleValue = MaximumScale;
                }

                float scale = (float)scaleValue;
                GizmoRoot.Scale = new float3(scale, scale, scale);
            }

            UpdateAxisHighlightMaterials();
            UpdateSnapPreview();
        }

        /// <summary>
        /// Enables or disables all rotation ring entities.
        /// </summary>
        /// <param name="enabled">True to render ring visuals; false to hide them.</param>
        void SetHandleVisualState(bool enabled) {
            for (int ringIndex = 0; ringIndex < GizmoRoot.Children.Count; ringIndex++) {
                Entity ringEntity = GizmoRoot.Children[ringIndex];
                if (ringEntity == null || !IsHandleEntity(ringEntity)) {
                    continue;
                }

                ringEntity.Enabled = enabled;
            }

            if (!enabled) {
                SetSnapPreviewVisible(false);
            }
        }

        /// <summary>
        /// Applies highlight material state based on the currently hovered rotation ring.
        /// </summary>
        void UpdateAxisHighlightMaterials() {
            Entity hoveredAxis = EditorGizmoHoverService.HoveredHandleEntity;
            for (int ringIndex = 0; ringIndex < GizmoRoot.Children.Count; ringIndex++) {
                if (GizmoRoot.Children[ringIndex] is not EditorEntity ringEntity || !IsHandleEntity(ringEntity)) {
                    continue;
                }

                bool isHoveredAxis = hoveredAxis != null && ReferenceEquals(ringEntity, hoveredAxis);
                RuntimeMaterial material = isHoveredAxis ? HighlightAxisMaterial : NormalAxisMaterial;
                ApplyAxisMaterial(ringEntity, material);
            }
        }

        /// <summary>
        /// Applies one material to the ring mesh on the supplied entity.
        /// </summary>
        /// <param name="ringEntity">Ring entity whose mesh material should be updated.</param>
        /// <param name="material">Material to apply.</param>
        void ApplyAxisMaterial(EditorEntity ringEntity, RuntimeMaterial material) {
            if (ringEntity == null) {
                throw new ArgumentNullException(nameof(ringEntity));
            }

            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            MeshComponent meshComponent = FindMeshComponent(ringEntity);
            if (meshComponent != null && !ReferenceEquals(meshComponent.Material, material)) {
                meshComponent.Material = material;
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
        /// Determines whether the rotation gizmo should be shown for the provided selection.
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
        /// Computes gizmo scale so the ring diameter stays at a constant on-screen pixel height fraction.
        /// </summary>
        /// <param name="origin">Gizmo origin in world space.</param>
        /// <param name="cameraPosition">Scene camera world position.</param>
        /// <returns>Scale factor applied to gizmo meshes.</returns>
        double ComputeScaleForTargetPixels(float3 origin, float3 cameraPosition) {
            float4 viewport = SceneCamera.Viewport;
            double viewportHeight = viewport.W;
            if (viewportHeight <= 0.0) {
                throw new InvalidOperationException("Scene camera viewport height must be greater than zero.");
            }

            if (TransformRotationGizmoFactory.OuterDiameter <= 0f) {
                throw new InvalidOperationException("Rotation gizmo outer diameter must be greater than zero.");
            }

            double targetDiameterPixels = viewportHeight * TargetViewportHeightFraction;
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

            double targetWorldDiameter = targetDiameterPixels * (2.0 * distance * tanHalfFov) / viewportHeight;
            return targetWorldDiameter / TransformRotationGizmoFactory.OuterDiameter;
        }

        /// <summary>
        /// Determines whether rotation gizmos should be active for the current viewport camera.
        /// </summary>
        /// <returns>True when the viewport tool mode is rotation.</returns>
        bool IsRotateToolActive() {
            return EditorViewportToolService.GetToolMode(SceneCamera) == EditorViewportToolMode.Rotate;
        }

        /// <summary>
        /// Updates the reusable snap-preview entity from the active modifier keys and hovered rotation ring.
        /// </summary>
        void UpdateSnapPreview() {
            InputSystem input = Core.Instance.Input;
            if (input == null) {
                SetSnapPreviewVisible(false);
                return;
            }

            double activeSnapValue = TransformGizmoActiveSnapValueResolver.ResolveActiveSnapValue(input, EditorViewportToolMode.Rotate);
            if (activeSnapValue <= 0.0) {
                SetSnapPreviewVisible(false);
                return;
            }

            Entity hoveredHandle = EditorGizmoHoverService.HoveredHandleEntity;
            if (hoveredHandle == null || !IsOwnedHandleEntity(hoveredHandle)) {
                SetSnapPreviewVisible(false);
                return;
            }

            if (!TransformRotationSnapPreviewResolver.TryResolvePreviewOrientation(hoveredHandle, out float4 previewOrientation)) {
                SetSnapPreviewVisible(false);
                return;
            }

            RuntimeModel previewModel = ResolvePreviewModel(activeSnapValue);
            if (!ReferenceEquals(SnapPreviewMesh.Model, previewModel)) {
                SnapPreviewMesh.Model = previewModel;
            }

            SnapPreviewEntity.Position = float3.Zero;
            SnapPreviewEntity.Orientation = previewOrientation;
            SnapPreviewEntity.Scale = float3.Zero;
            SetSnapPreviewVisible(true);
        }

        /// <summary>
        /// Resolves the cached runtime preview model for the supplied snap value, building it on first use.
        /// </summary>
        /// <param name="snapDegrees">Angular snap interval in degrees.</param>
        /// <returns>Runtime preview model for the requested snap interval.</returns>
        RuntimeModel ResolvePreviewModel(double snapDegrees) {
            if (snapDegrees <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(snapDegrees), "Snap value must be greater than zero.");
            }

            if (PreviewModelsBySnapDegrees.TryGetValue(snapDegrees, out RuntimeModel previewModel)) {
                return previewModel;
            }

            previewModel = Render3D.BuildModelFromRaw(TransformRotationSnapPreviewModelFactory.Create(snapDegrees));
            PreviewModelsBySnapDegrees[snapDegrees] = previewModel;
            return previewModel;
        }

        /// <summary>
        /// Enables or disables the reusable snap-preview entity.
        /// </summary>
        /// <param name="visible">True to render the preview disc; false to hide it.</param>
        void SetSnapPreviewVisible(bool visible) {
            SnapPreviewEntity.Enabled = visible;
        }

        /// <summary>
        /// Determines whether the supplied entity belongs to this gizmo's direct-handle set.
        /// </summary>
        /// <param name="handleEntity">Entity to test.</param>
        /// <returns>True when the entity is a direct rotation handle owned by this gizmo.</returns>
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
        /// Determines whether the supplied entity is one of the rotation gizmo's drag handles.
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
    }
}


