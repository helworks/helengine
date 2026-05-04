namespace helengine {
    /// <summary>
    /// Simple text box with placeholder, blinking cursor, and basic keyboard input handling.
    /// </summary>
    public class TextBoxComponent : Component, IFocusTarget {
        /// <summary>
        /// Horizontal padding between the textbox border and its text content.
        /// </summary>
        const int TextPaddingX = 8;
        /// <summary>
        /// Fixed per-frame time step used by transient textbox feedback effects.
        /// </summary>
        const float EffectFrameDeltaSeconds = 1f / 60f;
        /// <summary>
        /// Total duration used by the invalid-input shake effect.
        /// </summary>
        const float ShakeDurationSeconds = 0.3f;
        /// <summary>
        /// Peak horizontal amplitude used by the invalid-input shake effect.
        /// </summary>
        const float ShakeAmplitudePixels = 10f;
        /// <summary>
        /// Oscillation frequency used by the invalid-input shake effect.
        /// </summary>
        const float ShakeFrequencyHz = 16f;

        static TextBoxComponent focusedTextBox;
        readonly TextBoxEditState EditState;
        string placeholder = "";
        FontAsset font;
        int2 size;
        bool isFocused;
        bool cursorVisible = true;
        DateTime lastCursorBlink = DateTime.Now;
        bool hasRenderOrderOverrides;
        byte backgroundRenderOrder;
        byte textRenderOrder;
        bool isInvalid;
        /// <summary>
        /// Tracks whether the mouse is currently dragging a text selection inside the textbox.
        /// </summary>
        bool isSelectingText;
        bool isShakeActive;
        float shakeElapsedSeconds;
        float currentShakeOffsetX;
        float3 shakeBaseLocalPosition;
        
        // Child components
        RoundedRectComponent backgroundSprite;
        /// <summary>
        /// Hosts the translucent rectangle used to visualize the active text selection.
        /// </summary>
        Entity selectionEntity;
        /// <summary>
        /// Draws the active text-selection highlight behind the caret and glyphs.
        /// </summary>
        RoundedRectComponent selectionSprite;
        Entity textEntity;
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
        /// Raised when the text content changes.
        /// </summary>
        public event Action<TextBoxComponent> TextChanged;

        /// <summary>
        /// Gets or sets the focus group that owns this text box during keyboard traversal.
        /// </summary>
        public IFocusGroup FocusGroup { get; set; }

        /// <summary>
        /// Gets or sets the traversal order of this text box within its focus group.
        /// </summary>
        public int TabIndex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this text box is the preferred entry target for its root group.
        /// </summary>
        public bool IsDefaultTarget { get; set; }

        /// <summary>
        /// Gets whether this text box can currently receive keyboard focus.
        /// </summary>
        public bool CanReceiveFocus => Parent != null && Parent.IsHierarchyEnabled && interactableComponent != null;
        
        /// <summary>
        /// Gets or sets the text content.
        /// </summary>
        public string Text {
            get { return EditState.Text; }
            set {
                string previousText = EditState.Text;
                EditState.Text = value ?? "";
                UpdateTextDisplay();
                if (previousText != EditState.Text) {
                    TextChanged?.Invoke(this);
                }
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

                UpdateTextLayout();
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

                UpdateTextLayout();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the text box has input focus.
        /// </summary>
        public bool IsFocused {
            get { return isFocused; }
            set { SetFocusedState(value, true); }
        }

        /// <summary>
        /// Gets the transient horizontal shake offset currently applied for invalid-input feedback.
        /// Layout systems can use this to preserve the shake during host relayout.
        /// </summary>
        public float CurrentShakeOffsetX => currentShakeOffsetX;

        /// <summary>
        /// Overrides the render order used for the textbox background and text.
        /// </summary>
        /// <param name="backgroundOrder">Render order for the textbox background.</param>
        /// <param name="textOrder">Render order for textbox text.</param>
        public void SetRenderOrders(byte backgroundOrder, byte textOrder) {
            hasRenderOrderOverrides = true;
            backgroundRenderOrder = backgroundOrder;
            textRenderOrder = textOrder;

            if (backgroundSprite != null) {
                backgroundSprite.RenderOrder2D = backgroundOrder;
            }

            if (textComponent != null) {
                textComponent.RenderOrder2D = textOrder;
            }
        }

        /// <summary>
        /// Applies or clears the invalid visual state without changing the current text or focus state.
        /// </summary>
        /// <param name="isInvalid">True when the text box should use the invalid border color.</param>
        public void SetInvalidState(bool isInvalid) {
            this.isInvalid = isInvalid;
            UpdateFocusVisual();
        }

        /// <summary>
        /// Starts a short horizontal shake that provides invalid-input feedback while preserving the textbox layout base position.
        /// </summary>
        public void TriggerInvalidShake() {
            if (Parent == null) {
                throw new InvalidOperationException("Text boxes must be attached to an entity before they can animate invalid-input feedback.");
            }

            if (isShakeActive) {
                Parent.LocalPosition = shakeBaseLocalPosition;
                currentShakeOffsetX = 0f;
            }

            shakeBaseLocalPosition = Parent.LocalPosition;
            shakeElapsedSeconds = 0f;
            isShakeActive = true;
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
            EditState = new TextBoxEditState();
        }

        /// <summary>
        /// Builds child components and registers input handling when added.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            byte backgroundOrder = RenderOrder2D.PanelSurface;
            byte textOrder = RenderOrder2D.PanelForeground;
            if (hasRenderOrderOverrides) {
                backgroundOrder = backgroundRenderOrder;
                textOrder = textRenderOrder;
            }

            // Create rounded background
            backgroundSprite = new RoundedRectComponent();
            backgroundSprite.Size = size;
            backgroundSprite.Radius = MathF.Min(size.X, size.Y) * 0.15f;
            backgroundSprite.BorderThickness = 2f;
            backgroundSprite.FillColor = ThemeManager.Colors.SurfaceInput;
            backgroundSprite.BorderColor = ThemeManager.Colors.AccentTertiary;
            backgroundSprite.RenderOrder2D = backgroundOrder;
            entity.AddComponent(backgroundSprite);

            // Create selection highlight so dragged text can be shown behind the caret and text.
            selectionEntity = new Entity();
            selectionEntity.LayerMask = entity.LayerMask;
            selectionEntity.Enabled = true;
            selectionEntity.InitComponents();
            if (entity.Children == null) {
                entity.InitChildren();
            }

            entity.AddChild(selectionEntity);

            selectionSprite = new RoundedRectComponent();
            selectionSprite.Radius = 2f;
            selectionSprite.BorderThickness = 0f;
            selectionSprite.FillColor = new byte4(
                ThemeManager.Colors.AccentPrimary.X,
                ThemeManager.Colors.AccentPrimary.Y,
                ThemeManager.Colors.AccentPrimary.Z,
                96);
            selectionSprite.BorderColor = selectionSprite.FillColor;
            selectionSprite.RenderOrder2D = textOrder;
            selectionEntity.AddComponent(selectionSprite);

            // Create text component
            textEntity = new Entity();
            textEntity.LayerMask = entity.LayerMask;
            textEntity.Enabled = true;
            textEntity.InitComponents();
            if (entity.Children == null) {
                entity.InitChildren();
            }

            entity.AddChild(textEntity);

            textComponent = new TextComponent();
            textComponent.Font = font;
            textComponent.Color = new byte4(255, 255, 255, 255);
            textComponent.RenderOrder2D = textOrder;
            textEntity.AddComponent(textComponent);

            // Create interactable component for mouse clicks
            interactableComponent = new InteractableComponent();
            interactableComponent.HoverCursor = PointerCursorKind.Text;
            interactableComponent.Size = size;
            interactableComponent.CursorEvent += OnCursorEvent;
            entity.AddComponent(interactableComponent);

            // Create a custom update component for keyboard input
            var updateComponent = new TextBoxUpdateComponent(this);
            updateComponent.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
            entity.AddComponent(updateComponent);

            UpdateTextDisplay();
            UpdateFocusVisual();
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
                int cursorPosition = ResolveCursorPositionFromClick(relPos.X);
                EditState.SetSelection(cursorPosition, cursorPosition);
                isSelectingText = true;
                UpdateTextDisplay();
            } else if (state == PointerInteraction.Hover && isSelectingText) {
                EditState.CursorPosition = ResolveCursorPositionFromClick(relPos.X);
                UpdateTextDisplay();
            } else if (state == PointerInteraction.Release) {
                isSelectingText = false;
                UpdateTextDisplay();
            }
        }

        /// <summary>
        /// Handles blinking, key input, and updates the displayed text each frame.
        /// </summary>
        public void Update() {
            UpdateShakeAnimation();

            if (!isFocused) return;

            // Handle cursor blinking
            if ((DateTime.Now - lastCursorBlink).TotalMilliseconds > 500) {
                cursorVisible = !cursorVisible;
                lastCursorBlink = DateTime.Now;
                UpdateTextDisplay();
            }

            // Handle keyboard input
            var inputManager = Core.Instance.Input;
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
        /// Advances the short invalid-input shake effect and restores the original layout position when it finishes.
        /// </summary>
        void UpdateShakeAnimation() {
            if (!isShakeActive || Parent == null) {
                return;
            }

            shakeElapsedSeconds += EffectFrameDeltaSeconds;
            if (shakeElapsedSeconds >= ShakeDurationSeconds) {
                Parent.LocalPosition = shakeBaseLocalPosition;
                currentShakeOffsetX = 0f;
                isShakeActive = false;
                return;
            }

            double progress = shakeElapsedSeconds / ShakeDurationSeconds;
            double amplitude = ShakeAmplitudePixels * (1d - progress);
            double angle = shakeElapsedSeconds * ShakeFrequencyHz * Math.PI * 2d;
            double offset = Math.Sin(angle) * amplitude;

            currentShakeOffsetX = (float)offset;
            Parent.LocalPosition = new float3(
                shakeBaseLocalPosition.X + currentShakeOffsetX,
                shakeBaseLocalPosition.Y,
                shakeBaseLocalPosition.Z);
        }

        /// <summary>
        /// Processes a key press to modify text, move the cursor, or delete characters.
        /// </summary>
        /// <param name="key">Key pressed.</param>
        /// <param name="isShiftPressed">True when either shift key is pressed.</param>
        void HandleKeyPress(Keys key, bool isShiftPressed) {
            bool textChanged = false;
            bool layoutChanged = false;
            switch (key) {
                case Keys.Back:
                    string previousBackspaceText = EditState.Text;
                    EditState.Backspace();
                    textChanged = previousBackspaceText != EditState.Text;
                    break;
                    
                case Keys.Delete:
                    string previousDeleteText = EditState.Text;
                    EditState.Delete();
                    textChanged = previousDeleteText != EditState.Text;
                    break;
                    
                case Keys.Left:
                    int previousLeftCursor = EditState.CursorPosition;
                    bool previousLeftSelection = EditState.HasSelection;
                    EditState.MoveCursorLeft();
                    layoutChanged = previousLeftCursor != EditState.CursorPosition || previousLeftSelection != EditState.HasSelection;
                    break;
                    
                case Keys.Right:
                    int previousRightCursor = EditState.CursorPosition;
                    bool previousRightSelection = EditState.HasSelection;
                    EditState.MoveCursorRight();
                    layoutChanged = previousRightCursor != EditState.CursorPosition || previousRightSelection != EditState.HasSelection;
                    break;
                    
                case Keys.Home:
                    int previousHomeCursor = EditState.CursorPosition;
                    bool previousHomeSelection = EditState.HasSelection;
                    EditState.SetCursorToStart();
                    layoutChanged = previousHomeCursor != EditState.CursorPosition || previousHomeSelection != EditState.HasSelection;
                    break;
                    
                case Keys.End:
                    int previousEndCursor = EditState.CursorPosition;
                    bool previousEndSelection = EditState.HasSelection;
                    EditState.SetCursorToEnd();
                    layoutChanged = previousEndCursor != EditState.CursorPosition || previousEndSelection != EditState.HasSelection;
                    break;
                case Keys.Enter:
                    IsFocused = false;
                    break;
                    
                default:
                    char character = KeyToChar(key, isShiftPressed);
                    if (character != '\0') {
                        string previousText = EditState.Text;
                        EditState.InsertCharacter(character);
                        textChanged = previousText != EditState.Text;
                    }
                    break;
            }

            if (textChanged || layoutChanged) {
                UpdateTextDisplay();
            }

            if (textChanged) {
                TextChanged?.Invoke(this);
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
            bool showPlaceholder = string.IsNullOrEmpty(EditState.Text) && !isFocused;
            string displayText = showPlaceholder ? placeholder : EditState.Text;
            
            // Add cursor if focused and visible
            if (isFocused && cursorVisible) {
                int cursorIndex = Math.Max(0, Math.Min(EditState.CursorPosition, displayText.Length));
                displayText = displayText.Insert(cursorIndex, "|");
            }
            
            textComponent.Text = displayText;
            
            // Set color based on whether it's placeholder or real text
            if (showPlaceholder) {
                textComponent.Color = new byte4(150, 150, 150, 255); // Gray for placeholder
            } else {
                textComponent.Color = new byte4(255, 255, 255, 255); // White for text
            }

            UpdateTextLayout();
        }

        /// <summary>
        /// Resolves the caret position for a pointer click within the textbox text area.
        /// </summary>
        /// <param name="clickX">Pointer X position relative to the textbox bounds.</param>
        /// <returns>Caret index that best matches the clicked text position.</returns>
        int ResolveCursorPositionFromClick(int clickX) {
            if (Font == null) {
                return 0;
            }

            string text = EditState.Text;
            if (string.IsNullOrEmpty(text)) {
                return 0;
            }

            double textX = Math.Max(0.0, (double)clickX - TextPaddingX);
            double cursorX = 0.0;
            for (int index = 0; index < text.Length; index++) {
                double advance = ResolveCharacterAdvance(text[index]);
                if (textX < cursorX + (advance * 0.5)) {
                    return index;
                }

                cursorX += advance;
            }

            return text.Length;
        }

        /// <summary>
        /// Resolves the horizontal advance used for one character when placing the caret.
        /// </summary>
        /// <param name="character">Character to measure.</param>
        /// <returns>Advance width in pixels.</returns>
        double ResolveCharacterAdvance(char character) {
            if (character == ' ') {
                return Math.Max((double)Font.FontInfo.SpaceWidth, 1.0);
            }

            FontChar glyph;
            if (Font.Characters != null && Font.Characters.TryGetValue(character, out glyph)) {
                if (glyph.AdvanceWidth > 0f) {
                    return glyph.AdvanceWidth;
                }

                double sourceWidth = (double)glyph.SourceRect.Z;
                if (sourceWidth > 0.0) {
                    return sourceWidth;
                }
            }

            return 1.0;
        }

        /// <summary>
        /// Positions the textbox text host with shared left padding and vertically centers it using the font line height.
        /// </summary>
        void UpdateTextLayout() {
            if (textEntity == null || textComponent == null || font == null) {
                return;
            }

            double lineHeight = Math.Max((double)font.LineHeight, 1.0);
            double textY = Math.Round((size.Y - lineHeight) / 2.0, MidpointRounding.AwayFromZero);
            FontTightMetrics textMetrics = font.MeasureTight(textComponent.Text);
            textEntity.Position = new float3(TextPaddingX, (float)textY, 0.1f);
            textComponent.Size = new int2(
                (int)Math.Ceiling(textMetrics.Width),
                (int)Math.Ceiling(lineHeight));
            UpdateSelectionVisual(textY, lineHeight);
        }

        /// <summary>
        /// Clears focus when parent is disabled.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled && isFocused) {
                IsFocused = false;
            }

            if (textEntity != null) {
                textEntity.Enabled = newEnabled;
            }
        }

        /// <summary>
        /// Removes focus reference when component is removed.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            SetFocusedState(false, false);
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the text box bounds.
        /// </summary>
        /// <param name="x">Pointer X coordinate in window coordinates.</param>
        /// <param name="y">Pointer Y coordinate in window coordinates.</param>
        /// <returns>True when the point is inside the text box.</returns>
        public bool ContainsScreenPoint(int x, int y) {
            if (Parent == null) {
                return false;
            }

            float3 worldPosition = Parent.Position;
            return x >= worldPosition.X &&
                   x < worldPosition.X + size.X &&
                   y >= worldPosition.Y &&
                   y < worldPosition.Y + size.Y;
        }

        /// <summary>
        /// Applies or clears keyboard focus using the text box's existing focus semantics.
        /// </summary>
        /// <param name="isFocused">True when the text box should become focused.</param>
        public void SetTargetFocused(bool isFocused) {
            IsFocused = isFocused;
        }

        /// <summary>
        /// Returns true when the text box should activate for the provided key.
        /// </summary>
        /// <param name="key">Activation key to evaluate.</param>
        /// <returns>True when Enter should commit the text box.</returns>
        public bool CanActivateWithKey(Keys key) {
            return key == Keys.Enter;
        }

        /// <summary>
        /// Commits the text box by using the normal blur path for supported activation keys.
        /// </summary>
        /// <param name="key">Activation key routed to the text box.</param>
        public void ActivateFromKey(Keys key) {
            if (!CanActivateWithKey(key) || !isFocused) {
                return;
            }

            IsFocused = false;
        }

        /// <summary>
        /// Applies one focus-state transition and optionally submits the current value on blur.
        /// </summary>
        /// <param name="value">Focused state to apply.</param>
        /// <param name="submitOnBlur">True when losing focus should submit the text value.</param>
        void SetFocusedState(bool value, bool submitOnBlur) {
            if (isFocused == value) {
                if (!value) {
                    isSelectingText = false;
                    EditState.ClearSelection();
                    UpdateSelectionVisual();
                }
                UpdateFocusVisual();
                return;
            }

            isFocused = value;
            if (isFocused) {
                EditState.SetCursorToEnd();
                focusedTextBox = this;
            } else if (focusedTextBox == this) {
                focusedTextBox = null;
                isSelectingText = false;
                EditState.ClearSelection();
            }

            cursorVisible = true;
            UpdateTextDisplay();
            UpdateFocusVisual();
            FocusChanged?.Invoke(this, isFocused);
            if (!isFocused && submitOnBlur) {
                Submitted?.Invoke(this);
            }
        }

        /// <summary>
        /// Updates the text-box outline to reflect whether it currently has keyboard focus.
        /// </summary>
        void UpdateFocusVisual() {
            if (backgroundSprite == null) {
                return;
            }

            if (isInvalid) {
                backgroundSprite.BorderColor = ThemeManager.Colors.StateDanger;
                return;
            }

            backgroundSprite.BorderColor = isFocused
                ? ThemeManager.Colors.AccentPrimary
                : ThemeManager.Colors.AccentTertiary;
        }

        /// <summary>
        /// Updates the selection highlight geometry so dragged text is shown behind the caret.
        /// </summary>
        /// <param name="textY">Vertical offset used for the textbox text host.</param>
        /// <param name="lineHeight">Current text line height.</param>
        void UpdateSelectionVisual(double textY, double lineHeight) {
            if (selectionEntity == null || selectionSprite == null || font == null) {
                return;
            }

            if (!isFocused || !EditState.HasSelection || string.IsNullOrEmpty(EditState.Text)) {
                selectionSprite.Size = new int2(0, 0);
                selectionSprite.FillColor = new byte4(
                    ThemeManager.Colors.AccentPrimary.X,
                    ThemeManager.Colors.AccentPrimary.Y,
                    ThemeManager.Colors.AccentPrimary.Z,
                    0);
                selectionEntity.Position = new float3(TextPaddingX, (float)textY, 0.05f);
                return;
            }

            double selectionStartX = ResolveTextWidth(0, EditState.SelectionStart);
            double selectionWidth = ResolveTextWidth(EditState.SelectionStart, EditState.SelectionEnd);
            selectionEntity.Position = new float3(
                TextPaddingX + (float)selectionStartX,
                (float)textY,
                0.05f);
            selectionSprite.Size = new int2(
                (int)Math.Ceiling(selectionWidth),
                (int)Math.Ceiling(lineHeight));
            selectionSprite.FillColor = new byte4(
                ThemeManager.Colors.AccentPrimary.X,
                ThemeManager.Colors.AccentPrimary.Y,
                ThemeManager.Colors.AccentPrimary.Z,
                96);
        }

        /// <summary>
        /// Updates the selection highlight using the current text layout metrics.
        /// </summary>
        void UpdateSelectionVisual() {
            if (font == null) {
                return;
            }

            double lineHeight = Math.Max((double)font.LineHeight, 1.0);
            double textY = Math.Round((size.Y - lineHeight) / 2.0, MidpointRounding.AwayFromZero);
            UpdateSelectionVisual(textY, lineHeight);
        }

        /// <summary>
        /// Measures the width of a substring using the configured font metrics.
        /// </summary>
        /// <param name="startIndex">First character index to include.</param>
        /// <param name="endIndex">Character index immediately after the final character to include.</param>
        /// <returns>Measured width in pixels.</returns>
        double ResolveTextWidth(int startIndex, int endIndex) {
            if (Font == null) {
                return 0.0;
            }

            string text = EditState.Text;
            int clampedStart = Math.Max(0, Math.Min(startIndex, text.Length));
            int clampedEnd = Math.Max(0, Math.Min(endIndex, text.Length));
            double width = 0.0;
            for (int index = clampedStart; index < clampedEnd; index++) {
                width += ResolveCharacterAdvance(text[index]);
            }

            return width;
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

            InputSystem input = Core.Instance.Input;
            if (!input.WasMouseLeftButtonPressed()) {
                return;
            }

            int pointerX = input.GetMouseX();
            int pointerY = input.GetMouseY();
            if (!textBox.ContainsScreenPoint(pointerX, pointerY)) {
                textBox.IsFocused = false;
            }
        }
    }
}


