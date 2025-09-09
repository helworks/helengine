namespace helengine {
    public class ButtonComponent : Component {
        string text;
        FontAsset font;
        int2 size;
        Action? onClickAction;

        // Child entities and components
        Entity? textEntity;
        SpriteComponent? spriteComponent;
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

            // Create sprite component for button background
            spriteComponent = new SpriteComponent();
            spriteComponent.Texture = TextureUtils.PixelTexture;
            spriteComponent.Color = ThemeManager.Colors.AccentSecondary;
            spriteComponent.Size = size;
            spriteComponent.RenderOrder2D = 2;
            entity.AddComponent(spriteComponent);

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

            // Precise centering using glyph metrics (tight bounds)
            float totalWidth = 0f;
            float minTop = float.MaxValue;
            float maxBottom = float.MinValue;
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (c == ' ') {
                    totalWidth += font.FontInfo.SpaceWidth;
                    continue;
                }
                if (!font.Characters.TryGetValue(c, out var ch)) continue;

                float adv = ch.AdvanceWidth > 0 ? ch.AdvanceWidth : (ch.SourceRect.Z * font.AtlasWidth);
                totalWidth += adv;

                float glyphTop = ch.OffsetY;
                float glyphBottom = ch.OffsetY + (ch.SourceRect.W * font.AtlasHeight);
                if (glyphTop < minTop) minTop = glyphTop;
                if (glyphBottom > maxBottom) maxBottom = glyphBottom;
            }

            if (minTop == float.MaxValue) { // empty string
                minTop = 0f;
                maxBottom = font.LineHeight;
            }

            float tightHeight = Math.Max(1f, maxBottom - minTop);

            float px = (size.X - totalWidth) / 2f;
            float py = (size.Y / 2f) - ((minTop + maxBottom) / 2f);
            // Snap to pixel grid to avoid half-pixel shimmering
            px = MathF.Round(px);
            py = MathF.Round(py);

            textEntity.Position = new float3(px, py, 0.1f);

            // Create text component
            textComponent = new TextComponent();
            textComponent.Text = text;
            textComponent.Font = font;
            textComponent.Color = ThemeManager.Colors.TextOnAccent;
            textComponent.Size = new int2((int)Math.Ceiling(totalWidth), (int)Math.Ceiling(tightHeight));
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
            if (spriteComponent == null) return;

            if (isPressed) {
                spriteComponent.Color = ThemeManager.Colors.AccentTertiary;
            } else if (isHovering) {
                spriteComponent.Color = ThemeManager.Colors.AccentPrimary;
            } else {
                spriteComponent.Color = ThemeManager.Colors.AccentSecondary;
            }
        }
    }
}
