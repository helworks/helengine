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
        /// Initializes a new rotation gizmo follow component.
        /// </summary>
        /// <param name="sceneCamera">Scene camera that views the gizmo.</param>
        /// <param name="gizmoRoot">Root entity for the rotation gizmo.</param>
        /// <param name="normalAxisMaterial">Material used for non-hovered axis visuals.</param>
        /// <param name="highlightAxisMaterial">Material used for hovered axis visuals.</param>
        public TransformRotationGizmoFollowComponent(
            CameraComponent sceneCamera,
            EditorEntity gizmoRoot,
            RuntimeMaterial normalAxisMaterial,
            RuntimeMaterial highlightAxisMaterial) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            GizmoRoot = gizmoRoot ?? throw new ArgumentNullException(nameof(gizmoRoot));
            NormalAxisMaterial = normalAxisMaterial ?? throw new ArgumentNullException(nameof(normalAxisMaterial));
            HighlightAxisMaterial = highlightAxisMaterial ?? throw new ArgumentNullException(nameof(highlightAxisMaterial));
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
        }

        /// <summary>
        /// Enables or disables all rotation ring entities.
        /// </summary>
        /// <param name="enabled">True to render ring visuals; false to hide them.</param>
        void SetHandleVisualState(bool enabled) {
            for (int ringIndex = 0; ringIndex < GizmoRoot.Children.Count; ringIndex++) {
                Entity ringEntity = GizmoRoot.Children[ringIndex];
                if (ringEntity == null) {
                    continue;
                }

                ringEntity.Enabled = enabled;
            }
        }

        /// <summary>
        /// Applies highlight material state based on the currently hovered rotation ring.
        /// </summary>
        void UpdateAxisHighlightMaterials() {
            Entity hoveredAxis = EditorGizmoHoverService.HoveredHandleEntity;
            for (int ringIndex = 0; ringIndex < GizmoRoot.Children.Count; ringIndex++) {
                if (GizmoRoot.Children[ringIndex] is not EditorEntity ringEntity) {
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
    }
}
