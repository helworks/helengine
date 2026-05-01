using helengine;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the standalone text editing state used by text boxes.
    /// </summary>
    public class TextBoxEditStateTests {
        /// <summary>
        /// Ensures inserting a character at the caret updates the text and advances the caret by one position.
        /// </summary>
        [Fact]
        public void InsertCharacter_AppendsAtCaretAndAdvancesPosition() {
            TextBoxEditState state = new TextBoxEditState("ab");

            state.CursorPosition = 1;
            state.InsertCharacter('x');

            Assert.Equal("axb", state.Text);
            Assert.Equal(2, state.CursorPosition);
        }

        /// <summary>
        /// Ensures backspace removes the character immediately before the caret.
        /// </summary>
        [Fact]
        public void Backspace_RemovesCharacterBeforeCaret() {
            TextBoxEditState state = new TextBoxEditState("abc");

            state.CursorPosition = 2;
            state.Backspace();

            Assert.Equal("ac", state.Text);
            Assert.Equal(1, state.CursorPosition);
        }

        /// <summary>
        /// Ensures delete removes the character immediately after the caret.
        /// </summary>
        [Fact]
        public void Delete_RemovesCharacterAtCaret() {
            TextBoxEditState state = new TextBoxEditState("abc");

            state.CursorPosition = 1;
            state.Delete();

            Assert.Equal("ac", state.Text);
            Assert.Equal(1, state.CursorPosition);
        }

        /// <summary>
        /// Ensures inserting a character while text is selected replaces the selected range.
        /// </summary>
        [Fact]
        public void InsertCharacter_ReplacesSelectedText() {
            TextBoxEditState state = new TextBoxEditState("name");

            state.SetSelection(1, 4);
            state.InsertCharacter('x');

            Assert.Equal("nx", state.Text);
            Assert.Equal(2, state.CursorPosition);
            Assert.False(state.HasSelection);
        }

        /// <summary>
        /// Ensures replacing the text clamps the caret into the new valid range.
        /// </summary>
        [Fact]
        public void SetText_ClampsCaretIntoTheNewTextRange() {
            TextBoxEditState state = new TextBoxEditState("abcd");

            state.CursorPosition = 4;
            state.Text = "a";

            Assert.Equal("a", state.Text);
            Assert.Equal(1, state.CursorPosition);
        }
    }
}
