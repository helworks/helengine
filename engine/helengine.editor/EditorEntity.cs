namespace helengine {
    /// <summary>
    /// Represents a base entity type used within the editor with naming and visibility helpers.
    /// </summary>
    public class EditorEntity : Entity {
        /// <summary>
        /// Initializes a new editor entity with default components and children.
        /// </summary>
        public EditorEntity() {
            Name = "Entity";

            InitComponents();
            InitChildren();
            AddComponent(new EntitySaveComponent());
        }

        /// <summary>
        /// Gets or sets a value indicating whether the entity should be hidden from rendering.
        /// </summary>
        public bool Hidden { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the entity is internal to the editor and hidden from the scene hierarchy.
        /// </summary>
        public bool InternalEntity { get; set; }

        /// <summary>
        /// Gets or sets the display name for the entity.
        /// </summary>
        public string Name { get; set; }
    }
}
