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
        /// Ensures backspace collapses the caret after deleting a trailing character so a second backspace can remove the previous character.
        /// </summary>
        [Fact]
        public void Backspace_WhenDeletingTrailingDecimalDigit_DoesNotLeaveGhostSelection() {
            TextBoxEditState state = new TextBoxEditState("2.0");

            state.Backspace();
            state.Backspace();

            Assert.Equal("2", state.Text);
            Assert.Equal(1, state.CursorPosition);
            Assert.False(state.HasSelection);
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
        /// Ensures delete collapses the caret after removing one character without leaving a stale selection range behind.
        /// </summary>
        [Fact]
        public void Delete_WhenRemovingOneCharacter_DoesNotLeaveGhostSelection() {
            TextBoxEditState state = new TextBoxEditState("2.0");

            state.CursorPosition = 1;
            state.Delete();

            Assert.Equal("20", state.Text);
            Assert.Equal(1, state.CursorPosition);
            Assert.False(state.HasSelection);
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

        /// <summary>
        /// Ensures moving the caret left without an active selection keeps the selection collapsed at the new caret position.
        /// </summary>
        [Fact]
        public void MoveCursorLeft_WithoutSelection_KeepsSelectionCollapsed() {
            TextBoxEditState state = new TextBoxEditState("abc");

            state.MoveCursorLeft();

            Assert.Equal(2, state.CursorPosition);
            Assert.False(state.HasSelection);
        }

        /// <summary>
        /// Ensures moving the caret right without an active selection keeps the selection collapsed at the new caret position.
        /// </summary>
        [Fact]
        public void MoveCursorRight_WithoutSelection_KeepsSelectionCollapsed() {
            TextBoxEditState state = new TextBoxEditState("abc");

            state.SetCursorToStart();
            state.MoveCursorRight();

            Assert.Equal(1, state.CursorPosition);
            Assert.False(state.HasSelection);
        }

        /// <summary>
        /// Ensures reading the selected text returns the active selection range only.
        /// </summary>
        [Fact]
        public void GetSelectedText_WhenSelectionExists_ReturnsTheSelectedSubstring() {
            TextBoxEditState state = new TextBoxEditState("abcdef");

            state.SetSelection(1, 4);

            Assert.Equal("bcd", state.GetSelectedText());
        }

        /// <summary>
        /// Ensures inserting a text payload replaces the active selection and moves the caret to the end of the inserted text.
        /// </summary>
        [Fact]
        public void InsertText_WhenSelectionExists_ReplacesTheSelectionWithTheProvidedText() {
            TextBoxEditState state = new TextBoxEditState("abcdef");

            state.SetSelection(2, 5);
            state.InsertText("XYZ");

            Assert.Equal("abXYZf", state.Text);
            Assert.Equal(5, state.CursorPosition);
            Assert.False(state.HasSelection);
        }
    }
}
