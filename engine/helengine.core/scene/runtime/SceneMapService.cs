namespace helengine {
    /// <summary>
    /// Resolves optional authored scene-id remapping entries from the currently loaded scene set.
    /// </summary>
    public sealed class SceneMapService {
        /// <summary>
        /// Scene manager that supplies the currently loaded scene roots.
        /// </summary>
        readonly SceneManager SceneManagerValue;

        /// <summary>
        /// Initializes one runtime scene-map service.
        /// </summary>
        /// <param name="sceneManager">Scene manager that supplies the currently loaded scene roots.</param>
        public SceneMapService(SceneManager sceneManager) {
            SceneManagerValue = sceneManager;
        }

        /// <summary>
        /// Resolves one scene id through the loaded authored scene map, if any.
        /// </summary>
        /// <param name="sceneId">Logical scene id requested by the caller.</param>
        /// <returns>Mapped scene id when a matching entry exists; otherwise the original scene id.</returns>
        public string MapSceneId(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            SceneMapComponent sceneMapComponent = FindSingletonSceneMapComponent();
            if (sceneMapComponent == null) {
                return sceneId;
            }

            if (sceneMapComponent.Mappings.TryGetValue(sceneId, out string mappedSceneId)) {
                return mappedSceneId;
            }

            return sceneId;
        }

        /// <summary>
        /// Finds the single loaded scene-map component across all currently loaded scene roots.
        /// </summary>
        /// <returns>Loaded scene-map component when exactly one exists; otherwise null.</returns>
        SceneMapComponent FindSingletonSceneMapComponent() {
            if (SceneManagerValue == null) {
                return null;
            }

            SceneMapComponent resolvedComponent = null;
            IReadOnlyList<LoadedSceneRecord> loadedScenes = SceneManagerValue.LoadedScenes;
            for (int sceneIndex = 0; sceneIndex < loadedScenes.Count; sceneIndex++) {
                IReadOnlyList<Entity> rootEntities = loadedScenes[sceneIndex].RootEntities;
                for (int rootIndex = 0; rootIndex < rootEntities.Count; rootIndex++) {
                    VisitEntity(rootEntities[rootIndex], ref resolvedComponent);
                }
            }

            return resolvedComponent;
        }

        /// <summary>
        /// Visits one entity hierarchy looking for the singleton scene-map component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <param name="resolvedComponent">Current singleton candidate.</param>
        void VisitEntity(Entity entity, ref SceneMapComponent resolvedComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            List<Component> components = entity.Components;
            if (components != null) {
                for (int componentIndex = 0; componentIndex < components.Count; componentIndex++) {
                    if (components[componentIndex] is SceneMapComponent sceneMapComponent) {
                        if (resolvedComponent != null) {
                            throw new InvalidOperationException("Only one loaded SceneMapComponent may exist at a time.");
                        }

                        resolvedComponent = sceneMapComponent;
                    }
                }
            }

            List<Entity> children = entity.Children;
            if (children != null) {
                for (int childIndex = 0; childIndex < children.Count; childIndex++) {
                    VisitEntity(children[childIndex], ref resolvedComponent);
                }
            }
        }
    }
}
