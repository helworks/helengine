namespace helengine {
    /// <summary>
    /// Stores a stable reference to one serialized scene entity by id.
    /// </summary>
    public class SceneEntityReference {
        /// <summary>
        /// Gets or sets the stable id assigned to the referenced entity.
        /// </summary>
        public uint EntityId { get; set; }

        /// <summary>
        /// Gets the runtime entity resolved for this reference during scene loading.
        /// </summary>
        [ScenePersistenceIgnore]
        public Entity ResolvedEntity { get; internal set; }
    }
}
