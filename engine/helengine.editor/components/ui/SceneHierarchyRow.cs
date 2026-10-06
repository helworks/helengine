namespace helengine.editor {
    /// <summary>
    /// Bundles one hierarchy row's visuals, interaction state, and keyboard-focus target.
    /// </summary>
    public sealed class SceneHierarchyRow {
        readonly List<EditorEntity> depthGuideEntities;
        readonly List<SpriteComponent> depthGuideSprites;

        /// <summary>Initializes a legacy text-arrow row used by the entity picker.</summary>
        /// <param name="entity">Root entity for the row.</param>
        /// <param name="background">Background sprite for the row.</param>
        /// <param name="arrowHost">Entity hosting the picker disclosure text.</param>
        /// <param name="arrow">Text glyph that indicates picker branch state.</param>
        /// <param name="labelHost">Entity hosting the row label.</param>
        /// <param name="label">Text component for the row label.</param>
        /// <param name="interactable">Interactable region for the row.</param>
        /// <param name="focusTarget">Persistent keyboard-focus target for the row.</param>
        public SceneHierarchyRow(EditorEntity entity, SpriteComponent background, EditorEntity arrowHost, TextComponent arrow,
                EditorEntity labelHost, TextComponent label, InteractableComponent interactable, EditorFocusTarget focusTarget) {
            Entity = entity;
            Background = background;
            TopBorderEntity = null;
            TopBorder = null;
            BottomBorderEntity = null;
            BottomBorder = null;
            ArrowHost = arrowHost;
            Arrow = arrow;
            depthGuideEntities = new List<EditorEntity>();
            depthGuideSprites = new List<SpriteComponent>();
            LabelHost = labelHost;
            Label = label;
            Interactable = interactable;
            FocusTarget = focusTarget;
            BaseColor = ThemeManager.Colors.SurfacePrimary;
        }

        /// <summary>
        /// Initializes a new pooled hierarchy row.
        /// </summary>
        /// <param name="entity">Root entity for the row.</param>
        /// <param name="background">Background sprite for the row.</param>
        /// <param name="topBorderEntity">Entity hosting the row's top separator.</param>
        /// <param name="topBorder">One-pixel sprite for the row's top separator.</param>
        /// <param name="bottomBorderEntity">Entity hosting the row's bottom separator.</param>
        /// <param name="bottomBorder">One-pixel sprite for the row's bottom separator.</param>
        /// <param name="arrowHost">Entity hosting the row expand-collapse glyph.</param>
        /// <param name="disclosureBackground">Square button background and one-pixel border.</param>
        /// <param name="disclosureIconEntity">Entity hosting the SVG-derived expand-collapse icon.</param>
        /// <param name="disclosureIcon">PNG chevron shown by the button.</param>
        /// <param name="disclosureInteractable">Button-only hover and click region.</param>
        /// <param name="visibilityHost">Entity hosting the row's visibility button.</param>
        /// <param name="visibilityIconEntity">Entity hosting the visible or hidden eye sprite.</param>
        /// <param name="visibilityIcon">Visible or hidden eye sprite.</param>
        /// <param name="visibilityInteractable">Button-only pointer target for editor visibility.</param>
        /// <param name="labelHost">Entity hosting the row label.</param>
        /// <param name="label">Text component used for the row label.</param>
        /// <param name="interactable">Interactable region for the row.</param>
        /// <param name="focusTarget">Persistent keyboard-focus target for the row.</param>
        public SceneHierarchyRow(
            EditorEntity entity,
            SpriteComponent background,
            EditorEntity topBorderEntity,
            SpriteComponent topBorder,
            EditorEntity bottomBorderEntity,
            SpriteComponent bottomBorder,
            EditorEntity arrowHost,
            RoundedRectComponent disclosureBackground,
            EditorEntity disclosureIconEntity,
            SpriteComponent disclosureIcon,
            InteractableComponent disclosureInteractable,
            EditorEntity visibilityHost,
            EditorEntity visibilityIconEntity,
            SpriteComponent visibilityIcon,
            InteractableComponent visibilityInteractable,
            EditorEntity labelHost,
            TextComponent label,
            InteractableComponent interactable,
            EditorFocusTarget focusTarget) {

            Entity = entity;
            Background = background;
            TopBorderEntity = topBorderEntity;
            TopBorder = topBorder;
            BottomBorderEntity = bottomBorderEntity;
            BottomBorder = bottomBorder;
            ArrowHost = arrowHost;
            DisclosureBackground = disclosureBackground;
            DisclosureIconEntity = disclosureIconEntity;
            DisclosureIcon = disclosureIcon;
            DisclosureInteractable = disclosureInteractable;
            depthGuideEntities = new List<EditorEntity>();
            depthGuideSprites = new List<SpriteComponent>();
            VisibilityHost = visibilityHost;
            VisibilityIconEntity = visibilityIconEntity;
            VisibilityIcon = visibilityIcon;
            VisibilityInteractable = visibilityInteractable;
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

        /// <summary>Gets the entity hosting the top row separator.</summary>
        public EditorEntity TopBorderEntity { get; }

        /// <summary>Gets the sprite drawn as the top row separator.</summary>
        public SpriteComponent TopBorder { get; }

        /// <summary>Gets the entity hosting the bottom row separator.</summary>
        public EditorEntity BottomBorderEntity { get; }

        /// <summary>Gets the sprite drawn as the bottom row separator.</summary>
        public SpriteComponent BottomBorder { get; }

        /// <summary>
        /// Gets the entity hosting the row label.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Gets the entity hosting the expand-collapse glyph.
        /// </summary>
        public EditorEntity ArrowHost { get; }

        /// <summary>Gets the pooled entities that render this row's vertical hierarchy depth guides.</summary>
        internal List<EditorEntity> DepthGuideEntities => depthGuideEntities;

        /// <summary>Gets the sprites associated with the pooled vertical hierarchy depth guides.</summary>
        internal List<SpriteComponent> DepthGuideSprites => depthGuideSprites;

        /// <summary>Gets the legacy disclosure text used by the entity picker.</summary>
        public TextComponent Arrow { get; }

        /// <summary>Gets the individual disclosure button's outlined square.</summary>
        public RoundedRectComponent DisclosureBackground { get; }

        /// <summary>Gets the entity that rotates the chevron for expanded and collapsed states.</summary>
        public EditorEntity DisclosureIconEntity { get; }

        /// <summary>Gets the imported PNG chevron displayed by the disclosure button.</summary>
        public SpriteComponent DisclosureIcon { get; }

        /// <summary>Gets the button-only pointer target that displays a hand cursor.</summary>
        public InteractableComponent DisclosureInteractable { get; }

        /// <summary>Gets the entity that hosts the row's visibility icon and pointer target.</summary>
        public EditorEntity VisibilityHost { get; }

        /// <summary>Gets the entity that hosts the visible or hidden eye sprite.</summary>
        public EditorEntity VisibilityIconEntity { get; }

        /// <summary>Gets the icon that indicates the represented entity's authored visibility.</summary>
        public SpriteComponent VisibilityIcon { get; }

        /// <summary>Gets the button-only pointer target that toggles editor visibility.</summary>
        public InteractableComponent VisibilityInteractable { get; }

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
        /// Gets or sets the flattened hierarchy node index currently represented by this row.
        /// </summary>
        public int NodeIndex { get; set; }

        /// <summary>
        /// Gets or sets the base color used when the row is idle.
        /// </summary>
        public byte4 BaseColor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this row represents the current editor selection.
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this row may currently be selected or activated.
        /// </summary>
        public bool IsSelectable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this row represents the synthetic scene root entry.
        /// </summary>
        public bool IsSceneRoot { get; set; }

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

        /// <summary>Gets or sets the upper edge of the disclosure button's row-local hit region.</summary>
        public int ArrowHitTop { get; set; }

        /// <summary>Gets or sets the button height used when hit-testing the expand-collapse control.</summary>
        public int ArrowHitHeight { get; set; }

        /// <summary>Gets or sets the left edge of the visibility button's row-local hit region.</summary>
        public int VisibilityHitLeft { get; set; }

        /// <summary>Gets or sets the width of the visibility button's row-local hit region.</summary>
        public int VisibilityHitWidth { get; set; }

        /// <summary>Gets or sets the upper edge of the visibility button's row-local hit region.</summary>
        public int VisibilityHitTop { get; set; }

        /// <summary>Gets or sets the height of the visibility button's row-local hit region.</summary>
        public int VisibilityHitHeight { get; set; }

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

        /// <summary>Gets or sets whether the current press began inside the visibility button.</summary>
        public bool IsVisibilityPressed { get; set; }

        /// <summary>Gets or sets whether the eye button received the current pointer press.</summary>
        public bool IsVisibilityButtonPressed { get; set; }

        /// <summary>Gets or sets whether the disclosure button is currently hovered.</summary>
        public bool IsDisclosureHovering { get; set; }

        /// <summary>Gets or sets whether the disclosure button received the current pointer press.</summary>
        public bool IsDisclosureButtonPressed { get; set; }

        /// <summary>Gets or sets whether the visibility button is currently hovered.</summary>
        public bool IsVisibilityHovering { get; set; }

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
                   point.Y >= ArrowHitTop &&
                   point.Y < ArrowHitTop + ArrowHitHeight;
        }

        /// <summary>Returns true when a row-local point lies inside the visibility hit region.</summary>
        /// <param name="point">Pointer position in row-local coordinates.</param>
        /// <returns>True when the point lies inside the visibility button.</returns>
        public bool ContainsVisibilityPoint(int2 point) {
            return point.X >= VisibilityHitLeft &&
                   point.X < VisibilityHitLeft + VisibilityHitWidth &&
                   point.Y >= VisibilityHitTop &&
                   point.Y < VisibilityHitTop + VisibilityHitHeight;
        }
    }
}
