namespace helengine {
    /// <summary>
    /// Simple text box with placeholder, blinking cursor, and basic keyboard input handling.
    /// </summary>
    [RunInEditor]
    public class TextBoxComponent : Component, IFocusTarget {
        /// <summary>Optional system typography propagated to the existing text presentation.</summary>
        [NativeOwnedMember] SystemTextStyle SystemTextStyleValue;
        /// <summary>Stores explicit glyph density independently of reference-canvas scaling.</summary>
        float FontScaleValue = 1f;
        /// <summary>Gets or sets a finite positive glyph scale; one preserves existing bitmap and system typography defaults.</summary>
        public float FontScale {
            get { return FontScaleValue; }
            set {
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f || value > 64f) { throw new ArgumentOutOfRangeException(nameof(value)); }
                FontScaleValue = value;
                if (TextComponent != null) { TextComponent.FontScale = value; }
                UpdateTextLayout();
            }
        }
        /// <summary>Gets or sets the platform style for text and placeholder. Native keyboard activation is supplied separately by the host.</summary>
        [ScenePersistenceIgnore]
        public SystemTextStyle SystemTextStyle {
            get { return SystemTextStyleValue; }
            set {
                if (ReferenceEquals(value, SystemTextStyleValue)) { return; }
                SystemTextStyle replacement = value == null ? null : new SystemTextStyle(value.Role, value.Weight, value.Rounded, value.PointSize);
                NativeOwnership.Release(ref SystemTextStyleValue);
                SystemTextStyleValue = replacement;
                if (TextComponent != null) { TextComponent.SystemTextStyle = value; }
                UpdateTextLayout();
            }
        }
        /// <summary>Releases owned input and style without accessing presentation children, which entity teardown may already have destroyed.</summary>
        public override void Dispose() {
            NativeOwnership.DisposeAndRelease(ref NativeSession);
            if (FocusedTextBox == this) { FocusedTextBox = null; }
            IsFocusedValue = false;
            IsSelectingText = false;
            NativeOwnership.Release(ref SystemTextStyleValue);
            base.Dispose();
        }
        /// <summary>
        /// Horizontal padding between the textbox border and its text content.
        /// </summary>
        const int TextPaddingX = 8;
        /// <summary>Owns native input only while this attached control has focus.</summary>
        [NativeOwnedMember] ITextEntrySession NativeSession;
        /// <summary>Last coherent native snapshot revision applied to this control.</summary>
        int NativeRevision;
        /// <summary>Horizontal layout origin kept visible while the complete Unicode string remains shaped.</summary>
        float TextScrollX;
        /// <summary>Hosts the independently rendered caret; glyphs never contain a synthetic caret character.</summary>
        Entity CaretEntity;
        /// <summary>Draws the caret without disturbing shaping or composition.</summary>
        RoundedRectComponent CaretSprite;
        /// <summary>Hosts the underline for a native marked composition.</summary>
        Entity MarkedEntity;
        /// <summary>Draws the current marked span independently of text glyphs.</summary>
        RoundedRectComponent MarkedSprite;
        /// <summary>Stores the text foreground while preserving existing white defaults.</summary>
        byte4 TextColorValue = new byte4(255, 255, 255, 255);
        /// <summary>Stores the existing muted placeholder foreground.</summary>
        byte4 PlaceholderColorValue = new byte4(150, 150, 150, 255);
        /// <summary>Stores the caret foreground independently from glyph tint.</summary>
        byte4 CaretColorValue = new byte4(255, 255, 255, 255);
        /// <summary>Stores the idle background role selected when the control is created.</summary>
        byte4 FillColorValue;
        /// <summary>Stores the idle border role independently from focus.</summary>
        byte4 BorderColorValue;
        /// <summary>Stores the focused border role.</summary>
        byte4 FocusedBorderColorValue;
        /// <summary>Stores selection fill including its authored alpha.</summary>
        byte4 SelectionColorValue;
        /// <summary>Gets or sets the text foreground.</summary>
        public byte4 TextColor { get { return TextColorValue; } set { TextColorValue = value; UpdateTextDisplay(); } }
        /// <summary>Gets or sets the empty unfocused placeholder foreground.</summary>
        public byte4 PlaceholderColor { get { return PlaceholderColorValue; } set { PlaceholderColorValue = value; UpdateTextDisplay(); } }
        /// <summary>Gets or sets the independently drawn caret and marked-span foreground.</summary>
        public byte4 CaretColor { get { return CaretColorValue; } set { CaretColorValue = value; UpdateTextLayout(); } }
        /// <summary>Gets or sets the background surface without changing focus behavior.</summary>
        public byte4 FillColor { get { return FillColorValue; } set { FillColorValue = value; if (BackgroundSprite != null) { BackgroundSprite.FillColor = value; } } }
        /// <summary>Gets or sets the ordinary border color.</summary>
        public byte4 BorderColor { get { return BorderColorValue; } set { BorderColorValue = value; UpdateFocusVisual(); } }
        /// <summary>Gets or sets the focused border color.</summary>
        public byte4 FocusedBorderColor { get { return FocusedBorderColorValue; } set { FocusedBorderColorValue = value; UpdateFocusVisual(); } }
        /// <summary>Gets or sets selection fill, including opacity.</summary>
        public byte4 SelectionColor { get { return SelectionColorValue; } set { SelectionColorValue = value; UpdateTextLayout(); } }
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

