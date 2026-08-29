namespace helengine {
    /// <summary>
    /// Represents a base entity type used within the editor with naming and visibility helpers.
    /// </summary>
    public class EditorEntity : Entity {
        /// <summary>Resolves the interaction graph explicitly owned by an editor core.</summary>
        internal static global::helengine.editor.EditorSessionInteractionServices RequireInteractionServices(Core ownerCore) {
            if (ownerCore == null) {
                throw new ArgumentNullException(nameof(ownerCore));
            }

            if (ownerCore is global::helengine.EditorCore editorCore && editorCore.SessionInteractionServices != null) {
                return editorCore.SessionInteractionServices;
            }

            if (ownerCore.SessionInteractionGraph is global::helengine.editor.EditorSessionInteractionServices graph) {
                return graph;
            }

            if (ownerCore is not global::helengine.EditorCore) {
                throw new InvalidOperationException("An owning core with an attached session interaction graph is required.");
            }

            {
                throw new InvalidOperationException("An editor core with an attached session interaction graph is required.");
            }
        }

        /// <summary>
        /// Initializes a new editor entity with default components and children.
        /// </summary>
        /// <summary>
        /// Mutable interaction graph owned by the editor session that created this entity.
        /// </summary>
        public global::helengine.editor.EditorSessionInteractionServices InteractionServices { get; }

        static Core ValidateOwnerGraph(Core ownerCore, global::helengine.editor.EditorSessionInteractionServices interactionServices) {
            if (ownerCore == null) {
                throw new ArgumentNullException(nameof(ownerCore));
            }
            if (interactionServices == null) {
                throw new ArgumentNullException(nameof(interactionServices));
            }

            if (ownerCore is global::helengine.EditorCore editorCore
                && editorCore.SessionInteractionServices != null
                && !ReferenceEquals(editorCore.SessionInteractionServices, interactionServices)) {
                throw new InvalidOperationException("The editor entity interaction graph must be the graph attached to its owning editor core.");
            }
            if (ownerCore.SessionInteractionGraph is global::helengine.editor.EditorSessionInteractionServices attachedGraph
                && !ReferenceEquals(attachedGraph, interactionServices)) {
                throw new InvalidOperationException("The editor entity interaction graph must be the graph attached to its owning core.");
            }

            return ownerCore;
        }

        /// <summary>
        /// Initializes an editor entity against an explicit core and session interaction graph.
        /// </summary>
        public EditorEntity(Core ownerCore, global::helengine.editor.EditorSessionInteractionServices interactionServices)
            : base(ValidateOwnerGraph(ownerCore, interactionServices)) {
            InteractionServices = interactionServices;
            InitializeEditorEntity();
        }

        void InitializeEditorEntity() {
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
