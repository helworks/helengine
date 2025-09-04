using helengine;
using helengine.editor;

namespace helengine.editor.launcher {
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

            // Center text on button
            int textWidth = text.Length * 10; // Rough estimation
            int textHeight = 20;
            float3 textOffset = new float3(
                entity.Position.X + (size.X - textWidth) / 2,
                entity.Position.Y + (size.Y - textHeight) / 2,
                entity.Position.Z + 0.1f // Slightly forward
            );
            textEntity.Position = textOffset;

            // Create text component
            textComponent = new TextComponent();
            textComponent.Text = text;
            textComponent.Font = font;
            textComponent.Color = ThemeManager.Colors.TextOnAccent;
            textComponent.Size = new int2(textWidth, textHeight);
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

                case PointerInteraction.None:
                    if (isHovering) {
                        isHovering = false;
                        isPressed = false;
                        UpdateButtonColor();
                    }
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

        public void UpdatePosition(float3 newPosition) {
            if (Parent != null) {
                Parent.Position = newPosition;

                // Update text position to stay centered
                if (textEntity != null) {
                    int textWidth = text.Length * 10;
                    int textHeight = 20;
                    float3 textOffset = new float3(
                        newPosition.X + (size.X - textWidth) / 2,
                        newPosition.Y + (size.Y - textHeight) / 2,
                        newPosition.Z + 0.1f
                    );
                    textEntity.Position = textOffset;
                }
            }
        }

        public void SetText(string newText) {
            text = newText;
            if (textComponent != null) {
                textComponent.Text = newText;
                
                // Recalculate text position for centering
                if (Parent != null && textEntity != null) {
                    int textWidth = text.Length * 10;
                    int textHeight = 20;
                    float3 textOffset = new float3(
                        Parent.Position.X + (size.X - textWidth) / 2,
                        Parent.Position.Y + (size.Y - textHeight) / 2,
                        Parent.Position.Z + 0.1f
                    );
                    textEntity.Position = textOffset;
                    textComponent.Size = new int2(textWidth, textHeight);
                }
            }
        }
    }
}
