namespace helengine {
    /// <summary>
    /// Represents a base entity type used within the editor with naming and visibility helpers.
    /// </summary>
    public class EditorEntity : Entity {
        /// <summary>
        /// Initializes a new editor entity with default components and children.
        /// </summary>
        /// <summary>
        /// Mutable interaction graph owned by the editor session that created this entity.
        /// </summary>
        public global::helengine.editor.EditorSessionInteractionServices InteractionServices { get; internal set; }

        public EditorEntity() : base() {
            InteractionServices = (OwnerCore as EditorCore)?.SessionInteractionServices
                ?? new global::helengine.editor.EditorSessionInteractionServices();
            InitializeEditorEntity();
        }

        /// <summary>
        /// Initializes an editor entity against an explicit owning core. The
        /// session interaction graph is resolved from that core when available.
        /// </summary>
        /// <param name="ownerCore">Core whose object manager owns the entity.</param>
        public EditorEntity(Core ownerCore) : base(ownerCore) {
            InteractionServices = (ownerCore as EditorCore)?.SessionInteractionServices
                ?? new global::helengine.editor.EditorSessionInteractionServices();
            InitializeEditorEntity();
        }

        /// <summary>
        /// Initializes an editor entity against an explicit core and session interaction graph.
        /// </summary>
        public EditorEntity(Core ownerCore, global::helengine.editor.EditorSessionInteractionServices interactionServices)
            : base(ownerCore) {
            InteractionServices = interactionServices ?? throw new ArgumentNullException(nameof(interactionServices));
            InitializeEditorEntity();
        }

        void InitializeEditorEntity() {
            Name = "Entity";

            InitComponents();
            InitChildren();
            AddComponent(new EntitySaveComponent());
        }

        /// <inheritdoc />
        protected override void OwnerCoreChanged(Core ownerCore) {
            if (ownerCore is EditorCore editorCore && editorCore.SessionInteractionServices != null) {
                InteractionServices = editorCore.SessionInteractionServices;
            }
        }

        /// <summary>
        /// Rebinds an unattached editor subtree to one explicit interaction graph.
        /// This is used by composition fixtures that construct a hierarchy before
        /// assigning it to an owning session core.
        /// </summary>
        internal void RebindInteractionServices(global::helengine.editor.EditorSessionInteractionServices interactionServices) {
            InteractionServices = interactionServices ?? throw new ArgumentNullException(nameof(interactionServices));
            if (Children == null) {
                return;
            }
            for (int index = 0; index < Children.Count; index++) {
                if (Children[index] is EditorEntity child) {
                    child.RebindInteractionServices(interactionServices);
                }
            }
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
        /// Gets or sets whether this entity belongs to the authored scene and participates in editor persistence and scene lifecycle operations.
        /// This editor-only state is intentionally independent from the runtime render layer mask.
        /// </summary>
        public bool IsSceneOwned { get; set; }

        /// <summary>
        /// Gets or sets the display name for the entity.
        /// </summary>
        public string Name { get; set; }
    }
}
