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

            // Create sprite component for button background
            spriteComponent = new SpriteComponent();
            spriteComponent.Texture = TextureUtils.PixelTexture;
            spriteComponent.Color = ThemeManager.Colors.AccentSecondary;
            spriteComponent.Size = size;
            spriteComponent.RenderOrder2D = 2;
            entity.AddComponent(spriteComponent);

            // Create child entity for text
            textEntity = new Entity();
            textEntity.LayerMask = 0b1000000000000000;
            textEntity.Enabled = true;
            textEntity.InitComponents();

            // Calculate centered text position using MeasureString
            var textBounds = font.MeasureString(text);
            int textWidth = (int)textBounds.X;
            int textHeight = (int)textBounds.Y;
            
            // Center the text within the button (with proper vertical centering using font metrics)
            int centeredX = (size.X - textWidth) / 2;
            // Use font baseline for better vertical centering
            int centeredY = (size.Y - (int)font.FontInfo.LineHeight) / 2 + (int)font.FontInfo.BaselineOffset;
            
            // Position text entity relative to button entity
            textEntity.Position = entity.Position + new float3(centeredX, centeredY, 0);

            // Create text component
            textComponent = new TextComponent();
            textComponent.Text = text;
            textComponent.Font = font;
            textComponent.Color = ThemeManager.Colors.TextOnAccent;
            textComponent.Size = new int2(textWidth, textHeight);
            textComponent.RenderOrder2D = 3;
            textEntity.AddComponent(textComponent);

            // Create interactable component for mouse events
            interactableComponent = new InteractableComponent();
            interactableComponent.Size = size;
            interactableComponent.CursorEvent += OnCursorEvent;
            entity.AddComponent(interactableComponent);
        }

        public void UpdatePosition(float3 newPosition) {
            if (Parent != null) {
                Parent.Position = newPosition;
                
                // Update text position to maintain centering
                if (textEntity != null) {
                    var textBounds = font.MeasureString(text);
                    int textWidth = (int)textBounds.X;
                    int textHeight = (int)textBounds.Y;
                    
                    int centeredX = (size.X - textWidth) / 2;
                    // Use font baseline for better vertical centering
                    int centeredY = (size.Y - (int)font.FontInfo.LineHeight) / 2 + (int)font.FontInfo.BaselineOffset;
                    
                    textEntity.Position = newPosition + new float3(centeredX, centeredY, 0);
                }
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
                    isHovering = false;
                    UpdateButtonColor();
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

        public override void ComponentRemoved(Entity entity) {
            textEntity?.Dispose();
            base.ComponentRemoved(entity);
        }
    }
}