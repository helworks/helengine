namespace helengine {
    /// <summary>
    /// Creates, updates, and removes editor-only world-space border gizmos for authored viewport entities.
    /// </summary>
    public sealed class EditorViewportBorderGizmoSyncComponent : UpdateComponent, IEditorHiddenComponent {
        /// <summary>
        /// Owned gizmo entities keyed by the authored entity that owns each mirrored viewport component.
        /// </summary>
        readonly Dictionary<Entity, EditorEntity> OwnedGizmoEntitiesBySourceEntity;

        /// <summary>
        /// Initializes one authored viewport border gizmo synchronizer.
        /// </summary>
        public EditorViewportBorderGizmoSyncComponent() {
            OwnedGizmoEntitiesBySourceEntity = new Dictionary<Entity, EditorEntity>();
        }

        /// <summary>
        /// Synchronizes authored viewport border gizmos every frame.
        /// </summary>
        public override void Update() {
            base.Update();
            SynchronizeCurrentViewportEntities();
            RemoveStaleGizmoEntities();
        }

        /// <summary>
        /// Disposes all owned gizmo entities when the synchronizer leaves the scene.
        /// </summary>
        /// <param name="entity">Entity that hosted this synchronizer.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DisposeOwnedGizmoEntities();
        }

        /// <summary>
        /// Synchronizes gizmos for the current set of authored viewport entities.
        /// </summary>
        void SynchronizeCurrentViewportEntities() {
            Entity[] entitySnapshot = CreateEntitySnapshot();
            for (int index = 0; index < entitySnapshot.Length; index++) {
                Entity sourceEntity = entitySnapshot[index];
                if (!TryResolvePreviewableViewportComponent(sourceEntity, out ViewportComponent sourceViewportComponent)) {
                    continue;
                }

                EditorEntity gizmoEntity = EnsureGizmoEntity(sourceEntity, sourceViewportComponent);
                EditorViewportBorderGizmoComponent gizmoComponent = ResolveGizmoComponent(gizmoEntity);
                gizmoComponent.SynchronizeFromSource();
            }
        }

        /// <summary>
        /// Removes owned gizmo entities whose authored viewport sources no longer qualify for preview.
        /// </summary>
        void RemoveStaleGizmoEntities() {
            Entity[] sourceSnapshot = CreateOwnedSourceSnapshot();
            for (int index = 0; index < sourceSnapshot.Length; index++) {
                Entity sourceEntity = sourceSnapshot[index];
                if (IsSourceEntityStillPreviewable(sourceEntity)) {
                    continue;
                }

                DisposeOwnedGizmoEntity(sourceEntity);
            }
        }

        /// <summary>
        /// Creates or resolves the border gizmo entity associated with one authored viewport entity.
        /// </summary>
        /// <param name="sourceEntity">Authored entity that owns the viewport component.</param>
        /// <param name="sourceViewportComponent">Viewport component mirrored by the gizmo.</param>
        /// <returns>Resolved editor-only gizmo entity.</returns>
        EditorEntity EnsureGizmoEntity(Entity sourceEntity, ViewportComponent sourceViewportComponent) {
            if (OwnedGizmoEntitiesBySourceEntity.TryGetValue(sourceEntity, out EditorEntity gizmoEntity)) {
                return gizmoEntity;
            }

            gizmoEntity = CreateGizmoEntity(sourceEntity, sourceViewportComponent);
            OwnedGizmoEntitiesBySourceEntity[sourceEntity] = gizmoEntity;
            return gizmoEntity;
        }

        /// <summary>
        /// Creates one internal editor entity that renders the border gizmo for the supplied viewport component.
        /// </summary>
        /// <param name="sourceEntity">Authored entity that owns the viewport component.</param>
        /// <param name="sourceViewportComponent">Viewport component mirrored by the gizmo.</param>
        /// <returns>Newly created gizmo entity.</returns>
        EditorEntity CreateGizmoEntity(Entity sourceEntity, ViewportComponent sourceViewportComponent) {
            EditorEntity gizmoEntity = new EditorEntity {
                Name = "Viewport Border Gizmo",
                InternalEntity = true,
                LayerMask = helengine.editor.EditorLayerMasks.SceneObjects
            };
            gizmoEntity.AddComponent(new EditorViewportBorderGizmoComponent(sourceEntity, sourceViewportComponent));
            return gizmoEntity;
        }

        /// <summary>
        /// Resolves the gizmo component attached to the supplied gizmo entity.
        /// </summary>
        /// <param name="gizmoEntity">Gizmo entity whose border component should be resolved.</param>
        /// <returns>Typed border gizmo component.</returns>
        EditorViewportBorderGizmoComponent ResolveGizmoComponent(EditorEntity gizmoEntity) {
            for (int index = 0; index < gizmoEntity.Components.Count; index++) {
                if (gizmoEntity.Components[index] is EditorViewportBorderGizmoComponent gizmoComponent) {
                    return gizmoComponent;
                }
            }

            throw new InvalidOperationException("Viewport border gizmo entities must contain an EditorViewportBorderGizmoComponent.");
        }

        /// <summary>
        /// Attempts to resolve the authored viewport component that should receive a border gizmo.
        /// </summary>
        /// <param name="entity">Authored entity candidate.</param>
        /// <param name="viewportComponent">Resolved viewport component when present.</param>
        /// <returns>True when the entity owns an authored viewport component that should receive a gizmo.</returns>
        bool TryResolvePreviewableViewportComponent(Entity entity, out ViewportComponent viewportComponent) {
            viewportComponent = null;
            if (entity == null) {
                return false;
            } else if (!helengine.editor.EditorViewportSceneSelectionFilter.ShouldSelectEntity(entity)) {
                return false;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is ViewportComponent candidateViewportComponent) {
                    viewportComponent = candidateViewportComponent;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether one authored source entity still qualifies for a viewport border gizmo.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity to evaluate.</param>
        /// <returns>True when the source entity still owns an authored viewport component.</returns>
        bool IsSourceEntityStillPreviewable(Entity sourceEntity) {
            if (sourceEntity == null) {
                return false;
            } else if (!Core.Instance.ObjectManager.Entities.Contains(sourceEntity)) {
                return false;
            }

            return TryResolvePreviewableViewportComponent(sourceEntity, out _);
        }

        /// <summary>
        /// Disposes one owned gizmo entity and clears the corresponding mapping.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity whose gizmo should be disposed.</param>
        void DisposeOwnedGizmoEntity(Entity sourceEntity) {
            if (!OwnedGizmoEntitiesBySourceEntity.TryGetValue(sourceEntity, out EditorEntity gizmoEntity)) {
                return;
            }

            OwnedGizmoEntitiesBySourceEntity.Remove(sourceEntity);
            gizmoEntity.Dispose();
        }

        /// <summary>
        /// Disposes all currently owned gizmo entities.
        /// </summary>
        void DisposeOwnedGizmoEntities() {
            Entity[] sourceSnapshot = CreateOwnedSourceSnapshot();
            for (int index = 0; index < sourceSnapshot.Length; index++) {
                DisposeOwnedGizmoEntity(sourceSnapshot[index]);
            }
        }

        /// <summary>
        /// Creates one snapshot of the current entity list so synchronization can iterate safely.
        /// </summary>
        /// <returns>Snapshot of the current object-manager entities.</returns>
        Entity[] CreateEntitySnapshot() {
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            Entity[] snapshot = new Entity[entities.Count];
            entities.CopyTo(snapshot);
            return snapshot;
        }

        /// <summary>
        /// Creates one snapshot of the authored source entities currently owned by this synchronizer.
        /// </summary>
        /// <returns>Snapshot of authored source entities that currently own gizmos.</returns>
        Entity[] CreateOwnedSourceSnapshot() {
            Entity[] snapshot = new Entity[OwnedGizmoEntitiesBySourceEntity.Count];
            OwnedGizmoEntitiesBySourceEntity.Keys.CopyTo(snapshot, 0);
            return snapshot;
        }
    }
}
