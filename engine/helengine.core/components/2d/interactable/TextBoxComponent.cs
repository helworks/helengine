namespace helengine {
    /// <summary>
    /// Simple text box with placeholder, blinking cursor, and basic keyboard input handling.
    /// </summary>
    public class TextBoxComponent : Component {
        static TextBoxComponent focusedTextBox;
        string text = "";
        string placeholder = "";
        FontAsset font;
        int2 size;
        bool isFocused;
        bool cursorVisible = true;
        DateTime lastCursorBlink = DateTime.Now;
        int cursorPosition;
        
        // Child components
        RoundedRectComponent backgroundSprite;
        TextComponent textComponent;
        InteractableComponent interactableComponent;

        /// <summary>
        /// Raised when the text box submits its value.
        /// </summary>
        public event Action<TextBoxComponent> Submitted;

        /// <summary>
        /// Raised when the focus state changes.
        /// </summary>
        public event Action<TextBoxComponent, bool> FocusChanged;
        
        /// <summary>
        /// Gets or sets the text content.
        /// </summary>
        public string Text {
            get { return text; }
            set {
                text = value ?? "";
                cursorPosition = Math.Min(cursorPosition, text.Length);
                UpdateTextDisplay();
            }
        }

        /// <summary>
        /// Gets or sets placeholder text shown when empty and not focused.
        /// </summary>
        public string Placeholder {
            get { return placeholder; }
            set { 
                placeholder = value ?? "";
                UpdateTextDisplay();
            }
        }

        /// <summary>
        /// Gets or sets the font used for rendering text.
        /// </summary>
        public FontAsset Font {
            get { return font; }
            set { 
                font = value;
                if (textComponent != null) {
                    textComponent.Font = font;
                }
            }
        }

        /// <summary>
        /// Gets or sets the size of the text box.
        /// </summary>
        public int2 Size {
            get { return size; }
            set { 
                size = value;
                if (backgroundSprite != null) backgroundSprite.Size = size;
                if (interactableComponent != null) {
                    interactableComponent.Size = size;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the text box has input focus.
        /// </summary>
        public bool IsFocused {
            get { return isFocused; }
            set {
                if (isFocused == value) {
                    return;
                }

                isFocused = value;
                if (isFocused) {
                    cursorPosition = text.Length; // Move cursor to end when focused
                    focusedTextBox = this;
                } else if (focusedTextBox == this) {
                    focusedTextBox = null;
                }
                cursorVisible = true;
                UpdateTextDisplay();
                FocusChanged?.Invoke(this, isFocused);
                if (!isFocused) {
                    Submitted?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// Creates a new text box with size, font, and optional placeholder.
        /// </summary>
        /// <param name="size">Dimensions of the text box.</param>
        /// <param name="font">Font used to render text.</param>
        /// <param name="placeholder">Placeholder string.</param>
        public TextBoxComponent(int2 size, FontAsset font, string placeholder = "") {
            this.size = size;
            this.font = font;
            this.placeholder = placeholder;
        }

        /// <summary>
        /// Builds child components and registers input handling when added.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            byte backgroundOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(1);
            byte textOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);

            // Create rounded background
            backgroundSprite = new RoundedRectComponent();
            backgroundSprite.Size = size;
            backgroundSprite.Radius = MathF.Min(size.X, size.Y) * 0.15f;
            backgroundSprite.BorderThickness = 2f;
            backgroundSprite.FillColor = ThemeManager.Colors.SurfaceInput;
            backgroundSprite.BorderColor = ThemeManager.Colors.AccentTertiary;
            backgroundSprite.RenderOrder2D = backgroundOrder;
            entity.AddComponent(backgroundSprite);

            // Create text component
            textComponent = new TextComponent();
            textComponent.Font = font;
            textComponent.Color = new byte4(255, 255, 255, 255);
            textComponent.RenderOrder2D = textOrder;
            entity.AddComponent(textComponent);

            // Create interactable component for mouse clicks
            interactableComponent = new InteractableComponent();
            interactableComponent.Size = size;
            interactableComponent.CursorEvent += OnCursorEvent;
            entity.AddComponent(interactableComponent);

            // Create a custom update component for keyboard input
            var updateComponent = new TextBoxUpdateComponent(this);
            updateComponent.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
            entity.AddComponent(updateComponent);

            UpdateTextDisplay();
        }

        /// <summary>
        /// Handles pointer presses to focus the text box.
        /// </summary>
        void OnCursorEvent(int2 relPos, int2 delta, PointerInteraction state) {
            if (state == PointerInteraction.Press) {
                if (focusedTextBox != null && focusedTextBox != this) {
                    focusedTextBox.IsFocused = false;
                }
                IsFocused = true;
                cursorPosition = text.Length; // For now, just move to end
            }
        }

        /// <summary>
        /// Handles blinking, key input, and updates the displayed text each frame.
        /// </summary>
        public void Update() {
            if (!isFocused) return;

            // Handle cursor blinking
            if ((DateTime.Now - lastCursorBlink).TotalMilliseconds > 500) {
                cursorVisible = !cursorVisible;
                lastCursorBlink = DateTime.Now;
                UpdateTextDisplay();
            }

            // Handle keyboard input
            var inputManager = Core.Instance.InputManager;
            bool isShiftPressed = inputManager.IsKeyDown(Keys.LeftShift) || inputManager.IsKeyDown(Keys.RightShift);
            
            // Process newly pressed keys
            for (int i = 0; i < 255; i++) {
                Keys key = (Keys)i;
                if (inputManager.WasKeyPressed(key)) {
                    HandleKeyPress(key, isShiftPressed);
                }
            }
        }

        /// <summary>
        /// Processes a key press to modify text, move the cursor, or delete characters.
        /// </summary>
        /// <param name="key">Key pressed.</param>
        /// <param name="isShiftPressed">True when either shift key is pressed.</param>
        void HandleKeyPress(Keys key, bool isShiftPressed) {
            switch (key) {
                case Keys.Back:
                    if (cursorPosition > 0) {
                        text = text.Remove(cursorPosition - 1, 1);
                        cursorPosition--;
                        UpdateTextDisplay();
                    }
                    break;
                    
                case Keys.Delete:
                    if (cursorPosition < text.Length) {
                        text = text.Remove(cursorPosition, 1);
                        UpdateTextDisplay();
                    }
                    break;
                    
                case Keys.Left:
                    cursorPosition = Math.Max(0, cursorPosition - 1);
                    UpdateTextDisplay();
                    break;
                    
                case Keys.Right:
                    cursorPosition = Math.Min(text.Length, cursorPosition + 1);
                    UpdateTextDisplay();
                    break;
                    
                case Keys.Home:
                    cursorPosition = 0;
                    UpdateTextDisplay();
                    break;
                    
                case Keys.End:
                    cursorPosition = text.Length;
                    UpdateTextDisplay();
                    break;
                case Keys.Enter:
                    Submitted?.Invoke(this);
                    IsFocused = false;
                    break;
                    
                default:
                    char character = KeyToChar(key, isShiftPressed);
                    if (character != '\0') {
                        text = text.Insert(cursorPosition, character.ToString());
                        cursorPosition++;
                        UpdateTextDisplay();
                    }
                    break;
            }
        }

        /// <summary>
        /// Converts a key and shift state into a printable character.
        /// </summary>
        /// <param name="key">Keyboard key.</param>
        /// <param name="isShiftPressed">True if shift is pressed.</param>
        /// <returns>Printable character or '\0' if not mapped.</returns>
        char KeyToChar(Keys key, bool isShiftPressed) {
            // Handle letters
            if (key >= Keys.A && key <= Keys.Z) {
                char baseChar = (char)('a' + (key - Keys.A));
                return isShiftPressed ? char.ToUpper(baseChar) : baseChar;
            }
            
            // Handle numbers and symbols
            switch (key) {
                case Keys.D0: return isShiftPressed ? ')' : '0';
                case Keys.D1: return isShiftPressed ? '!' : '1';
                case Keys.D2: return isShiftPressed ? '@' : '2';
                case Keys.D3: return isShiftPressed ? '#' : '3';
                case Keys.D4: return isShiftPressed ? '$' : '4';
                case Keys.D5: return isShiftPressed ? '%' : '5';
                case Keys.D6: return isShiftPressed ? '^' : '6';
                case Keys.D7: return isShiftPressed ? '&' : '7';
                case Keys.D8: return isShiftPressed ? '*' : '8';
                case Keys.D9: return isShiftPressed ? '(' : '9';
                case Keys.Space: return ' ';
                case Keys.OemPeriod: return isShiftPressed ? '>' : '.';
                case Keys.OemComma: return isShiftPressed ? '<' : ',';
                case Keys.OemMinus: return isShiftPressed ? '_' : '-';
                case Keys.OemPlus: return isShiftPressed ? '+' : '=';
                case Keys.OemQuestion: return isShiftPressed ? '?' : '/';
                case Keys.OemSemicolon: return isShiftPressed ? ':' : ';';
                case Keys.OemQuotes: return isShiftPressed ? '"' : '\'';
                case Keys.OemOpenBrackets: return isShiftPressed ? '{' : '[';
                case Keys.OemCloseBrackets: return isShiftPressed ? '}' : ']';
                case Keys.OemPipe: return isShiftPressed ? '|' : '\\';
                default: return '\0';
            }
        }

        /// <summary>
        /// Updates displayed text, placeholder coloring, and cursor.
        /// </summary>
        void UpdateTextDisplay() {
            if (textComponent == null) return;

            // Display text or placeholder; hide placeholder when focused
            bool showPlaceholder = string.IsNullOrEmpty(text) && !isFocused;
            string displayText = showPlaceholder ? placeholder : text;
            
            // Add cursor if focused and visible
            if (isFocused && cursorVisible) {
                displayText = displayText.Insert(cursorPosition, "|");
            }
            
            textComponent.Text = displayText;
            
            // Set color based on whether it's placeholder or real text
            if (showPlaceholder) {
                textComponent.Color = new byte4(150, 150, 150, 255); // Gray for placeholder
            } else {
                textComponent.Color = new byte4(255, 255, 255, 255); // White for text
            }
        }

        /// <summary>
        /// Clears focus when parent is disabled.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled && focusedTextBox == this) {
                IsFocused = false;
            }
        }

        /// <summary>
        /// Removes focus reference when component is removed.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            if (focusedTextBox == this) {
                focusedTextBox = null;
            }
        }

        /// <summary>
        /// Determines whether a pointer is inside the text box bounds.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <returns>True when the pointer is inside the text box.</returns>
        public bool ContainsPointer(int2 pointer) {
            if (Parent == null) {
                return false;
            }

            float3 worldPosition = Parent.Position;
            return pointer.X >= worldPosition.X &&
                   pointer.X < worldPosition.X + size.X &&
                   pointer.Y >= worldPosition.Y &&
                   pointer.Y < worldPosition.Y + size.Y;
        }
    }

    /// <summary>
    /// Helper update component that forwards updates to its owning text box.
    /// </summary>
    class TextBoxUpdateComponent : UpdateComponent {
        TextBoxComponent textBox;

        /// <summary>
        /// Creates a forwarding update component for the given text box.
        /// </summary>
        /// <param name="textBox">Text box to drive.</param>
        public TextBoxUpdateComponent(TextBoxComponent textBox) {
            this.textBox = textBox;
        }

        /// <summary>
        /// Forwards update calls to the text box.
        /// </summary>
        public override void Update() {
            textBox.Update();

            if (!textBox.IsFocused) {
                return;
            }

            InputManager input = Core.Instance.InputManager;
            if (!input.WasMouseLeftButtonPressed()) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (!textBox.ContainsPointer(pointer)) {
                textBox.IsFocused = false;
            }
        }
    }
}
