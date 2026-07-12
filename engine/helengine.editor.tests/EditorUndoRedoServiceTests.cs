using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor undo/redo service maintains stack state, saved revisions, and refresh callbacks correctly.
    /// </summary>
    public sealed class EditorUndoRedoServiceTests {
        /// <summary>
        /// Ensures recording one operation enables undo, undo enables redo, and each applied direction refreshes editor state once.
        /// </summary>
        [Fact]
        public void Record_undo_and_redo_update_stack_state_and_refresh_editor_state() {
            List<string> invocationLog = new List<string>();
            int refreshCount = 0;
            EditorUndoRedoService service = new EditorUndoRedoService(new EditorHistoryContext {
                RefreshEditorState = () => refreshCount++
            });
            RecordingHistoryOperation operation = new RecordingHistoryOperation("rename", invocationLog);

            service.Record(operation);

            Assert.True(service.CanUndo);
            Assert.False(service.CanRedo);
            Assert.False(service.IsAtSavedState);

            bool undoApplied = service.Undo();

            Assert.True(undoApplied);
            Assert.False(service.CanUndo);
            Assert.True(service.CanRedo);
            Assert.Equal(new[] { "undo:rename" }, invocationLog);
            Assert.Equal(1, refreshCount);

            bool redoApplied = service.Redo();

            Assert.True(redoApplied);
            Assert.True(service.CanUndo);
            Assert.False(service.CanRedo);
            Assert.Equal(new[] { "undo:rename", "redo:rename" }, invocationLog);
            Assert.Equal(2, refreshCount);
        }

        /// <summary>
        /// Ensures the saved revision marker follows the current history cursor and resets with a cleared stack.
        /// </summary>
        [Fact]
        public void Mark_saved_and_reset_track_the_saved_revision_cursor() {
            EditorUndoRedoService service = new EditorUndoRedoService(new EditorHistoryContext());
            RecordingHistoryOperation operation = new RecordingHistoryOperation("move", new List<string>());

            Assert.True(service.IsAtSavedState);

            service.Record(operation);
            Assert.False(service.IsAtSavedState);

            service.MarkSaved();
            Assert.True(service.IsAtSavedState);

            service.Undo();
            Assert.False(service.IsAtSavedState);

            service.Reset();

            Assert.True(service.IsAtSavedState);
            Assert.False(service.CanUndo);
            Assert.False(service.CanRedo);
            Assert.False(service.IsApplyingHistory);
        }

        /// <summary>
        /// Ensures recording while one undo or redo mutation is being applied is rejected.
        /// </summary>
        [Fact]
        public void Record_while_history_is_applying_throws_an_invalid_operation_exception() {
            EditorUndoRedoService service = new EditorUndoRedoService(new EditorHistoryContext());
            NestedRecordingHistoryOperation operation = new NestedRecordingHistoryOperation(service);

            service.Record(operation);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Undo());

            Assert.Equal("Undo/redo operations cannot record new history while history is applying.", exception.Message);
            Assert.False(service.IsApplyingHistory);
        }
    }
}
