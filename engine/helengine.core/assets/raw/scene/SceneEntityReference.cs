namespace helengine {
    /// <summary>
    /// Stores a stable reference to one serialized scene entity by id.
    /// </summary>
    public class SceneEntityReference {
        /// <summary>
        /// Gets or sets the stable id assigned to the referenced entity.
        /// </summary>
        public uint EntityId { get; set; }
    }
}
