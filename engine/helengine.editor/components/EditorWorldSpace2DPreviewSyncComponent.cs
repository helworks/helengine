namespace helengine {
    /// <summary>
    /// Creates, updates, and removes the editor-only world-space 2D preview proxies needed by the active scene.
    /// </summary>
    public sealed class EditorWorldSpace2DPreviewSyncComponent : UpdateComponent, IEditorHiddenComponent {
        /// <summary>
        /// Preview proxies currently created by this synchronizer, keyed by authored source entity.
        /// </summary>
        readonly Dictionary<Entity, EditorEntity> OwnedPreviewEntitiesBySourceEntity;

        /// <summary>
        /// Initializes one world-space preview synchronizer.
        /// </summary>
        public EditorWorldSpace2DPreviewSyncComponent() {
            OwnedPreviewEntitiesBySourceEntity = new Dictionary<Entity, EditorEntity>();
        }

        /// <summary>
        /// Synchronizes preview proxies for all supported authored 2D scene entities.
        /// </summary>
        public override void Update() {
            base.Update();
            SynchronizeCurrentSceneEntities();
            RemoveStalePreviewEntities();
        }

        /// <summary>
        /// Disposes all preview proxies created by this synchronizer when it leaves the scene.
        /// </summary>
        /// <param name="entity">Owning entity that hosted the synchronizer.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DisposeOwnedPreviewEntities();
        }

        /// <summary>
        /// Synchronizes previews for the current set of supported authored scene entities.
        /// </summary>
        void SynchronizeCurrentSceneEntities() {
            Entity[] entitySnapshot = CreateEntitySnapshot();
            for (int index = 0; index < entitySnapshot.Length; index++) {
                Entity sourceEntity = entitySnapshot[index];
                if (!helengine.editor.EditorWorldSpace2DPreviewMapper.TryResolveSupportedSourceComponent(sourceEntity, out Component sourceComponent)) {
                    continue;
                } else if (helengine.editor.EditorViewportDirect2DPresentationService.ShouldKeepViewportLockBehavior(sourceEntity)) {
                    continue;
                }

                EditorEntity previewEntity = EnsurePreviewEntity(sourceEntity, sourceComponent);
                EditorWorldSpace2DPreviewComponentBase previewComponent = ResolvePreviewComponent(previewEntity);
                previewComponent.SynchronizeFromSource();
            }
        }

        /// <summary>
        /// Removes preview proxies whose authored source entity disappeared or is no longer preview-supported.
        /// </summary>
        void RemoveStalePreviewEntities() {
            Entity[] ownedSources = CreateOwnedSourceSnapshot();
            for (int index = 0; index < ownedSources.Length; index++) {
                Entity sourceEntity = ownedSources[index];
                if (IsSourceEntityStillPreviewable(sourceEntity)) {
                    continue;
                }

                DisposeOwnedPreviewEntity(sourceEntity);
            }
        }

        /// <summary>
        /// Creates or resolves the preview entity associated with one authored source entity.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity that requires a preview.</param>
        /// <param name="sourceComponent">Supported 2D component mirrored by the preview.</param>
        /// <returns>Resolved preview entity for the supplied source entity.</returns>
        EditorEntity EnsurePreviewEntity(Entity sourceEntity, Component sourceComponent) {
            EditorEntity previewEntity = helengine.editor.EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity);
            if (previewEntity != null) {
                if (!OwnedPreviewEntitiesBySourceEntity.ContainsKey(sourceEntity)) {
                    OwnedPreviewEntitiesBySourceEntity.Add(sourceEntity, previewEntity);
                }

                return previewEntity;
            }

            previewEntity = CreatePreviewEntity(sourceEntity, sourceComponent);
            OwnedPreviewEntitiesBySourceEntity[sourceEntity] = previewEntity;
            helengine.editor.EditorWorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);
            return previewEntity;
        }

        /// <summary>
        /// Creates one internal preview proxy entity for the supplied authored source entity.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity mirrored by the preview.</param>
        /// <param name="sourceComponent">Supported 2D component mirrored by the preview.</param>
        /// <returns>Newly created internal preview proxy entity.</returns>
        EditorEntity CreatePreviewEntity(Entity sourceEntity, Component sourceComponent) {
            EditorEntity previewEntity = new EditorEntity {
                Name = "World Space 2D Preview",
                InternalEntity = true,
                LayerMask = helengine.editor.EditorLayerMasks.SceneObjects
            };
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, sourceComponent));
            previewEntity.AddComponent(CreatePreviewComponent(sourceEntity, sourceComponent));
            return previewEntity;
        }

        /// <summary>
        /// Creates the typed preview component required for the supplied authored source component.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity mirrored by the preview.</param>
        /// <param name="sourceComponent">Supported 2D component mirrored by the preview.</param>
        /// <returns>Typed preview component ready to attach to the preview entity.</returns>
        EditorWorldSpace2DPreviewComponentBase CreatePreviewComponent(Entity sourceEntity, Component sourceComponent) {
            if (sourceComponent is SpriteComponent spriteComponent) {
                return new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent);
            } else if (sourceComponent is TextComponent textComponent) {
                return new EditorTextWorldPreviewComponent(sourceEntity, textComponent);
            } else if (sourceComponent is RoundedRectComponent roundedRectComponent) {
                return new EditorRoundedRectWorldPreviewComponent(sourceEntity, roundedRectComponent);
            }

            throw new InvalidOperationException($"Unsupported 2D preview source component type '{sourceComponent.GetType().FullName}'.");
        }

        /// <summary>
        /// Resolves the typed preview component attached to one preview entity.
        /// </summary>
        /// <param name="previewEntity">Preview entity whose rendering component should be resolved.</param>
        /// <returns>Typed world-space preview component attached to the entity.</returns>
        EditorWorldSpace2DPreviewComponentBase ResolvePreviewComponent(EditorEntity previewEntity) {
            for (int componentIndex = 0; componentIndex < previewEntity.Components.Count; componentIndex++) {
                if (previewEntity.Components[componentIndex] is EditorWorldSpace2DPreviewComponentBase previewComponent) {
                    return previewComponent;
                }
            }

            throw new InvalidOperationException("World-space 2D preview entities must contain one preview rendering component.");
        }

        /// <summary>
        /// Determines whether one authored source entity still exists and still participates in world-space preview synchronization.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity to evaluate.</param>
        /// <returns>True when the entity remains registered and still exposes a supported preview component.</returns>
        bool IsSourceEntityStillPreviewable(Entity sourceEntity) {
            if (sourceEntity == null || Core.Instance == null || Core.Instance.ObjectManager == null) {
                return false;
            }

            if (!Core.Instance.ObjectManager.Entities.Contains(sourceEntity)) {
                return false;
            }

            if (!helengine.editor.EditorWorldSpace2DPreviewMapper.TryResolveSupportedSourceComponent(sourceEntity, out _)) {
                return false;
            }

            return !helengine.editor.EditorViewportDirect2DPresentationService.ShouldKeepViewportLockBehavior(sourceEntity);
        }

        /// <summary>
        /// Disposes one preview entity owned by this synchronizer and clears its registry mapping.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity whose preview should be removed.</param>
        void DisposeOwnedPreviewEntity(Entity sourceEntity) {
            if (!OwnedPreviewEntitiesBySourceEntity.TryGetValue(sourceEntity, out EditorEntity previewEntity)) {
                return;
            }

            OwnedPreviewEntitiesBySourceEntity.Remove(sourceEntity);
            helengine.editor.EditorWorldSpace2DPreviewRegistry.RemoveBySourceEntity(sourceEntity);
            previewEntity.Dispose();
        }

        /// <summary>
        /// Disposes every preview entity currently owned by this synchronizer.
        /// </summary>
        void DisposeOwnedPreviewEntities() {
            Entity[] ownedSources = CreateOwnedSourceSnapshot();
            for (int index = 0; index < ownedSources.Length; index++) {
                DisposeOwnedPreviewEntity(ownedSources[index]);
            }
        }

        /// <summary>
        /// Creates one stable snapshot of the current registered entity list before synchronization mutates it.
        /// </summary>
        /// <returns>Snapshot of the current registered entities.</returns>
        Entity[] CreateEntitySnapshot() {
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            Entity[] snapshot = new Entity[entities.Count];
            for (int index = 0; index < entities.Count; index++) {
                snapshot[index] = entities[index];
            }

            return snapshot;
        }

        /// <summary>
        /// Creates one stable snapshot of the authored source entities currently owned by this synchronizer.
        /// </summary>
        /// <returns>Snapshot of owned authored source entities.</returns>
        Entity[] CreateOwnedSourceSnapshot() {
            Entity[] snapshot = new Entity[OwnedPreviewEntitiesBySourceEntity.Count];
            int writeIndex = 0;
            foreach (Entity sourceEntity in OwnedPreviewEntitiesBySourceEntity.Keys) {
                snapshot[writeIndex++] = sourceEntity;
            }

            return snapshot;
        }
    }
}
