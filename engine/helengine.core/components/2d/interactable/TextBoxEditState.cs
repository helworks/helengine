namespace helengine {
    /// <summary>
    /// Stores the editable text and caret position for a textbox control.
    /// </summary>
    public class TextBoxEditState {
        /// <summary>
        /// Backing text content currently being edited.
        /// </summary>
        string TextValue = "";

        /// <summary>
        /// Current caret position within the text content.
        /// </summary>
        int CursorPositionValue;

        /// <summary>
        /// Initializes a new empty edit state.
        /// </summary>
        public TextBoxEditState() {
        }

        /// <summary>
        /// Initializes a new edit state seeded with the supplied text.
        /// </summary>
        /// <param name="text">Initial text content.</param>
        public TextBoxEditState(string text) {
            Text = text;
            SetCursorToEnd();
        }

        /// <summary>
        /// Gets or sets the current text content.
        /// Setting the text preserves the caret when possible and clamps it into the new range when necessary.
        /// </summary>
        public string Text {
            get { return TextValue; }
            set {
                TextValue = value ?? "";
                CursorPositionValue = ClampCursor(CursorPositionValue);
            }
        }

        /// <summary>
        /// Gets or sets the caret position within the current text.
        /// </summary>
        public int CursorPosition {
            get { return CursorPositionValue; }
            set { CursorPositionValue = ClampCursor(value); }
        }

        /// <summary>
        /// Moves the caret to the beginning of the text.
        /// </summary>
        public void SetCursorToStart() {
            CursorPositionValue = 0;
        }

        /// <summary>
        /// Moves the caret to the end of the text.
        /// </summary>
        public void SetCursorToEnd() {
            CursorPositionValue = TextValue.Length;
        }

        /// <summary>
        /// Moves the caret one character to the left without crossing the start of the text.
        /// </summary>
        public void MoveCursorLeft() {
            CursorPositionValue = Math.Max(0, CursorPositionValue - 1);
        }

        /// <summary>
        /// Moves the caret one character to the right without crossing the end of the text.
        /// </summary>
        public void MoveCursorRight() {
            CursorPositionValue = Math.Min(TextValue.Length, CursorPositionValue + 1);
        }

        /// <summary>
        /// Deletes the character immediately before the caret and moves the caret back one position.
        /// </summary>
        public void Backspace() {
            if (CursorPositionValue <= 0) {
                return;
            }

            TextValue = TextValue.Remove(CursorPositionValue - 1, 1);
            CursorPositionValue--;
        }

        /// <summary>
        /// Deletes the character immediately after the caret without moving the caret.
        /// </summary>
        public void Delete() {
            if (CursorPositionValue < 0 || CursorPositionValue >= TextValue.Length) {
                return;
            }

            TextValue = TextValue.Remove(CursorPositionValue, 1);
            CursorPositionValue = ClampCursor(CursorPositionValue);
        }

        /// <summary>
        /// Inserts one character at the caret and advances the caret to the new insertion point.
        /// </summary>
        /// <param name="character">Character to insert.</param>
        public void InsertCharacter(char character) {
            int insertionIndex = ClampCursor(CursorPositionValue);
            TextValue = TextValue.Insert(insertionIndex, character.ToString());
            CursorPositionValue = insertionIndex + 1;
        }

        /// <summary>
        /// Clamps a requested caret position into the current text bounds.
        /// </summary>
        /// <param name="value">Caret position to normalize.</param>
        /// <returns>Normalized caret position.</returns>
        int ClampCursor(int value) {
            return Math.Max(0, Math.Min(value, TextValue.Length));
        }
    }
}
