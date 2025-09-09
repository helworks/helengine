namespace helengine {
    public class ButtonComponent : Component {
        string text;
        FontAsset font;
        int2 size;
        Action? onClickAction;

        // Child entities and components
        Entity? textEntity;
        RoundedRectComponent? roundedRect;
        TextComponent? textComponent;
        InteractableComponent? interactableComponent;

        // Current state
        bool isHovering;
        bool isPressed;

        public ButtonComponent(
            string text,
            int2 size,
            FontAsset font,
            Action? onClickAction = null) {

            this.text = text;
            this.size = size;
            this.font = font;
            this.onClickAction = onClickAction;
        }

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (!entity.Enabled) return;

            // Create rounded rectangle background
            roundedRect = new RoundedRectComponent();
            roundedRect.Size = size;
            roundedRect.Radius = MathF.Min(size.X, size.Y) * 0.15f;
            roundedRect.BorderThickness = 2f;
            roundedRect.FillColor = ThemeManager.Colors.AccentSecondary;
            roundedRect.BorderColor = ThemeManager.Colors.AccentTertiary;
            roundedRect.RenderOrder2D = 2;
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

            // Precise centering using font-provided tight bounds
            var tight = font.MeasureTight(text);

            float px = (size.X - tight.Width) / 2f;
            float py = (size.Y / 2f) - ((tight.MinTop + tight.MaxBottom) / 2f);
            // Snap to pixel grid to avoid half-pixel shimmering
            px = MathF.Round(px);
            py = MathF.Round(py);

            textEntity.Position = new float3(px, py, 0.1f);

            // Create text component
            textComponent = new TextComponent();
            textComponent.Text = text;
            textComponent.Font = font;
            textComponent.Color = ThemeManager.Colors.TextOnAccent;
            textComponent.Size = new int2((int)Math.Ceiling(tight.Width), (int)Math.Ceiling(tight.Height));
            textComponent.RenderOrder2D = 3;
            textEntity.AddComponent(textComponent);
        }

        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (textEntity != null) {
                textEntity.Enabled = newEnabled;
            }
        }

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
