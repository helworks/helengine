using helengine;
using helengine.editor;

namespace helengine.editor.launcher {
    public static class ButtonUtility {
        public static ButtonUIElements CreateButton(
            string text,
            float3 position,
            int2 size,
            FontAsset font,
            byte4 backgroundColor,
            byte4 textColor,
            Action<int2, int2, PointerInteraction>? clickHandler = null) {
            
            // Create button background entity
            var buttonEntity = new Entity();
            buttonEntity.LayerMask = 0b1000000000000000; // UI layer
            buttonEntity.Position = position;
            buttonEntity.Enabled = true;

            // Create button sprite component
            var spriteComponent = new SpriteComponent();
            spriteComponent.Texture = TextureUtils.PixelTexture; // Use 1x1 white pixel texture
            spriteComponent.Color = backgroundColor;
            spriteComponent.Size = size;
            spriteComponent.RenderOrder2D = 2;
            buttonEntity.InitComponents();
            buttonEntity.AddComponent(spriteComponent);

            // Create interactable component for click handling
            InteractableComponent? interactableComponent = null;
            if (clickHandler != null) {
                interactableComponent = new InteractableComponent();
                interactableComponent.Size = size;
                interactableComponent.CursorEvent += clickHandler;
                buttonEntity.AddComponent(interactableComponent);
            }

            // Create text entity (positioned relative to button center)
            var textEntity = new Entity();
            textEntity.LayerMask = 0b1000000000000000; // UI layer
            textEntity.InitComponents();

            // Center text on button - approximate text size based on character count
            int textWidth = text.Length * 10; // Rough estimation
            int textHeight = 20;
            float3 textOffset = new float3(
                position.X + (size.X - textWidth) / 2,
                position.Y + (size.Y - textHeight) / 2,
                position.Z + 0.1f // Slightly forward to render on top
            );
            textEntity.Position = textOffset;
            textEntity.Enabled = true;

            // Create text component
            var textComponent = new TextComponent();
            textComponent.Text = text;
            textComponent.Font = font;
            textComponent.Color = textColor;
            textComponent.Size = new int2(textWidth, textHeight);
            textComponent.RenderOrder2D = 3; // Render on top of button
            
            textEntity.AddComponent(textComponent);

            return new ButtonUIElements {
                ButtonEntity = buttonEntity,
                TextEntity = textEntity,
                SpriteComponent = spriteComponent,
                TextComponent = textComponent,
                InteractableComponent = interactableComponent
            };
        }

        public static void SetButtonHoverState(ButtonUIElements button, byte4 hoverColor) {
            if (button.SpriteComponent != null) {
                button.SpriteComponent.Color = hoverColor;
            }
        }

        public static void SetButtonNormalState(ButtonUIElements button, byte4 normalColor) {
            if (button.SpriteComponent != null) {
                button.SpriteComponent.Color = normalColor;
            }
        }

        public static void UpdateButtonPosition(ButtonUIElements button, float3 newPosition) {
            if (button.ButtonEntity != null) {
                button.ButtonEntity.Position = newPosition;
                
                // Update text position to stay centered
                if (button.TextEntity != null && button.SpriteComponent != null) {
                    int textWidth = button.TextComponent?.Text?.Length * 10 ?? 100;
                    int textHeight = 20;
                    float3 textOffset = new float3(
                        newPosition.X + (button.SpriteComponent.Size.X - textWidth) / 2,
                        newPosition.Y + (button.SpriteComponent.Size.Y - textHeight) / 2,
                        newPosition.Z + 0.1f
                    );
                    button.TextEntity.Position = textOffset;
                }
            }
        }
    }

    public class ButtonUIElements {
        public Entity? ButtonEntity { get; set; }
        public Entity? TextEntity { get; set; }
        public SpriteComponent? SpriteComponent { get; set; }
        public TextComponent? TextComponent { get; set; }
        public InteractableComponent? InteractableComponent { get; set; }
    }
}
