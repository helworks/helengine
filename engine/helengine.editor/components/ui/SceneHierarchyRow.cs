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
        /// <param name="arrowHost">Entity hosting the row expand-collapse glyph.</param>
        /// <param name="arrow">Text component used for the row expand-collapse glyph.</param>
        /// <param name="labelHost">Entity hosting the row label.</param>
        /// <param name="label">Text component used for the row label.</param>
        /// <param name="interactable">Interactable region for the row.</param>
        /// <param name="focusTarget">Persistent keyboard-focus target for the row.</param>
        public SceneHierarchyRow(
            EditorEntity entity,
            SpriteComponent background,
            EditorEntity arrowHost,
            TextComponent arrow,
            EditorEntity labelHost,
            TextComponent label,
            InteractableComponent interactable,
            EditorFocusTarget focusTarget) {

            Entity = entity;
            Background = background;
            ArrowHost = arrowHost;
            Arrow = arrow;
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
        /// Gets the entity hosting the expand-collapse glyph.
        /// </summary>
        public EditorEntity ArrowHost { get; }

        /// <summary>
        /// Gets the text component used for the expand-collapse glyph.
        /// </summary>
        public TextComponent Arrow { get; }

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
        /// Gets or sets a value indicating whether this row represents an entity with visible scene children.
        /// </summary>
        public bool HasChildren { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this row's branch is currently expanded.
        /// </summary>
        public bool IsExpanded { get; set; }

        /// <summary>
        /// Gets or sets the left edge of the local arrow hit region.
        /// </summary>
        public int ArrowHitLeft { get; set; }

        /// <summary>
        /// Gets or sets the width of the local arrow hit region.
        /// </summary>
        public int ArrowHitWidth { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is hovered.
        /// </summary>
        public bool IsHovering { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is pressed.
        /// </summary>
        public bool IsPressed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the current press started inside the arrow hit region.
        /// </summary>
        public bool IsArrowPressed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is currently keyboard-focused.
        /// </summary>
        public bool IsKeyboardFocused { get; set; }

        /// <summary>
        /// Returns true when the provided local row point lies inside the expand-collapse hit region.
        /// </summary>
        /// <param name="point">Pointer position in row-local coordinates.</param>
        /// <returns>True when the row has children and the point lies inside the arrow hit region.</returns>
        public bool ContainsArrowPoint(int2 point) {
            if (!HasChildren) {
                return false;
            }

            return point.X >= ArrowHitLeft &&
                   point.X < ArrowHitLeft + ArrowHitWidth &&
                   point.Y >= 0 &&
                   point.Y < SceneHierarchyPanel.RowHeight;
        }
    }
}
