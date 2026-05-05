namespace helengine.editor {
    /// <summary>
    /// Stores UI elements and state for a context menu row.
    /// </summary>
    public sealed class ContextMenuRow {
        /// <summary>
        /// Initializes a new context menu row with its visual components.
        /// </summary>
        /// <param name="entity">Root entity for the row.</param>
        /// <param name="background">Background sprite component.</param>
        /// <param name="labelHost">Host entity for the label.</param>
        /// <param name="label">Text label component.</param>
        /// <param name="indicatorHost">Host entity for the submenu indicator text.</param>
        /// <param name="indicator">Text component used to render the submenu indicator.</param>
        /// <param name="interactable">Interactable used for input.</param>
        public ContextMenuRow(
            EditorEntity entity,
            SpriteComponent background,
            EditorEntity labelHost,
            TextComponent label,
            EditorEntity indicatorHost,
            TextComponent indicator,
            InteractableComponent interactable) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (background == null) {
                throw new ArgumentNullException(nameof(background));
            }
            if (labelHost == null) {
                throw new ArgumentNullException(nameof(labelHost));
            }
            if (label == null) {
                throw new ArgumentNullException(nameof(label));
            }
            if (indicatorHost == null) {
                throw new ArgumentNullException(nameof(indicatorHost));
            }
            if (indicator == null) {
                throw new ArgumentNullException(nameof(indicator));
            }
            if (interactable == null) {
                throw new ArgumentNullException(nameof(interactable));
            }

            Entity = entity;
            Background = background;
            LabelHost = labelHost;
            Label = label;
            IndicatorHost = indicatorHost;
            Indicator = indicator;
            Interactable = interactable;
            BaseColor = ThemeManager.Colors.SurfacePrimary;
            HoverColor = ThemeManager.Colors.AccentSecondary;
            PressedColor = ThemeManager.Colors.AccentPrimary;

            Interactable.CursorEvent += HandleCursorEvent;
        }

        /// <summary>
        /// Raised when the row is activated by a click.
        /// </summary>
        public event Action<ContextMenuRow> Activated;

        /// <summary>
        /// Raised when the pointer first presses on the row.
        /// </summary>
        public event Action<ContextMenuRow> Pressed;

        /// <summary>
        /// Raised when the row is hovered by the pointer.
        /// </summary>
        public event Action<ContextMenuRow> Hovered;
        /// <summary>
        /// Raised when the pointer leaves the row.
        /// </summary>
        public event Action<ContextMenuRow> Left;

        /// <summary>
        /// Gets the root entity for this row.
        /// </summary>
        public EditorEntity Entity { get; }

        /// <summary>
        /// Gets the background sprite for the row.
        /// </summary>
        public SpriteComponent Background { get; }

        /// <summary>
        /// Gets the host entity for the label.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Gets the label text component.
        /// </summary>
        public TextComponent Label { get; }

        /// <summary>
        /// Gets the host entity for the submenu indicator text.
        /// </summary>
        public EditorEntity IndicatorHost { get; }

        /// <summary>
        /// Gets the text component used to render the submenu indicator.
        /// </summary>
        public TextComponent Indicator { get; }

        /// <summary>
        /// Gets the interactable component used for hit testing.
        /// </summary>
        public InteractableComponent Interactable { get; }

        /// <summary>
        /// Gets or sets the menu item assigned to this row.
        /// </summary>
        public ContextMenuItem Item { get; set; }

        /// <summary>
        /// Gets or sets the descriptor currently rendered by this row.
        /// </summary>
        public EditorComponentAddDescriptor CurrentDescriptor { get; set; }

        /// <summary>
        /// Gets or sets the base color used when idle.
        /// </summary>
        public byte4 BaseColor { get; set; }

        /// <summary>
        /// Gets or sets the hover color used when the row is hovered.
        /// </summary>
        public byte4 HoverColor { get; set; }

        /// <summary>
        /// Gets or sets the pressed color used when the row is pressed.
        /// </summary>
        public byte4 PressedColor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is hovered.
        /// </summary>
        public bool IsHovering { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is pressed.
        /// </summary>
        public bool IsPressed { get; set; }

        /// <summary>
        /// Resets row hover and press state.
        /// </summary>
        public void ResetState() {
            IsHovering = false;
            IsPressed = false;
            UpdateBackground();
        }

        /// <summary>
        /// Updates the background color based on hover and press state.
        /// </summary>
        public void UpdateBackground() {
            if (IsPressed) {
                Background.Color = PressedColor;
                return;
            }

            if (IsHovering) {
                Background.Color = HoverColor;
                return;
            }

            Background.Color = BaseColor;
        }

        /// <summary>
        /// Handles pointer interactions on the row.
        /// </summary>
        /// <param name="pos">Pointer position relative to the row.</param>
        /// <param name="delta">Pointer delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleCursorEvent(int2 pos, int2 delta, PointerInteraction state) {
            bool wasHovering = IsHovering;
            switch (state) {
                case PointerInteraction.Hover:
                    IsHovering = true;
                    if (!wasHovering && Hovered != null) {
                        Hovered(this);
                    }
                    break;
                case PointerInteraction.Press:
                    IsPressed = true;
                    if (Pressed != null) {
                        Pressed(this);
                    }
                    break;
                case PointerInteraction.Release:
                    bool shouldActivate = IsPressed && IsHovering;
                    IsPressed = false;
                    if (shouldActivate && Activated != null) {
                        Activated(this);
                    }
                    break;
                case PointerInteraction.Leave:
                    IsHovering = false;
                    IsPressed = false;
                    if (wasHovering && Left != null) {
                        Left(this);
                    }
                    break;
                default:
                    break;
            }

            UpdateBackground();
        }
    }
}
