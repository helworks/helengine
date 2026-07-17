namespace helengine.editor {
    /// <summary>
    /// Tracks stable ids for entities participating in scene persistence.
    /// </summary>
    public class SceneEntityReferenceTable {
        /// <summary>
        /// Entity ids keyed by the live entity instance.
        /// </summary>
        readonly Dictionary<Entity, uint> EntityIdsByEntity;

        /// <summary>
        /// Live entities keyed by their stable id.
        /// </summary>
        readonly Dictionary<uint, Entity> EntitiesById;

        /// <summary>
        /// Initializes an empty entity reference table.
        /// </summary>
        public SceneEntityReferenceTable() {
            EntityIdsByEntity = new Dictionary<Entity, uint>();
            EntitiesById = new Dictionary<uint, Entity>();
        }

        /// <summary>
        /// Clears all cached live-entity registrations so one new save or history-capture session starts from the current scene graph.
        /// </summary>
        public void Clear() {
            EntityIdsByEntity.Clear();
            EntitiesById.Clear();
        }

        /// <summary>
        /// Returns the existing stable id for one entity.
        /// </summary>
        /// <param name="entity">Live entity to identify.</param>
        /// <returns>Stable id associated with the entity.</returns>
        public uint GetRequiredEntityId(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (EntityIdsByEntity.TryGetValue(entity, out uint entityId)) {
                return entityId;
            }

            EntitySaveComponent saveComponent = FindSaveComponent(entity);
            if (saveComponent != null && saveComponent.EntityId != 0u) {
                RegisterEntity(entity, saveComponent.EntityId);
                return saveComponent.EntityId;
            }

            throw new InvalidOperationException("Authored editor entities must have a preassigned numeric scene entity id.");
        }

        /// <summary>
        /// Returns the stable scene entity reference for one live entity.
        /// </summary>
        /// <param name="entity">Live entity to identify.</param>
        /// <returns>Stable scene entity reference for the entity.</returns>
        public SceneEntityReference GetOrCreateReference(Entity entity) {
            return new SceneEntityReference {
                EntityId = GetRequiredEntityId(entity)
            };
        }

        /// <summary>
        /// Registers one live entity for a stable id.
        /// </summary>
        /// <param name="entity">Live entity to register.</param>
        /// <param name="entityId">Stable id assigned to the entity.</param>
        public void RegisterEntity(Entity entity, uint entityId) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entityId == 0u) {
                throw new ArgumentException("Entity id must be non-zero.", nameof(entityId));
            }

            if (EntitiesById.TryGetValue(entityId, out Entity existingEntity) && !ReferenceEquals(existingEntity, entity)) {
                throw new InvalidOperationException($"An entity is already registered for id '{entityId}'.");
            }

            if (EntityIdsByEntity.TryGetValue(entity, out uint existingEntityId) && existingEntityId != entityId) {
                throw new InvalidOperationException("The entity is already registered with a different id.");
            }

            EntityIdsByEntity[entity] = entityId;
            EntitiesById[entityId] = entity;

            EntitySaveComponent saveComponent = FindSaveComponent(entity);
            if (saveComponent != null) {
                if (saveComponent.EntityId == 0u) {
                    saveComponent.EntityId = entityId;
                } else if (saveComponent.EntityId != entityId) {
                    throw new InvalidOperationException("The entity save component already stores a different id.");
                }
            }
        }

        /// <summary>
        /// Resolves one stable entity id back to a live entity.
        /// </summary>
        /// <param name="entityId">Stable id to resolve.</param>
        /// <returns>Live entity associated with the id.</returns>
        public Entity Resolve(uint entityId) {
            if (entityId == 0u) {
                throw new ArgumentException("Entity id must be non-zero.", nameof(entityId));
            }

            if (!EntitiesById.TryGetValue(entityId, out Entity entity)) {
                throw new InvalidOperationException($"No entity is registered for id '{entityId}'.");
            }

            return entity;
        }

        /// <summary>
        /// Resolves one stable scene entity reference back to a live entity.
        /// </summary>
        /// <param name="reference">Stable scene entity reference to resolve.</param>
        /// <returns>Live entity associated with the reference.</returns>
        public Entity Resolve(SceneEntityReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            return Resolve(reference.EntityId);
        }

        /// <summary>
        /// Finds the hidden save component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be returned.</param>
        /// <returns>Attached save component when present; otherwise null.</returns>
        static EntitySaveComponent FindSaveComponent(Entity entity) {
            if (entity == null || entity.Components == null) {
                return null;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            return null;
        }
    }
}
