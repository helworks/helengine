namespace helengine {
    /// <summary>
    /// Renders text using a provided font asset via the 2D render manager.
    /// </summary>
    public class TextComponent : Component, ITextDrawable2D, IAnchorSizeProvider {
        /// <summary>
        /// Stores the current text content so selection state can clamp itself when the text changes.
        /// </summary>
        string TextValue;
        /// <summary>
        /// Tracks whether the text can be selected by mouse or keyboard input.
        /// </summary>
        bool SelectionEnabledValue;
        /// <summary>
        /// Tracks whether the component currently owns keyboard focus for selection input.
        /// </summary>
        bool IsFocusedValue;
        /// <summary>
        /// Tracks whether a drag is currently extending the active text selection.
        /// </summary>
        bool IsSelectingTextValue;
        /// <summary>
        /// Fixed end of the current selection range.
        /// </summary>
        int SelectionAnchorPositionValue;
        /// <summary>
        /// Moving end of the current selection range.
        /// </summary>
        int CursorPositionValue;
        /// <summary>
        /// Child entity that renders the active selection highlight.
        /// </summary>
        Entity SelectionEntityValue;
        /// <summary>
        /// Rounded rectangle used to visualize the active selection range.
        /// </summary>
        RoundedRectComponent SelectionSpriteValue;
        /// <summary>
        /// Update component that polls input when selection is enabled.
        /// </summary>
        TextComponentSelectionUpdateComponent SelectionUpdateComponentValue;
        /// <summary>
        /// Uniform glyph scale applied during text layout, hit testing, and rendering.
        /// </summary>
        float FontScaleValue;
        byte RenderOrder2DValue;

        /// <summary>
        /// Gets or sets the render order for this text drawable.
        /// </summary>
        public byte RenderOrder2D {
            get { return RenderOrder2DValue; }
            set {
                if (RenderOrder2DValue != value) {
                    RenderOrder2DValue = value;
                    UpdateSelectionRenderOrder();
                    if (Parent != null && Parent.IsHierarchyEnabled) {
                        Core.Instance.ObjectManager.RemoveFromRender2D(this);
                        Core.Instance.ObjectManager.RegisterForRender2D(this);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets an optional pre-rendered texture backing this text.
        /// </summary>
        [ScenePersistenceIgnore]
        public RuntimeTexture Texture { get; set; }

        /// <summary>
        /// Gets or sets the rotation applied during rendering.
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// Gets or sets the source rectangle within the backing texture.
        /// </summary>
        public float4 SourceRect { get; set; }

        /// <summary>
        /// Gets or sets the layout size of the rendered text.
        /// </summary>
        public int2 Size { get; set; }

        /// <summary>
        /// Gets the local size used by anchored layout calculations.
        /// </summary>
        public int2 AnchorSize => Size;

        /// <summary>
        /// Gets or sets the color tint applied to the glyphs.
        /// </summary>
        public byte4 Color { get; set; }

        /// <summary>
        /// Gets or sets the text content to render.
        /// </summary>
        public string Text {
            get { return TextValue; }
            set {
                TextValue = value ?? "";
                ClampSelectionToTextLength();
                UpdateSelectionVisual();
            }
        }

        /// <summary>
        /// Gets or sets whether the renderer should wrap text against the component width.
        /// </summary>
        public bool WrapText { get; set; }

        /// <summary>
        /// Gets or sets the font asset used for rendering.
        /// </summary>
        public FontAsset Font { get; set; }

        /// <summary>
        /// Gets or sets the uniform glyph scale applied during rendering and interaction.
        /// </summary>
        public float FontScale {
            get { return FontScaleValue; }
            set {
                if (value <= 0f) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Font scale must be greater than zero.");
                }

                if (FontScaleValue != value) {
                    FontScaleValue = value;
                    UpdateSelectionVisual();
                }
            }
        }

        /// <summary>
        /// Gets or sets the layer mask used to filter cameras.
        /// </summary>
        public byte LayerMask { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether mouse and keyboard selection is enabled for this text component.
        /// </summary>
        public bool SelectionEnabled {
            get { return SelectionEnabledValue; }
            set {
                if (SelectionEnabledValue == value) {
                    return;
                }

                SelectionEnabledValue = value;
                if (!SelectionEnabledValue) {
                    IsFocusedValue = false;
                    IsSelectingTextValue = false;
                    ClearSelection();
                    if (SelectionEntityValue != null) {
                        SelectionEntityValue.Enabled = false;
                    }
                    UpdateSelectionVisual();
                    return;
                }

                if (Parent != null) {
                    EnsureSelectionInfrastructure(Parent);
                    if (SelectionEntityValue != null) {
                        SelectionEntityValue.Enabled = Parent.IsHierarchyEnabled;
                    }
                    if (Parent.IsHierarchyEnabled && RenderOrder2DValue == 0) {
                        Core.Instance.ObjectManager.RemoveFromRender2D(this);
                        Core.Instance.ObjectManager.RegisterForRender2D(this);
                    }
                    UpdateSelectionVisual();
                }
            }
        }

        /// <summary>
        /// Gets whether the component currently has an active selection range.
        /// </summary>
        public bool HasSelection {
            get { return SelectionAnchorPositionValue != CursorPositionValue; }
        }

        /// <summary>
        /// Gets the first character index included in the active selection.
        /// </summary>
        public int SelectionStart {
            get { return Math.Min(SelectionAnchorPositionValue, CursorPositionValue); }
        }

        /// <summary>
        /// Gets the character index immediately after the active selection.
        /// </summary>
        public int SelectionEnd {
            get { return Math.Max(SelectionAnchorPositionValue, CursorPositionValue); }
        }

        /// <summary>
        /// Initializes a new text component with default values.
        /// </summary>
        public TextComponent() {
            TextValue = "";
            SelectionAnchorPositionValue = 0;
            CursorPositionValue = 0;
            Color = new byte4(255, 255, 255, 255);
            SourceRect = new float4(0, 0, 1, 1);
            WrapText = false;
            FontScaleValue = 1f;
        }

        /// <summary>
        /// Registers the text drawable when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            if (SelectionEnabled) {
                EnsureSelectionInfrastructure(entity);
            }

            base.ComponentAdded(entity);

            if (entity.IsHierarchyEnabled) {
                Core.Instance.ObjectManager.RegisterForRender2D(this);
            }

            if (SelectionEnabled) {
                if (SelectionEntityValue != null) {
                    SelectionEntityValue.Enabled = entity.IsHierarchyEnabled;
                }
                UpdateSelectionVisual();
            }
        }

        /// <summary>
        /// Registers or unregisters the text based on enabled state changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterForRender2D(this);
            } else {
                Core.Instance.ObjectManager.RemoveFromRender2D(this);
                IsFocusedValue = false;
                IsSelectingTextValue = false;
            }

            if (SelectionEntityValue != null) {
                SelectionEntityValue.Enabled = newEnabled && SelectionEnabled;
            }

            UpdateSelectionVisual();
        }

        /// <summary>
        /// Removes any attached selection helper components when the text component is detached from an entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            Core.Instance.ObjectManager.RemoveFromRender2D(this);

            IsFocusedValue = false;
            IsSelectingTextValue = false;

            if (SelectionUpdateComponentValue != null && entity != null) {
                entity.RemoveComponent(SelectionUpdateComponentValue);
                SelectionUpdateComponentValue = null;
            }

            if (SelectionEntityValue != null) {
                SelectionEntityValue.Enabled = false;
            }
        }

        /// <summary>
        /// Clears the current selection without moving the caret.
        /// </summary>
        public void ClearSelection() {
            SelectionAnchorPositionValue = CursorPositionValue;
            UpdateSelectionVisual();
        }

        /// <summary>
        /// Selects the entire text range.
        /// </summary>
        public void SelectAll() {
            SelectionAnchorPositionValue = 0;
            CursorPositionValue = TextValue.Length;
            UpdateSelectionVisual();
        }

        /// <summary>
        /// Ensures the selection overlay and update component exist when selection is enabled.
        /// </summary>
        /// <param name="entity">Entity that owns the text component.</param>
        void EnsureSelectionInfrastructure(Entity entity) {
            if (SelectionEntityValue == null) {
                if (entity.Children == null) {
                    entity.InitChildren();
                }

                SelectionEntityValue = new Entity();
                SelectionEntityValue.LayerMask = entity.LayerMask;
                SelectionEntityValue.Enabled = false;
                SelectionEntityValue.InitComponents();
                entity.AddChild(SelectionEntityValue);

                SelectionSpriteValue = new RoundedRectComponent();
                SelectionSpriteValue.Radius = 2f;
                SelectionSpriteValue.BorderThickness = 0f;
                SelectionSpriteValue.FillColor = new byte4(
                    ThemeManager.Colors.AccentPrimary.X,
                    ThemeManager.Colors.AccentPrimary.Y,
                    ThemeManager.Colors.AccentPrimary.Z,
                    96);
                SelectionSpriteValue.BorderColor = SelectionSpriteValue.FillColor;
                SelectionSpriteValue.RenderOrder2D = ResolveSelectionRenderOrder();
                SelectionEntityValue.AddComponent(SelectionSpriteValue);
            }

            if (SelectionUpdateComponentValue == null) {
                SelectionUpdateComponentValue = new TextComponentSelectionUpdateComponent(this);
                SelectionUpdateComponentValue.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
                entity.AddComponent(SelectionUpdateComponentValue);
            }
        }

        /// <summary>
        /// Updates the selection render order so the highlight remains behind the glyphs.
        /// </summary>
        void UpdateSelectionRenderOrder() {
            if (SelectionSpriteValue == null) {
                return;
            }

            SelectionSpriteValue.RenderOrder2D = ResolveSelectionRenderOrder();
        }

        /// <summary>
        /// Resolves the render order used for the selection highlight.
        /// </summary>
        /// <returns>Render order one step behind the text drawable when possible.</returns>
        byte ResolveSelectionRenderOrder() {
            if (RenderOrder2DValue == 0) {
                return 0;
            }

            return (byte)(RenderOrder2DValue - 1);
        }

        /// <summary>
        /// Updates the selection state so both ends stay within the current text bounds.
        /// </summary>
        void ClampSelectionToTextLength() {
            int textLength = TextValue.Length;
            if (CursorPositionValue < 0) {
                CursorPositionValue = 0;
            } else if (CursorPositionValue > textLength) {
                CursorPositionValue = textLength;
            }

            if (SelectionAnchorPositionValue < 0) {
                SelectionAnchorPositionValue = 0;
            } else if (SelectionAnchorPositionValue > textLength) {
                SelectionAnchorPositionValue = textLength;
            }
        }

        /// <summary>
        /// Resolves the current selection highlight geometry from the active text range.
        /// </summary>
        void UpdateSelectionVisual() {
            if (SelectionEntityValue == null || SelectionSpriteValue == null) {
                return;
            }

            if (!SelectionEnabled || Font == null || !HasSelection || string.IsNullOrEmpty(TextValue)) {
                SelectionEntityValue.LocalPosition = new float3(0f, 0f, 0.05f);
                SelectionSpriteValue.Size = new int2(0, 0);
                SelectionSpriteValue.FillColor = new byte4(
                    ThemeManager.Colors.AccentPrimary.X,
                    ThemeManager.Colors.AccentPrimary.Y,
                    ThemeManager.Colors.AccentPrimary.Z,
                    0);
                SelectionSpriteValue.BorderColor = SelectionSpriteValue.FillColor;
                return;
            }

            int selectionLineIndex = ResolveLineIndexFromTextIndex(SelectionStart);
            int selectionLineEndIndex = ResolveLineEndIndex(selectionLineIndex);
            double selectionStartX = ResolveTextWidthInLine(selectionLineIndex, ResolveLineStartIndex(selectionLineIndex), SelectionStart);
            double selectionWidth = ResolveTextWidthInLine(
                selectionLineIndex,
                SelectionStart,
                Math.Min(SelectionEnd, selectionLineEndIndex));
            double lineHeight = Math.Max((double)Font.LineHeight * GetResolvedFontScale(), 1.0);

            SelectionEntityValue.LocalPosition = new float3(
                (float)selectionStartX,
                (float)(selectionLineIndex * lineHeight),
                0.05f);
            SelectionSpriteValue.Size = new int2(
                (int)Math.Ceiling(selectionWidth),
                (int)Math.Ceiling(lineHeight));
            SelectionSpriteValue.FillColor = new byte4(
                ThemeManager.Colors.AccentPrimary.X,
                ThemeManager.Colors.AccentPrimary.Y,
                ThemeManager.Colors.AccentPrimary.Z,
                96);
            SelectionSpriteValue.BorderColor = SelectionSpriteValue.FillColor;
        }

        /// <summary>
        /// Resolves the width of a substring using the configured font metrics.
        /// </summary>
        /// <param name="startIndex">First character index to include.</param>
        /// <param name="endIndex">Character index immediately after the last character to include.</param>
        /// <returns>Substring width in pixels.</returns>
        double ResolveTextWidth(int startIndex, int endIndex) {
            if (Font == null) {
                return 0.0;
            }

            int clampedStart = Math.Max(0, Math.Min(startIndex, TextValue.Length));
            int clampedEnd = Math.Max(0, Math.Min(endIndex, TextValue.Length));
            double width = 0.0;
            for (int index = clampedStart; index < clampedEnd; index++) {
                width += ResolveCharacterAdvance(TextValue[index]);
            }

            return width;
        }

        /// <summary>
        /// Resolves the width of one substring after clamping it to a specific text line.
        /// </summary>
        /// <param name="lineIndex">Zero-based line index that contains the measured range.</param>
        /// <param name="startIndex">First character index to include.</param>
        /// <param name="endIndex">Character index immediately after the last character to include.</param>
        /// <returns>Measured width in pixels for the requested range on the supplied line.</returns>
        double ResolveTextWidthInLine(int lineIndex, int startIndex, int endIndex) {
            int lineStartIndex = ResolveLineStartIndex(lineIndex);
            int lineEndIndex = ResolveLineEndIndex(lineIndex);
            int clampedStartIndex = Math.Max(lineStartIndex, Math.Min(startIndex, lineEndIndex));
            int clampedEndIndex = Math.Max(clampedStartIndex, Math.Min(endIndex, lineEndIndex));
            return ResolveTextWidth(clampedStartIndex, clampedEndIndex);
        }

        /// <summary>
        /// Resolves the advance width of one character using the configured font metrics.
        /// </summary>
        /// <param name="character">Character to measure.</param>
        /// <returns>Advance width in pixels.</returns>
        double ResolveCharacterAdvance(char character) {
            double fontScale = GetResolvedFontScale();
            if (character == '\r' || character == '\n') {
                return 0.0;
            }

            if (character == ' ') {
                return Math.Max((double)Font.FontInfo.SpaceWidth, 1.0) * fontScale;
            }

            FontChar glyph;
            if (Font.Characters != null && Font.Characters.TryGetValue(character, out glyph)) {
                if (glyph.AdvanceWidth > 0f) {
                    return glyph.AdvanceWidth * fontScale;
                }

                double sourceWidth = (double)glyph.SourceRect.Z;
                if (sourceWidth > 0.0) {
                    return sourceWidth * fontScale;
                }
            }

            return 1.0 * fontScale;
        }

        /// <summary>
        /// Returns whether the provided screen point lies within the rendered text bounds.
        /// </summary>
        /// <param name="x">Screen X coordinate to evaluate.</param>
        /// <param name="y">Screen Y coordinate to evaluate.</param>
        /// <returns>True when the point lies inside the text bounds.</returns>
        bool ContainsScreenPoint(int x, int y) {
            if (Parent == null || Font == null) {
                return false;
            }

            float3 position = Parent.Position;
            float2 textSize = Font.MeasureString(TextValue);
            double fontScale = GetResolvedFontScale();
            double textWidth = textSize.X * fontScale;
            double textHeight = textSize.Y * fontScale;

            return x >= position.X &&
                   x < position.X + textWidth &&
                   y >= position.Y &&
                   y < position.Y + textHeight;
        }

        /// <summary>
        /// Applies or clears keyboard focus for selection input.
        /// </summary>
        /// <param name="newFocused">True when the text component should accept selection input.</param>
        void SetFocusedState(bool newFocused) {
            if (IsFocusedValue == newFocused) {
                return;
            }

            IsFocusedValue = newFocused;
            IsSelectingTextValue = false;

            if (IsFocusedValue) {
                CursorPositionValue = TextValue.Length;
                SelectionAnchorPositionValue = CursorPositionValue;
            }
        }

        /// <summary>
        /// Handles a pointer press that may start a selection drag.
        /// </summary>
        /// <param name="pointerX">Pointer X coordinate in screen coordinates.</param>
        /// <param name="pointerY">Pointer Y coordinate in screen coordinates.</param>
        void HandleSelectionPress(int pointerX, int pointerY) {
            if (!SelectionEnabled || !ContainsScreenPoint(pointerX, pointerY)) {
                SetFocusedState(false);
                return;
            }

            SetFocusedState(true);
            int cursor = ResolveCursorPositionFromClick(pointerX, pointerY);
            CursorPositionValue = cursor;
            SelectionAnchorPositionValue = cursor;
            IsSelectingTextValue = true;
            UpdateSelectionVisual();
        }

        /// <summary>
        /// Continues an active drag selection while the pointer is held.
        /// </summary>
        /// <param name="pointerX">Pointer X coordinate in screen coordinates.</param>
        /// <param name="pointerY">Pointer Y coordinate in screen coordinates.</param>
        void HandleSelectionDrag(int pointerX, int pointerY) {
            if (!SelectionEnabled || !IsSelectingTextValue || Font == null) {
                return;
            }

            CursorPositionValue = ResolveCursorPositionFromClick(pointerX, pointerY);
            ClampSelectionToTextLength();
            UpdateSelectionVisual();
        }

        /// <summary>
        /// Ends an active drag selection.
        /// </summary>
        void HandleSelectionRelease() {
            IsSelectingTextValue = false;
        }

        /// <summary>
        /// Processes keyboard input for caret movement and selection shortcuts.
        /// </summary>
        void HandleSelectionKeyboardInput() {
            if (!SelectionEnabled || !IsFocusedValue) {
                return;
            }

            InputSystem input = Core.Instance.Input;
            bool isShiftPressed = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
            bool isControlPressed = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);

            for (int i = 0; i < 255; i++) {
                Keys key = (Keys)i;
                if (!input.WasKeyPressed(key)) {
                    continue;
                }

                if (isControlPressed && key == Keys.A) {
                    SelectAll();
                } else if (key == Keys.Left) {
                    MoveCursorLeft(isShiftPressed);
                } else if (key == Keys.Right) {
                    MoveCursorRight(isShiftPressed);
                } else if (key == Keys.Home) {
                    MoveCursorToStart(isShiftPressed);
                } else if (key == Keys.End) {
                    MoveCursorToEnd(isShiftPressed);
                }
            }
        }

        /// <summary>
        /// Moves the caret one character to the left and optionally extends the selection.
        /// </summary>
        /// <param name="extendSelection">True to extend the current selection.</param>
        void MoveCursorLeft(bool extendSelection) {
            if (extendSelection) {
                if (!HasSelection) {
                    SelectionAnchorPositionValue = CursorPositionValue;
                }

                CursorPositionValue = Math.Max(0, CursorPositionValue - 1);
            } else if (HasSelection) {
                CursorPositionValue = SelectionStart;
                SelectionAnchorPositionValue = CursorPositionValue;
            } else {
                CursorPositionValue = Math.Max(0, CursorPositionValue - 1);
                SelectionAnchorPositionValue = CursorPositionValue;
            }

            UpdateSelectionVisual();
        }

        /// <summary>
        /// Moves the caret one character to the right and optionally extends the selection.
        /// </summary>
        /// <param name="extendSelection">True to extend the current selection.</param>
        void MoveCursorRight(bool extendSelection) {
            if (extendSelection) {
                if (!HasSelection) {
                    SelectionAnchorPositionValue = CursorPositionValue;
                }

                CursorPositionValue = Math.Min(TextValue.Length, CursorPositionValue + 1);
            } else if (HasSelection) {
                CursorPositionValue = SelectionEnd;
                SelectionAnchorPositionValue = CursorPositionValue;
            } else {
                CursorPositionValue = Math.Min(TextValue.Length, CursorPositionValue + 1);
                SelectionAnchorPositionValue = CursorPositionValue;
            }

            UpdateSelectionVisual();
        }

        /// <summary>
        /// Moves the caret to the beginning of the text and optionally extends the selection.
        /// </summary>
        /// <param name="extendSelection">True to extend the current selection.</param>
        void MoveCursorToStart(bool extendSelection) {
            if (extendSelection) {
                if (!HasSelection) {
                    SelectionAnchorPositionValue = CursorPositionValue;
                }

                CursorPositionValue = 0;
            } else {
                CursorPositionValue = 0;
                SelectionAnchorPositionValue = CursorPositionValue;
            }

            UpdateSelectionVisual();
        }

        /// <summary>
        /// Moves the caret to the end of the text and optionally extends the selection.
        /// </summary>
        /// <param name="extendSelection">True to extend the current selection.</param>
        void MoveCursorToEnd(bool extendSelection) {
            if (extendSelection) {
                if (!HasSelection) {
                    SelectionAnchorPositionValue = CursorPositionValue;
                }

                CursorPositionValue = TextValue.Length;
            } else {
                CursorPositionValue = TextValue.Length;
                SelectionAnchorPositionValue = CursorPositionValue;
            }

            UpdateSelectionVisual();
        }

        /// <summary>
        /// Resolves the caret position nearest to a mouse click.
        /// </summary>
        /// <param name="pointerX">Mouse X position in screen coordinates.</param>
        /// <param name="pointerY">Mouse Y position in screen coordinates.</param>
        /// <returns>Caret index that best matches the clicked position.</returns>
        int ResolveCursorPositionFromClick(int pointerX, int pointerY) {
            if (Font == null || Parent == null) {
                return 0;
            }

            if (string.IsNullOrEmpty(TextValue)) {
                return 0;
            }

            float3 textPosition = Parent.Position;
            double textX = Math.Max(0.0, (double)pointerX - textPosition.X);
            double textY = Math.Max(0.0, (double)pointerY - textPosition.Y);
            int lineIndex = ResolveLineIndexFromLocalY(textY);
            int lineStartIndex = ResolveLineStartIndex(lineIndex);
            int lineEndIndex = ResolveLineEndIndex(lineIndex);
            double cursorX = 0.0;
            for (int index = lineStartIndex; index < lineEndIndex; index++) {
                double advance = ResolveCharacterAdvance(TextValue[index]);
                if (textX < cursorX + (advance * 0.5)) {
                    return index;
                }

                cursorX += advance;
            }

            return lineEndIndex;
        }

        /// <summary>
        /// Resolves the zero-based line index addressed by one local pointer Y coordinate.
        /// </summary>
        /// <param name="localY">Pointer Y position relative to the text origin.</param>
        /// <returns>Line index that contains the pointer.</returns>
        int ResolveLineIndexFromLocalY(double localY) {
            double lineHeight = Math.Max((double)Font.LineHeight * GetResolvedFontScale(), 1.0);
            int lineIndex = (int)(localY / lineHeight);
            int maxLineIndex = Math.Max(0, ResolveLineCount() - 1);
            return Math.Max(0, Math.Min(lineIndex, maxLineIndex));
        }

        /// <summary>
        /// Resolves the uniform glyph scale used by layout, hit testing, and rendering.
        /// </summary>
        /// <returns>Positive glyph scale.</returns>
        double GetResolvedFontScale() {
            return Math.Max((double)FontScaleValue, 0.0001d);
        }

        /// <summary>
        /// Resolves the zero-based line index that contains the supplied text index.
        /// </summary>
        /// <param name="textIndex">Text index to resolve.</param>
        /// <returns>Zero-based line index that contains the requested text position.</returns>
        int ResolveLineIndexFromTextIndex(int textIndex) {
            int clampedIndex = Math.Max(0, Math.Min(textIndex, TextValue.Length));
            int lineIndex = 0;
            for (int index = 0; index < clampedIndex && index < TextValue.Length; index++) {
                if (TextValue[index] == '\n') {
                    lineIndex++;
                }
            }

            return lineIndex;
        }

        /// <summary>
        /// Resolves the first text index that belongs to the supplied line.
        /// </summary>
        /// <param name="lineIndex">Zero-based line index to inspect.</param>
        /// <returns>First text index on the requested line.</returns>
        int ResolveLineStartIndex(int lineIndex) {
            if (lineIndex <= 0) {
                return 0;
            }

            int currentLineIndex = 0;
            for (int index = 0; index < TextValue.Length; index++) {
                if (TextValue[index] != '\n') {
                    continue;
                }

                currentLineIndex++;
                if (currentLineIndex == lineIndex) {
                    return index + 1;
                }
            }

            return TextValue.Length;
        }

        /// <summary>
        /// Resolves the character index immediately after the last character on the supplied line.
        /// </summary>
        /// <param name="lineIndex">Zero-based line index to inspect.</param>
        /// <returns>Exclusive line-end index.</returns>
        int ResolveLineEndIndex(int lineIndex) {
            int lineStartIndex = ResolveLineStartIndex(lineIndex);
            for (int index = lineStartIndex; index < TextValue.Length; index++) {
                if (TextValue[index] == '\n') {
                    return index;
                }
            }

            return TextValue.Length;
        }

        /// <summary>
        /// Counts the number of visible lines in the current text content.
        /// </summary>
        /// <returns>Number of lines represented by the current text.</returns>
        int ResolveLineCount() {
            if (string.IsNullOrEmpty(TextValue)) {
                return 1;
            }

            int lineCount = 1;
            for (int index = 0; index < TextValue.Length; index++) {
                if (TextValue[index] == '\n') {
                    lineCount++;
                }
            }

            return lineCount;
        }

        /// <summary>
        /// Polls the active input state and forwards any selection interaction to this text component.
        /// </summary>
        internal void UpdateSelectionInput() {
            if (!SelectionEnabled || Parent == null || Font == null || !Parent.IsHierarchyEnabled) {
                return;
            }

            InputSystem input = Core.Instance.Input;
            if (input == null) {
                return;
            }

            int pointerX = input.GetMouseX();
            int pointerY = input.GetMouseY();
            if (input.WasMouseLeftButtonPressed()) {
                HandleSelectionPress(pointerX, pointerY);
            } else if (input.GetMouseLeftButtonState() == ButtonState.Pressed && IsSelectingTextValue) {
                HandleSelectionDrag(pointerX, pointerY);
            } else if (input.WasMouseLeftButtonReleased()) {
                HandleSelectionRelease();
            } else {
                HandleSelectionKeyboardInput();
            }
        }

        /// <summary>
        /// Issues a draw call for this text through the 2D render manager.
        /// </summary>
        public virtual void Draw() {
            if (Font == null) {
                return;
            }

            Core.Instance.RenderManager2D.DrawText(this);
        }

    }
}


