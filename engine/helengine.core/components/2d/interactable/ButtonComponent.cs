namespace helengine {
    /// <summary>
    /// Simple interactable button that renders rounded rect styling and invokes a click action.
    /// </summary>
    public class ButtonComponent : Component {
        string text;
        FontAsset font;
        int2 size;
        Action onClickAction;
        readonly float borderThickness;

        // Child entities and components
        Entity textEntity;
        RoundedRectComponent roundedRect;
        TextComponent textComponent;
        InteractableComponent interactableComponent;

        // Current state
        bool isHovering;
        bool isPressed;

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
        }

        /// <summary>
        /// Creates child components and sets up interactivity when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (!entity.Enabled) return;

            byte backgroundOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(1);
            byte textOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);

            // Create rounded rectangle background
            roundedRect = new RoundedRectComponent();
            roundedRect.Size = size;
            roundedRect.Radius = MathF.Min(size.X, size.Y) * 0.15f;
            roundedRect.BorderThickness = borderThickness;
            roundedRect.FillColor = ThemeManager.Colors.AccentSecondary;
            roundedRect.BorderColor = ThemeManager.Colors.AccentTertiary;
            roundedRect.RenderOrder2D = backgroundOrder;
            entity.AddComponent(roundedRect);

            // Create interactable component for mouse events
            interactableComponent = new InteractableComponent();
            interactableComponent.Size = size;
            interactableComponent.CursorEvent += OnCursorEvent;
            entity.AddComponent(interactableComponent);

            // Create text entity as child
            textEntity = new Entity();
            textEntity.LayerMask = entity.LayerMask;
            textEntity.Enabled = true;
            textEntity.InitComponents();

            entity.InitChildren();
            entity.AddChild(textEntity);

            // Precise centering using font-provided tight bounds for width; line height keeps vertical centering stable across glyphs with descenders
            var tight = font.MeasureTight(text);
            float lineHeight = MathF.Max(font.LineHeight, 1f);

            float px = (size.X - tight.Width) / 2f;
            float py = (size.Y - lineHeight) / 2f;
            // Snap to pixel grid to avoid half-pixel shimmering
            px = MathF.Round(px);
            py = MathF.Round(py);

            textEntity.Position = new float3(px, py, 0.1f);

            // Create text component
            textComponent = new TextComponent();
            textComponent.Text = text;
            textComponent.Font = font;
            textComponent.Color = ThemeManager.Colors.TextOnAccent;
            textComponent.Size = new int2((int)Math.Ceiling(tight.Width), (int)Math.Ceiling(lineHeight));
            textComponent.RenderOrder2D = textOrder;
            textEntity.AddComponent(textComponent);
        }

        /// <summary>
        /// Keeps child text entity enabled state in sync with parent.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (textEntity != null) {
                textEntity.Enabled = newEnabled;
            }
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

            if (isPressed) {
                roundedRect.FillColor = ThemeManager.Colors.AccentTertiary;
            } else if (isHovering) {
                roundedRect.FillColor = ThemeManager.Colors.AccentPrimary;
            } else {
                roundedRect.FillColor = ThemeManager.Colors.AccentSecondary;
            }
        }
    }
}
