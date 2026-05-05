namespace helengine {
    /// <summary>
    /// Simple interactable button that renders rounded rect styling and invokes a click action.
    /// </summary>
    public class ButtonComponent : Component, IFocusTarget, IAnchorSizeProvider {
        string text;
        FontAsset font;
        int2 size;
        Action onClickAction;
        readonly float borderThickness;
        /// <summary>
        /// Tracks whether custom render orders were supplied for the button visuals.
        /// </summary>
        bool HasRenderOrderOverrides;
        /// <summary>
        /// Render order override for the rounded rectangle background.
        /// </summary>
        byte BackgroundRenderOrder;
        /// <summary>
        /// Render order override for the button label text.
        /// </summary>
        byte TextRenderOrder;
        /// <summary>
        /// Transparent fill used by buttons that should only show their background during interaction.
        /// </summary>
        static readonly byte4 TransparentBackgroundColor = new byte4(255, 255, 255, 0);
        /// <summary>
        /// Tracks whether the idle button background should be transparent until hovered or pressed.
        /// </summary>
        bool UsesHoverOnlyBackground;
        /// <summary>
        /// Tracks the rounded corners that should remain active on the backing shape.
        /// </summary>
        public RoundedRectCorners Corners { get; private set; }
        /// <summary>
        /// Color used when creating or updating the button label text component.
        /// </summary>
        byte4 ButtonTextColor;
        /// <summary>
        /// Corner radius applied to the rounded rectangle background.
        /// </summary>
        float CornerRadius;
        /// <summary>
        /// Cursor requested while the pointer hovers the button.
        /// </summary>
        PointerCursorKind HoverCursorKind;

        // Child entities and components
        Entity textEntity;
        RoundedRectComponent roundedRect;
        TextComponent textComponent;
        InteractableComponent interactableComponent;

        // Current state
        bool isHovering;
        bool isPressed;

        /// <summary>
        /// Gets or sets the focus group that owns this button during keyboard traversal.
        /// </summary>
        public IFocusGroup FocusGroup { get; set; }

        /// <summary>
        /// Gets or sets the traversal order of this button within its focus group.
        /// </summary>
        public int TabIndex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this button is the preferred entry target for its root group.
        /// </summary>
        public bool IsDefaultTarget { get; set; }

        /// <summary>
        /// Gets whether this button can currently receive keyboard focus.
        /// </summary>
        public bool CanReceiveFocus => Parent != null && Parent.IsHierarchyEnabled && interactableComponent != null;

        /// <summary>
        /// Gets a value indicating whether this button is currently keyboard-focused.
        /// </summary>
        public bool IsKeyboardFocused { get; private set; }

        /// <summary>
        /// Gets the current button size.
        /// </summary>
        public int2 Size => size;

        /// <summary>
        /// Gets or sets the font used to render the button label.
        /// </summary>
        public FontAsset Font {
            get { return font; }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                font = value;
                if (textComponent != null) {
                    textComponent.Font = font;
                }

                ApplyTextLayout();
            }
        }

        /// <summary>
        /// Gets the size used by anchor components when the button host is pinned to a layout edge.
        /// </summary>
        public int2 AnchorSize => size;

        /// <summary>
        /// Raised when the pointer first enters the button during a hover interaction.
        /// </summary>
        public event Action Hovered;

        /// <summary>
        /// Creates a new button with text, size, font, and optional click action.
        /// </summary>
        /// <param name="text">Label text displayed on the button.</param>
        /// <param name="size">Button dimensions.</param>
        /// <param name="font">Font used to render the label.</param>
        /// <param name="onClickAction">Optional callback invoked on click.</param>
        /// <param name="borderThickness">Border thickness in pixels.</param>
        public ButtonComponent(
            string text,
            int2 size,
            FontAsset font,
            Action onClickAction = null,
            float borderThickness = 2f) {

            this.text = text;
            this.size = size;
            this.font = font;
            this.onClickAction = onClickAction;
            this.borderThickness = borderThickness;
            ButtonTextColor = ThemeManager.Colors.TextOnAccent;
            Corners = RoundedRectCorners.All;
            UpdateCornerRadius();
        }

        /// <summary>
        /// Overrides the render order used for the button background and label.
        /// </summary>
        /// <param name="backgroundOrder">Render order for the rounded rectangle background.</param>
        /// <param name="textOrder">Render order for the label text.</param>
        public void SetRenderOrders(byte backgroundOrder, byte textOrder) {
            HasRenderOrderOverrides = true;
            BackgroundRenderOrder = backgroundOrder;
            TextRenderOrder = textOrder;

            if (roundedRect != null) {
                roundedRect.RenderOrder2D = backgroundOrder;
            }

            if (textComponent != null) {
                textComponent.RenderOrder2D = textOrder;
            }
        }

        /// <summary>
        /// Configures the button to render no idle background while preserving hover, pressed, and focus styling.
        /// </summary>
        public void UseHoverOnlyBackground() {
            UsesHoverOnlyBackground = true;
            UpdateButtonColor();
        }

        /// <summary>
        /// Sets the cursor displayed while the pointer hovers the button.
        /// </summary>
        /// <param name="cursor">Cursor shown on hover.</param>
        public void SetHoverCursor(PointerCursorKind cursor) {
            HoverCursorKind = cursor;

            if (interactableComponent != null) {
                interactableComponent.HoverCursor = cursor;
            }
        }

        /// <summary>
        /// Sets the color used by the button label text.
        /// </summary>
        /// <param name="color">Label color to render.</param>
        public void SetTextColor(byte4 color) {
            ButtonTextColor = color;

            if (textComponent != null) {
                textComponent.Color = color;
            }
        }

        /// <summary>
        /// Configures the button background to render with square corners.
        /// </summary>
        public void UseSquareCorners() {
            Corners = RoundedRectCorners.None;
            CornerRadius = 0f;

            if (roundedRect != null) {
                roundedRect.Corners = Corners;
                roundedRect.Radius = CornerRadius;
            }
        }

        /// <summary>
        /// Configures the button background to round only the top corners.
        /// </summary>
        public void UseTopCorners() {
            Corners = (RoundedRectCorners)((int)RoundedRectCorners.TopLeft + (int)RoundedRectCorners.TopRight);
            UpdateCornerRadius();

            if (roundedRect != null) {
                roundedRect.Corners = Corners;
                roundedRect.Radius = CornerRadius;
            }
        }

        /// <summary>
        /// Updates the button bounds and reapplies the existing visual layout.
        /// </summary>
        /// <param name="newSize">New button dimensions.</param>
        public void SetSize(int2 newSize) {
            if (newSize.X < 1 || newSize.Y < 1) {
                throw new ArgumentOutOfRangeException(nameof(newSize), "Button size must be positive.");
            }

            size = newSize;
            if (Corners != RoundedRectCorners.None) {
                UpdateCornerRadius();
            }

            if (roundedRect != null) {
                roundedRect.Size = size;
                roundedRect.Corners = Corners;
                roundedRect.Radius = CornerRadius;
            }

            if (interactableComponent != null) {
                interactableComponent.Size = size;
                interactableComponent.HoverCursor = HoverCursorKind;
            }

            if (textEntity == null || textComponent == null) {
                return;
            }

            ApplyTextLayout();
        }

        /// <summary>
        /// Creates child components and sets up interactivity when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (!entity.Enabled) return;

            byte backgroundOrder = RenderOrder2D.PanelSurface;
            byte textOrder = RenderOrder2D.PanelForeground;
            if (HasRenderOrderOverrides) {
                backgroundOrder = BackgroundRenderOrder;
                textOrder = TextRenderOrder;
            }

            // Create rounded rectangle background
            roundedRect = new RoundedRectComponent();
            roundedRect.Size = size;
            roundedRect.Corners = Corners;
            roundedRect.Radius = CornerRadius;
            roundedRect.BorderThickness = borderThickness;
            roundedRect.FillColor = ThemeManager.Colors.AccentSecondary;
            roundedRect.BorderColor = ThemeManager.Colors.AccentTertiary;
            roundedRect.RenderOrder2D = backgroundOrder;
            entity.AddComponent(roundedRect);
            UpdateButtonColor();

            // Create interactable component for mouse events
            interactableComponent = new InteractableComponent();
            interactableComponent.Size = size;
            interactableComponent.HoverCursor = HoverCursorKind;
            interactableComponent.CursorEvent += OnCursorEvent;
            entity.AddComponent(interactableComponent);

            // Create text entity as child
            textEntity = new Entity();
            textEntity.LayerMask = entity.LayerMask;
            textEntity.Enabled = true;
            textEntity.InitComponents();

            entity.InitChildren();
            entity.AddChild(textEntity);

            // Create text component
            textComponent = new TextComponent();
            textComponent.Text = text;
            textComponent.Font = font;
            textComponent.Color = ButtonTextColor;
            textComponent.Size = new int2(1, 1);
            textComponent.RenderOrder2D = textOrder;
            textEntity.AddComponent(textComponent);

            ApplyTextLayout();
        }

        /// <summary>
        /// Keeps child text entity enabled state in sync with parent.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled) {
                isHovering = false;
                isPressed = false;
                SetTargetFocused(false);
            }

            if (textEntity != null) {
                textEntity.Enabled = newEnabled;
            }
        }

        /// <summary>
        /// Clears transient interaction and keyboard-focus state when the button is removed.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);

            isHovering = false;
            isPressed = false;
            SetTargetFocused(false);
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the button bounds.
        /// </summary>
        /// <param name="x">Screen-space X coordinate to evaluate.</param>
        /// <param name="y">Screen-space Y coordinate to evaluate.</param>
        /// <returns>True when the point is inside the button.</returns>
        public bool ContainsScreenPoint(int x, int y) {
            if (Parent == null) {
                return false;
            }

            float3 position = Parent.Position;
            return x >= position.X &&
                   x < position.X + size.X &&
                   y >= position.Y &&
                   y < position.Y + size.Y;
        }

        /// <summary>
        /// Applies or clears the keyboard-focused visual state for the button.
        /// </summary>
        /// <param name="isFocused">True when the button should render as focused.</param>
        public void SetTargetFocused(bool isFocused) {
            if (IsKeyboardFocused == isFocused) {
                UpdateButtonColor();
                return;
            }

            IsKeyboardFocused = isFocused;
            UpdateButtonColor();
        }

        /// <summary>
        /// Returns true when the button should activate for the provided key.
        /// </summary>
        /// <param name="key">Activation key to evaluate.</param>
        /// <returns>True when Enter or Space should activate the button.</returns>
        public bool CanActivateWithKey(Keys key) {
            return key == Keys.Enter || key == Keys.Space;
        }

        /// <summary>
        /// Invokes the button action for supported keyboard activation keys.
        /// </summary>
        /// <param name="key">Activation key routed to the button.</param>
        public void ActivateFromKey(Keys key) {
            if (!CanActivateWithKey(key)) {
                return;
            }

            onClickAction?.Invoke();
        }

        /// <summary>
        /// Handles cursor events to manage hover/press states and clicks.
        /// </summary>
        /// <param name="relPos">Relative pointer position.</param>
        /// <param name="delta">Pointer delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void OnCursorEvent(int2 relPos, int2 delta, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Hover:
                    if (!isHovering) {
                        isHovering = true;
                        UpdateButtonColor();
                        RaiseHovered();
                    }
                    break;

                case PointerInteraction.Press:
                    isPressed = true;
                    UpdateButtonColor();
                    break;

                case PointerInteraction.Release:
                    if (isPressed && isHovering) {
                        // Trigger click action
                        onClickAction?.Invoke();
                    }
                    isPressed = false;
                    UpdateButtonColor();
                    break;

                case PointerInteraction.Leave:
                    // Pointer left the button's bounds
                    if (isHovering || isPressed) {
                        isHovering = false;
                        isPressed = false;
                        UpdateButtonColor();
                    }
                    break;

                case PointerInteraction.None:
                    // No-op under new semantics; retain state.
                    break;
            }
        }

        /// <summary>
        /// Updates the button fill color based on hover/pressed state.
        /// </summary>
        void UpdateButtonColor() {
            if (roundedRect == null) return;

            roundedRect.BorderColor = IsKeyboardFocused
                ? ThemeManager.Colors.AccentPrimary
                : GetIdleBorderColor();

            if (isPressed) {
                roundedRect.FillColor = ThemeManager.Colors.AccentTertiary;
            } else if (isHovering) {
                roundedRect.FillColor = ThemeManager.Colors.AccentPrimary;
            } else {
                roundedRect.FillColor = GetIdleFillColor();
            }
        }

        /// <summary>
        /// Gets the fill color used when the button is neither hovered nor pressed.
        /// </summary>
        /// <returns>Transparent fill for hover-only buttons; normal accent fill otherwise.</returns>
        byte4 GetIdleFillColor() {
            if (UsesHoverOnlyBackground) {
                return TransparentBackgroundColor;
            }

            return ThemeManager.Colors.AccentSecondary;
        }

        /// <summary>
        /// Gets the border color used when the button is not keyboard-focused.
        /// </summary>
        /// <returns>Transparent border for hover-only buttons; normal accent border otherwise.</returns>
        byte4 GetIdleBorderColor() {
            if (UsesHoverOnlyBackground) {
                return TransparentBackgroundColor;
            }

            return ThemeManager.Colors.AccentTertiary;
        }

        /// <summary>
        /// Recomputes the shared corner radius from the current button size.
        /// </summary>
        void UpdateCornerRadius() {
            CornerRadius = (float)(Math.Min((double)size.X, (double)size.Y) * 0.15d);
        }

        /// <summary>
        /// Raises the hover event when a listener is interested in pointer entry.
        /// </summary>
        void RaiseHovered() {
            if (Hovered != null) {
                Hovered();
            }
        }

        /// <summary>
        /// Recomputes the label size and position for the current button bounds.
        /// </summary>
        void ApplyTextLayout() {
            if (textEntity == null || textComponent == null) {
                return;
            }

            var tight = font.MeasureTight(text);
            double lineHeight = Math.Max((double)font.LineHeight, 1d);

            double px = ((double)size.X - tight.Width) / 2d;
            double py = ((double)size.Y - lineHeight) / 2d;
            px = Math.Round(px);
            py = Math.Round(py);

            textEntity.Position = new float3((float)px, (float)py, 0.1f);
            textComponent.Size = new int2((int)Math.Ceiling(tight.Width), (int)Math.Ceiling(lineHeight));
        }
    }
}