        static TextBoxComponent FocusedTextBox;

        /// <summary>
        /// The text box currently receiving typed characters, or null when no text box is focused. Editor shortcut
        /// dispatch consults this so single-key commands stay quiet while the user types.
        /// </summary>
        public static TextBoxComponent FocusedTextEntry {
            get { return FocusedTextBox; }
        }
        readonly TextBoxEditState EditState;
        string PlaceholderValue = "";
        FontAsset FontValue;
        int2 SizeValue;
        bool IsFocusedValue;
        bool CursorVisible = true;
        double CursorBlinkElapsedSeconds;
        bool IsInvalid;
        /// <summary>
        /// True when the text box hides its border while it is neither focused nor invalid.
        /// </summary>
        bool UseFocusedBorderOnlyValue;
        /// <summary>
        /// Tracks whether the mouse is currently dragging a text selection inside the textbox.
        /// </summary>
        bool IsSelectingText;
        bool IsShakeActive;
        float ShakeElapsedSeconds;
        float CurrentShakeOffsetXValue;
        float3 ShakeBaseLocalPosition;

        // Child components
        RoundedRectComponent BackgroundSprite;
        /// <summary>
        /// Hosts the translucent rectangle used to visualize the active text selection.
        /// </summary>
        Entity SelectionEntity;
        /// <summary>
        /// Draws the active text-selection highlight behind the caret and glyphs.
        /// </summary>
        RoundedRectComponent SelectionSprite;
        Entity TextEntity;
        TextComponent TextComponent;
        InteractableComponent InteractableComponent;

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
        public bool CanReceiveFocus => Parent != null && Parent.IsHierarchyEnabled && InteractableComponent != null;

