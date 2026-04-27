namespace helengine.editor {
    /// <summary>
    /// Bundles one hierarchy row's visuals, interaction state, and keyboard-focus target.
    /// </summary>
    public sealed class SceneHierarchyRow {
        /// <summary>
        /// Initializes a new pooled hierarchy row.
        /// </summary>
        /// <param name="entity">Root entity for the row.</param>
        /// <param name="background">Background sprite for the row.</param>
        /// <param name="labelHost">Entity hosting the row label.</param>
        /// <param name="label">Text component used for the row label.</param>
        /// <param name="interactable">Interactable region for the row.</param>
        /// <param name="focusTarget">Persistent keyboard-focus target for the row.</param>
        public SceneHierarchyRow(
            EditorEntity entity,
            SpriteComponent background,
            EditorEntity labelHost,
            TextComponent label,
            InteractableComponent interactable,
            EditorFocusTarget focusTarget) {

            Entity = entity;
            Background = background;
            LabelHost = labelHost;
            Label = label;
            Interactable = interactable;
            FocusTarget = focusTarget;
            BaseColor = ThemeManager.Colors.SurfacePrimary;
        }

        /// <summary>
        /// Gets the row root entity.
        /// </summary>
        public EditorEntity Entity { get; }

        /// <summary>
        /// Gets the background sprite component.
        /// </summary>
        public SpriteComponent Background { get; }

        /// <summary>
        /// Gets the entity hosting the row label.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Gets the text component for the row label.
        /// </summary>
        public TextComponent Label { get; }

        /// <summary>
        /// Gets the interactable region used for pointer input.
        /// </summary>
        public InteractableComponent Interactable { get; }

        /// <summary>
        /// Gets the persistent keyboard-focus target assigned to this pooled row.
        /// </summary>
        public EditorFocusTarget FocusTarget { get; }

        /// <summary>
        /// Gets or sets the scene entity currently represented by this row.
        /// </summary>
        public Entity NodeEntity { get; set; }

        /// <summary>
        /// Gets or sets the base color used when the row is idle.
        /// </summary>
        public byte4 BaseColor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this row represents the current editor selection.
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is hovered.
        /// </summary>
        public bool IsHovering { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is pressed.
        /// </summary>
        public bool IsPressed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is currently keyboard-focused.
        /// </summary>
        public bool IsKeyboardFocused { get; set; }
    }
}