        /// <summary>
        /// Gets or sets the text content.
        /// </summary>
        public string Text {
            get { return EditState.Text; }
            set {
                string incomingText = value ?? "";
                if (incomingText == EditState.Text) { return; }
                if (NativeSession != null) { TextEntryValidation.ValidateText(incomingText); }
                string previousText = EditState.Text;
                if (NativeSession != null && NativeSession.IsActive) {
                    NativeSession.Replace(incomingText, EditState.SelectionStart, EditState.SelectionEnd - EditState.SelectionStart);
                    EditState.ApplyNativeSnapshot(NativeSession.Text, NativeSession.SelectionStart, NativeSession.SelectionLength, NativeSession.MarkedStart, NativeSession.MarkedLength);
                    NativeRevision = NativeSession.Revision;
                } else {
                    EditState.Text = incomingText;
                }
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
            get { return PlaceholderValue; }
            set {
                PlaceholderValue = value ?? "";
                UpdateTextDisplay();
            }
        }

        /// <summary>
        /// Gets or sets the font used for rendering text.
        /// </summary>
        public FontAsset Font {
            get { return FontValue; }
            set {
                FontValue = value;
                if (TextComponent != null) {
                    TextComponent.Font = FontValue;
                }

                UpdateTextLayout();
            }
        }

        /// <summary>
        /// Gets or sets the size of the text box.
        /// </summary>
        public int2 Size {
            get { return SizeValue; }
            set {
                SizeValue = value;
                if (BackgroundSprite != null) BackgroundSprite.Size = SizeValue;
                if (InteractableComponent != null) {
                    InteractableComponent.Size = SizeValue;
                }

                UpdateTextLayout();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the text box has input focus.
        /// </summary>
        public bool IsFocused {
            get { return IsFocusedValue; }
            set { SetFocusedState(value, true); }
        }

        /// <summary>
        /// Gets or sets whether the textbox border is shown only while focused or invalid.
        /// </summary>
        public bool UseFocusedBorderOnly {
            get { return UseFocusedBorderOnlyValue; }
            set {
                UseFocusedBorderOnlyValue = value;
                UpdateFocusVisual();
            }
        }

        /// <summary>
        /// Gets the transient horizontal shake offset currently applied for invalid-input feedback.
        /// Layout systems can use this to preserve the shake during host relayout.
        /// </summary>
        public float CurrentShakeOffsetX => CurrentShakeOffsetXValue;

        /// <summary>
        /// Applies or clears the invalid visual state without changing the current text or focus state.
        /// </summary>
        /// <param name="isInvalid">True when the text box should use the invalid border color.</param>
        public void SetInvalidState(bool isInvalid) {
            this.IsInvalid = isInvalid;
            UpdateFocusVisual();
        }

        /// <summary>
        /// Starts a short horizontal shake that provides invalid-input feedback while preserving the textbox layout base position.
        /// </summary>
        public void TriggerInvalidShake() {
            if (Parent == null) {
                throw new InvalidOperationException("Text boxes must be attached to an entity before they can animate invalid-input feedback.");
            }

            if (IsShakeActive) {
                Parent.LocalPosition = ShakeBaseLocalPosition;
                CurrentShakeOffsetXValue = 0f;
            }

            ShakeBaseLocalPosition = Parent.LocalPosition;
            ShakeElapsedSeconds = 0f;
            IsShakeActive = true;
        }

        /// <summary>
        /// Creates a new text box with size, font, and optional placeholder.
        /// </summary>
        /// <param name="size">Dimensions of the text box.</param>
        /// <param name="font">Font used to render text.</param>
        /// <param name="placeholder">Placeholder string.</param>
        public TextBoxComponent(int2 size, FontAsset font, string placeholder = "") {
            this.SizeValue = size;
            this.FontValue = font;
            this.PlaceholderValue = placeholder;
            EditState = new TextBoxEditState();
            FillColorValue = ThemeManager.Colors.SurfaceInput;
            BorderColorValue = ThemeManager.Colors.AccentTertiary;
            FocusedBorderColorValue = ThemeManager.Colors.AccentPrimary;
            SelectionColorValue = new byte4(FocusedBorderColorValue.X, FocusedBorderColorValue.Y, FocusedBorderColorValue.Z, 96);
        }

        /// <summary>
        /// Builds child components and registers input handling when added.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            // Create rounded background
            BackgroundSprite = new RoundedRectComponent();
            BackgroundSprite.Size = SizeValue;
            BackgroundSprite.Radius = MathF.Min(SizeValue.X, SizeValue.Y) * 0.15f;
            BackgroundSprite.BorderThickness = 2f;
            BackgroundSprite.FillColor = FillColorValue;
            BackgroundSprite.BorderColor = BorderColorValue;
            entity.AddComponent(BackgroundSprite);

            // Create selection highlight so dragged text can be shown behind the caret and text.
            SelectionEntity = new Entity(OwnerCore ?? throw new InvalidOperationException("Text-box visuals require an owning core."));
            SelectionEntity.LayerMask = entity.LayerMask;
            SelectionEntity.Enabled = true;
            SelectionEntity.InitComponents();
            if (entity.Children == null) {
                entity.InitChildren();
            }

            entity.AddChild(SelectionEntity);

            SelectionSprite = new RoundedRectComponent();
            SelectionSprite.Radius = 2f;
            SelectionSprite.BorderThickness = 0f;
            SelectionSprite.FillColor = new byte4(
                ThemeManager.Colors.AccentPrimary.X,
                ThemeManager.Colors.AccentPrimary.Y,
                ThemeManager.Colors.AccentPrimary.Z,
                96);
            SelectionSprite.BorderColor = SelectionSprite.FillColor;
            SelectionEntity.AddComponent(SelectionSprite);
            SelectionEntity.LocalPosition = new float3(0f, 0f, -0.01f);

            // Create text component
            TextEntity = new Entity(OwnerCore ?? throw new InvalidOperationException("Text-box visuals require an owning core."));
            TextEntity.LayerMask = entity.LayerMask;
            TextEntity.Enabled = true;
            TextEntity.InitComponents();
            if (entity.Children == null) {
                entity.InitChildren();
            }

            entity.AddChild(TextEntity);

            TextComponent = new TextComponent();
            TextComponent.SystemTextStyle = SystemTextStyleValue;
            TextComponent.FontScale = FontScaleValue;
            TextComponent.Font = FontValue;
            TextComponent.Color = TextColorValue;
            TextComponent.WrapText = false;
            TextComponent.ClipSystemTextToSize = true;
            TextEntity.AddComponent(TextComponent);
            CaretEntity = CreateFeedbackEntity(entity);
            entity.AddChild(CaretEntity);
            CaretSprite = new RoundedRectComponent { BorderThickness = 0f, Radius = 0f, FillColor = CaretColorValue };
            CaretEntity.AddComponent(CaretSprite);
            MarkedEntity = CreateFeedbackEntity(entity);
            entity.AddChild(MarkedEntity);
            MarkedSprite = new RoundedRectComponent { BorderThickness = 0f, Radius = 0f, FillColor = CaretColorValue };
            MarkedEntity.AddComponent(MarkedSprite);

            // Create interactable component for mouse clicks
            InteractableComponent = new InteractableComponent();
            InteractableComponent.HoverCursor = PointerCursorKind.Text;
            InteractableComponent.Size = SizeValue;
            InteractableComponent.CursorEvent += OnCursorEvent;
            entity.AddComponent(InteractableComponent);

            // Create a custom update component for keyboard input
            var updateComponent = new TextBoxUpdateComponent(this);
            updateComponent.UpdateOrder = OwnerCore.ObjectManager.GetUpdateOrderForLayer(1);
            entity.AddComponent(updateComponent);

            UpdateTextDisplay();
            UpdateFocusVisual();
        }

        /// <summary>
        /// Handles pointer presses to focus the text box.
        /// </summary>
        void OnCursorEvent(int2 relPos, int2 delta, PointerInteraction state) {
            if (state == PointerInteraction.Press) {
                if (FocusedTextBox != null && FocusedTextBox != this) {
                    FocusedTextBox.IsFocused = false;
                }
                IsFocused = true;
                PollNativeSession();
                int cursorPosition = ResolveCursorPositionFromClick(relPos.X);
                EditState.SetSelection(cursorPosition, cursorPosition);
                PushNativeSelection();
                IsSelectingText = true;
                UpdateTextDisplay();
            } else if (state == PointerInteraction.Hover && IsSelectingText) {
#if DESKTOP_PLATFORM
                if (OwnerCore.Input.GetMouseLeftButtonState() != ButtonState.Pressed) {
                    IsSelectingText = false;
                } else {
#endif
                    EditState.SetSelection(EditState.SelectionAnchorPosition, ResolveCursorPositionFromClick(relPos.X));
                    PushNativeSelection();
#if DESKTOP_PLATFORM
                }
#endif

                UpdateTextDisplay();
            } else if (state == PointerInteraction.Release) {
                IsSelectingText = false;
                UpdateTextDisplay();
            }
        }

        /// <summary>
        /// Handles blinking, key input, and updates the displayed text each frame.
        /// </summary>
        public void Update() {
            UpdateShakeAnimation();

            if (!IsFocusedValue) return;

            Core core = OwnerCore;
            if (core == null) {
                return;
            }

            PollNativeSession();
            if (!IsFocusedValue) { return; }
            // Handle cursor blinking
            CursorBlinkElapsedSeconds += core.DeltaTime;
            if (CursorBlinkElapsedSeconds >= 0.5d) {
                CursorVisible = !CursorVisible;
                CursorBlinkElapsedSeconds = 0d;
                UpdateTextDisplay();
            }

            // Handle keyboard input
#if DESKTOP_PLATFORM
            if (NativeSession != null) { return; }
            InputSystem inputManager = core.Input;
            bool isShiftPressed = inputManager.IsKeyDown(Keys.LeftShift) || inputManager.IsKeyDown(Keys.RightShift);
            bool isControlPressed = inputManager.IsKeyDown(Keys.LeftControl) || inputManager.IsKeyDown(Keys.RightControl);
            bool isAltPressed = inputManager.IsKeyDown(Keys.LeftAlt) || inputManager.IsKeyDown(Keys.RightAlt);

            // Process newly pressed keys
            for (int i = 0; i < 255; i++) {
                Keys key = (Keys)i;
                if (inputManager.WasKeyPressed(key)) {
                    HandleKeyPress(key, isShiftPressed, isControlPressed, isAltPressed);
                }
            }
#endif
        }

        /// <summary>
        /// Advances the short invalid-input shake effect and restores the original layout position when it finishes.
        /// </summary>
        void UpdateShakeAnimation() {
            if (!IsShakeActive || Parent == null) {
                return;
            }

            ShakeElapsedSeconds += EffectFrameDeltaSeconds;
            if (ShakeElapsedSeconds >= ShakeDurationSeconds) {
                Parent.LocalPosition = ShakeBaseLocalPosition;
                CurrentShakeOffsetXValue = 0f;
                IsShakeActive = false;
                return;
            }

            double progress = ShakeElapsedSeconds / ShakeDurationSeconds;
            double amplitude = ShakeAmplitudePixels * (1d - progress);
            double angle = ShakeElapsedSeconds * ShakeFrequencyHz * Math.PI * 2d;
            double offset = Math.Sin(angle) * amplitude;

            CurrentShakeOffsetXValue = (float)offset;
            Parent.LocalPosition = new float3(
                ShakeBaseLocalPosition.X + CurrentShakeOffsetXValue,
                ShakeBaseLocalPosition.Y,
                ShakeBaseLocalPosition.Z);
        }

#if DESKTOP_PLATFORM
        /// <summary>
        /// Processes a key press to modify text, move the cursor, or delete characters.
        /// </summary>
        /// <param name="key">Key pressed.</param>
        /// <param name="isShiftPressed">True when either shift key is pressed.</param>
        /// <param name="isControlPressed">True when either control key is pressed.</param>
        /// <param name="isAltPressed">True when either alt key is pressed.</param>
        void HandleKeyPress(Keys key, bool isShiftPressed, bool isControlPressed, bool isAltPressed) {
            bool textChanged = false;
            bool layoutChanged = false;
            if (TryHandleShortcut(key, isShiftPressed, isControlPressed, isAltPressed, out textChanged, out layoutChanged)) {
                if (textChanged || layoutChanged) {
                    UpdateTextDisplay();
                }

                if (textChanged) {
                    TextChanged?.Invoke(this);
                }

                return;
            }

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
        /// Attempts to resolve one registered textbox shortcut before ordinary key editing runs.
        /// </summary>
        /// <param name="key">Key pressed on the current frame.</param>
        /// <param name="isShiftPressed">True when either shift key is pressed.</param>
        /// <param name="isControlPressed">True when either control key is pressed.</param>
        /// <param name="isAltPressed">True when either alt key is pressed.</param>
        /// <param name="textChanged">Receives whether the shortcut changed the text payload.</param>
        /// <param name="layoutChanged">Receives whether the shortcut changed caret or selection layout.</param>
        /// <returns>True when a registered shortcut consumed the key press.</returns>
        bool TryHandleShortcut(
            Keys key,
            bool isShiftPressed,
            bool isControlPressed,
            bool isAltPressed,
            out bool textChanged,
            out bool layoutChanged) {
            textChanged = false;
            layoutChanged = false;

            TextBoxShortcutRegistry shortcutRegistry = OwnerCore.TextBoxShortcutRegistry;
            if (shortcutRegistry == null) {
                return false;
            }

            if (shortcutRegistry.SelectAllShortcut != null
                && shortcutRegistry.SelectAllShortcut.Matches(key, isControlPressed, isShiftPressed, isAltPressed)) {
                bool hadSelection = EditState.HasSelection;
                int previousSelectionStart = EditState.SelectionStart;
                int previousSelectionEnd = EditState.SelectionEnd;
                EditState.SelectAll();
                layoutChanged = !hadSelection
                    || previousSelectionStart != EditState.SelectionStart
                    || previousSelectionEnd != EditState.SelectionEnd;
                return true;
            }

            if (shortcutRegistry.CopyShortcut != null
                && shortcutRegistry.CopyShortcut.Matches(key, isControlPressed, isShiftPressed, isAltPressed)) {
                if (EditState.HasSelection) {
                    OwnerCore.TextClipboardService.WriteText(EditState.GetSelectedText());
                }

                return true;
            }

            if (shortcutRegistry.PasteShortcut != null
                && shortcutRegistry.PasteShortcut.Matches(key, isControlPressed, isShiftPressed, isAltPressed)) {
                if (!OwnerCore.TextClipboardService.HasText()) {
                    return true;
                }

                string previousText = EditState.Text;
                int previousSelectionStart = EditState.SelectionStart;
                int previousSelectionEnd = EditState.SelectionEnd;
                bool hadSelection = EditState.HasSelection;
                EditState.InsertText(OwnerCore.TextClipboardService.ReadText());
                textChanged = previousText != EditState.Text;
                layoutChanged = hadSelection
                    || previousSelectionStart != EditState.SelectionStart
                    || previousSelectionEnd != EditState.SelectionEnd;
                return true;
            }

            return false;
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
#endif

        /// <summary>
        /// Updates displayed text, placeholder coloring, and cursor.
        /// </summary>
        void UpdateTextDisplay() {
            if (TextComponent == null) return;

            // Display text or placeholder; hide placeholder when focused
            bool showPlaceholder = string.IsNullOrEmpty(EditState.Text) && !IsFocusedValue;
            string displayText = showPlaceholder ? PlaceholderValue : EditState.Text;

            TextComponent.Text = displayText;
            TextComponent.Color = showPlaceholder ? PlaceholderColorValue : TextColorValue;

            UpdateTextLayout();
        }

        /// <summary>
        /// Resolves the caret position for a pointer click within the textbox text area.
        /// </summary>
        /// <param name="clickX">Pointer X position relative to the textbox bounds.</param>
        /// <returns>Caret index that best matches the clicked text position.</returns>
        int ResolveCursorPositionFromClick(int clickX) {
            if (SystemTextStyleValue != null && TextComponent != null) {
                ISystemTextLayout layout = TextComponent.MeasureSystemText();
                return layout.HitTest(Math.Max(0f, clickX - TextPaddingX + TextScrollX), 0f);
            }
            if (Font == null) { return 0; }
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
                return Math.Max((double)Font.FontInfo.SpaceWidth, 1.0) * FontScaleValue;
            }

            FontChar glyph;
            if (Font.Characters != null && Font.Characters.TryGetValue(character, out glyph)) {
                if (glyph.AdvanceWidth > 0f) {
                    return glyph.AdvanceWidth * FontScaleValue;
                }

                double sourceWidth = (double)glyph.SourceRect.Z;
                if (sourceWidth > 0.0) {
                    return sourceWidth * FontScaleValue;
                }
            }

            return 1.0;
        }

        /// <summary>
        /// Positions the textbox text host with shared left padding and vertically centers it using the font line height.
        /// </summary>
        void UpdateTextLayout() {
            if (TextEntity == null || TextComponent == null) {
                return;
            }
            if (SystemTextStyleValue != null) {
                UpdateSystemTextLayout();
                return;
            }
            TextComponent.CenterSystemTextVertically = false;
            if (FontValue == null) { return; }

            double lineHeight = Math.Max((double)FontValue.LineHeight * FontScaleValue, 1.0);
            double textY = Math.Round((SizeValue.Y - lineHeight) / 2.0, MidpointRounding.AwayFromZero);
            FontTightMetrics textMetrics = FontValue.MeasureTight(TextComponent.Text);
            TextEntity.Position = new float3(TextPaddingX, (float)textY, 0.1f);
            TextComponent.Size = new int2(
                (int)Math.Ceiling(textMetrics.Width),
                (int)Math.Ceiling(lineHeight));
            UpdateSelectionVisual(textY, lineHeight);
            UpdateCaretGeometry((float)ResolveTextWidth(0, EditState.CursorPosition), (float)textY, (float)lineHeight);
        }

        /// <summary>
        /// Clears focus when parent is disabled.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled && IsFocusedValue) {
                IsFocused = false;
            }

            if (TextEntity != null) {
                TextEntity.Enabled = newEnabled;
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
                   x < worldPosition.X + SizeValue.X &&
                   y >= worldPosition.Y &&
                   y < worldPosition.Y + SizeValue.Y;
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
#if DESKTOP_PLATFORM
        public bool CanActivateWithKey(Keys key) {
            return key == Keys.Enter;
        }

        /// <summary>
        /// Commits the text box by using the normal blur path for supported activation keys.
        /// </summary>
        /// <param name="key">Activation key routed to the text box.</param>
        public void ActivateFromKey(Keys key) {
            if (!CanActivateWithKey(key) || !IsFocusedValue) {
                return;
            }

            IsFocused = false;
        }
#endif

        /// <summary>
        /// Applies one focus-state transition and optionally submits the current value on blur.
        /// </summary>
        /// <param name="value">Focused state to apply.</param>
        /// <param name="submitOnBlur">True when losing focus should submit the text value.</param>
        void SetFocusedState(bool value, bool submitOnBlur) {
            if (IsFocusedValue == value) {
                if (!value) {
                    IsSelectingText = false;
                    EditState.ClearSelection();
                    UpdateSelectionVisual();
                }
                UpdateFocusVisual();
                return;
            }

            if (value && !CanReceiveFocus) { return; }
            if (value && OwnerCore != null && OwnerCore.TextEntry != null) { TextEntryValidation.ValidateText(EditState.Text); }
            if (value && FocusedTextBox != null && FocusedTextBox != this) { FocusedTextBox.IsFocused = false; }
            if (!value) { CloseNativeSession(); }
            if (value) {
                EditState.SetCursorToEnd();
                if (OwnerCore != null && OwnerCore.TextEntry != null) {
                    TextEntryValidation.ValidateText(EditState.Text);
                    NativeOwnership.DisposeAndRelease(ref NativeSession);
                    NativeSession = OwnerCore.TextEntry.Begin(EditState.Text, EditState.SelectionStart, EditState.SelectionEnd - EditState.SelectionStart, TextEntryValidation.MaximumLength);
                    if (NativeSession == null) { IsFocusedValue = false; throw new InvalidOperationException("Native text backend returned no owned session."); }
                    NativeRevision = -1;
                }
                IsFocusedValue = true;
                FocusedTextBox = this;
            } else if (FocusedTextBox == this) {
                FocusedTextBox = null;
                IsSelectingText = false;
                EditState.ClearSelection();
            }

            IsFocusedValue = value;
            CursorVisible = true;
            CursorBlinkElapsedSeconds = 0d;
            UpdateTextDisplay();
            UpdateFocusVisual();
            FocusChanged?.Invoke(this, IsFocusedValue);
            if (!IsFocusedValue && submitOnBlur) {
                Submitted?.Invoke(this);
            }
        }

        /// <summary>Creates an initialized feedback entity and transfers ownership to the caller, which attaches it before keeping a borrowed field reference.</summary>
        [NativeOwnedReturn]
        Entity CreateFeedbackEntity(Entity parent) {
            Entity child = new Entity(OwnerCore);
            child.LayerMask = parent.LayerMask;
            child.Enabled = true;
            child.InitComponents();
            return child;
        }
        /// <summary>Polls only this focus owner's session, applying a complete coherent snapshot before terminal focus changes.</summary>
        void PollNativeSession() {
            if (NativeSession == null || !IsFocusedValue) { return; }
            NativeSession.Poll();
            if (NativeRevision != NativeSession.Revision) {
                string previous = EditState.Text;
                EditState.ApplyNativeSnapshot(NativeSession.Text, NativeSession.SelectionStart, NativeSession.SelectionLength, NativeSession.MarkedStart, NativeSession.MarkedLength);
                NativeRevision = NativeSession.Revision;
                CursorVisible = true;
                UpdateTextDisplay();
                if (previous != EditState.Text) { TextChanged?.Invoke(this); }
            }
            if (NativeSession == null || !IsFocusedValue) { return; }
            if (NativeSession.Submitted) { SetFocusedState(false, true); }
            else if (NativeSession.Canceled || !NativeSession.IsActive) { SetFocusedState(false, false); }
        }
        /// <summary>Pushes caret/selection changes without replacing the text or unmarking an IME composition.</summary>
        void PushNativeSelection() {
            if (NativeSession != null && NativeSession.IsActive) { NativeSession.Select(EditState.SelectionStart, EditState.SelectionEnd - EditState.SelectionStart); NativeRevision = -1; }
        }
        /// <summary>Ends and releases only this control's owned session; cancellation cannot reach a newer focus owner.</summary>
        void CloseNativeSession() {
            bool changed = false;
            if (NativeSession != null) {
                NativeSession.Poll();
                if (NativeRevision != NativeSession.Revision) {
                    string previous = EditState.Text;
                    EditState.ApplyNativeSnapshot(NativeSession.Text, NativeSession.SelectionStart, NativeSession.SelectionLength, NativeSession.MarkedStart, NativeSession.MarkedLength);
                    changed = previous != EditState.Text;
                    NativeRevision = NativeSession.Revision;
                }
                NativeSession.Cancel(); NativeOwnership.DisposeAndRelease(ref NativeSession);
            }
            if (changed) { TextChanged?.Invoke(this); }
            if (EditState.MarkedStart >= 0) { EditState.ApplyNativeSnapshot(EditState.Text, EditState.SelectionStart, EditState.SelectionEnd - EditState.SelectionStart, -1, 0); }
        }
        /// <summary>Copies one caret from a borrowed shaped layout, moving an interior composed index back to a permitted boundary.</summary>
        float4 ResolveSystemCaret(ISystemTextLayout layout, int index) {
            int boundary = Math.Max(0, Math.Min(index, layout.TextLength));
            while (boundary > 0 && !layout.IsCaretBoundary(boundary)) { boundary--; }
            return layout.GetCaret(boundary);
        }
        /// <summary>Shapes the complete string while rasterizing only the visible horizontal window and scrolling to the active caret.</summary>
        void UpdateSystemTextLayout() {
            int width = Math.Max(0, SizeValue.X - TextPaddingX * 2);
            TextComponent.Size = new int2(width, SizeValue.Y);
            TextComponent.CenterSystemTextVertically = true;
            if (OwnerCore == null || OwnerCore.RenderManager2D == null || OwnerCore.RenderManager2D.SystemTextBackend == null) { return; }
            ISystemTextLayout layout = TextComponent.MeasureSystemText();
            float4 caret = ResolveSystemCaret(layout, EditState.CursorPosition);
            float4 selectionStart = ResolveSystemCaret(layout, EditState.SelectionStart);
            float4 selectionEnd = ResolveSystemCaret(layout, EditState.SelectionEnd);
            float4 markedStart = ResolveSystemCaret(layout, Math.Max(0, EditState.MarkedStart));
            float4 markedEnd = ResolveSystemCaret(layout, Math.Max(0, EditState.MarkedStart + EditState.MarkedLength));
            float lineY = (SizeValue.Y - (float)layout.Size.Y) * 0.5f;
            float maximumScroll = Math.Max(0f, (float)layout.Size.X - width + 2f);
            if (IsFocusedValue) {
                if ((float)caret.X < TextScrollX) { TextScrollX = (float)caret.X; }
                else if ((float)caret.X > TextScrollX + Math.Max(0, width - 2)) { TextScrollX = (float)caret.X - Math.Max(0, width - 2); }
                TextScrollX = Math.Max(0f, Math.Min(TextScrollX, maximumScroll));
            } else { TextScrollX = 0f; }
            TextComponent.SystemTextRasterOrigin = new float2(TextScrollX, -lineY);
            TextEntity.Position = new float3(TextPaddingX - TextScrollX, 0f, 0.1f);
            UpdateCaretGeometry((float)caret.X - TextScrollX, lineY + (float)caret.Y, (float)caret.Z);
            SetFeedbackRange(SelectionEntity, SelectionSprite, (float)selectionStart.X - TextScrollX, (float)selectionEnd.X - TextScrollX, lineY, (float)caret.Z, IsFocusedValue && EditState.HasSelection, SelectionColorValue);
            SetFeedbackRange(MarkedEntity, MarkedSprite, (float)markedStart.X - TextScrollX, (float)markedEnd.X - TextScrollX, lineY + (float)caret.Z - 2f, 2f, IsFocusedValue && EditState.MarkedStart >= 0, CaretColorValue);
        }
        /// <summary>Draws a bounded rectangular span after copying shaped metrics; bidi multi-range highlighting remains a separate enhancement.</summary>
        void SetFeedbackRange(Entity entity, RoundedRectComponent sprite, float start, float end, float y, float height, bool visible, byte4 color) {
            if (entity == null || sprite == null) { return; }
            int width = Math.Max(0, SizeValue.X - TextPaddingX * 2);
            int viewportHeight = Math.Max(0, SizeValue.Y);
            int left = (int)Math.Floor(Math.Max(0f, Math.Min(width, Math.Min(start, end))));
            int right = (int)Math.Ceiling(Math.Max(0f, Math.Min(width, Math.Max(start, end))));
            int top = (int)Math.Floor(Math.Max(0f, Math.Min(viewportHeight, y)));
            int bottom = (int)Math.Ceiling(Math.Max(0f, Math.Min(viewportHeight, y + Math.Max(0f, height))));
            entity.Position = new float3(TextPaddingX + left, top, 0.05f);
            sprite.Size = visible ? new int2(Math.Max(0, right - left), Math.Max(0, bottom - top)) : new int2(0, 0);
            sprite.FillColor = color;
        }
        /// <summary>Positions a separate caret rectangle without adding glyphs to the text or moving native selection.</summary>
        void UpdateCaretGeometry(float x, float y, float height) {
            if (CaretSprite == null || CaretEntity == null) { return; }
            bool visible = IsFocusedValue && CursorVisible && !EditState.HasSelection;
            SetFeedbackRange(CaretEntity, CaretSprite, x, x + Math.Max(1, (int)Math.Ceiling(FontScaleValue)), y, height, visible, CaretColorValue);
            CaretEntity.Position = new float3(CaretEntity.Position.X, CaretEntity.Position.Y, 0.15f);
            if (SystemTextStyleValue == null && MarkedSprite != null) { MarkedSprite.Size = new int2(0, 0); }
        }

        /// <summary>
        /// Updates the text-box outline to reflect whether it currently has keyboard focus.
        /// </summary>
        void UpdateFocusVisual() {
            if (BackgroundSprite == null) {
                return;
            }

            BackgroundSprite.BorderThickness = UseFocusedBorderOnlyValue && !IsFocusedValue && !IsInvalid ? 0f : 2f;

            if (IsInvalid) {
                BackgroundSprite.BorderColor = ThemeManager.Colors.StateDanger;
                return;
            }

            BackgroundSprite.BorderColor = IsFocusedValue
                ? FocusedBorderColorValue
                : BorderColorValue;
        }

        /// <summary>
        /// Updates the selection highlight geometry so dragged text is shown behind the caret.
        /// </summary>
        /// <param name="textY">Vertical offset used for the textbox text host.</param>
        /// <param name="lineHeight">Current text line height.</param>
        void UpdateSelectionVisual(double textY, double lineHeight) {
            if (SelectionEntity == null || SelectionSprite == null || FontValue == null) {
                return;
            }

            if (!IsFocusedValue || !EditState.HasSelection || string.IsNullOrEmpty(EditState.Text)) {
                SelectionSprite.Size = new int2(0, 0);
                SelectionSprite.FillColor = new byte4(
                    ThemeManager.Colors.AccentPrimary.X,
                    ThemeManager.Colors.AccentPrimary.Y,
                    ThemeManager.Colors.AccentPrimary.Z,
                    0);
                SelectionEntity.Position = new float3(TextPaddingX, (float)textY, 0.05f);
                return;
            }

            double selectionStartX = ResolveTextWidth(0, EditState.SelectionStart);
            double selectionWidth = ResolveTextWidth(EditState.SelectionStart, EditState.SelectionEnd);
            SelectionEntity.Position = new float3(
                TextPaddingX + (float)selectionStartX,
                (float)textY,
                0.05f);
            SelectionSprite.Size = new int2(
                (int)Math.Ceiling(selectionWidth),
                (int)Math.Ceiling(lineHeight));
            SelectionSprite.FillColor = SelectionColorValue;
        }

        /// <summary>
        /// Updates the selection highlight using the current text layout metrics.
        /// </summary>
        void UpdateSelectionVisual() {
            if (FontValue == null) {
                return;
            }

            double lineHeight = Math.Max((double)FontValue.LineHeight * FontScaleValue, 1.0);
            double textY = Math.Round((SizeValue.Y - lineHeight) / 2.0, MidpointRounding.AwayFromZero);
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

}
