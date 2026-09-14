using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies scene-transition coordination delegates to the existing selection and history owners.
/// </summary>
public sealed class EditorSceneWorkspaceCoordinatorTests {
    /// <summary>
    /// Ensures one transition clears selection and resets history exactly once.
    /// </summary>
    [Fact]
    public void ResetForSceneTransition_ClearsSelectionAndHistory() {
        EditorSelectionService selection = new();
        RecordingUndoRedoService history = new();
        EditorSceneWorkspaceCoordinator coordinator = new(selection, history);
        int selectionChanges = 0;
        selection.SelectionChanged += _ => selectionChanges++;

        coordinator.ResetForSceneTransition();

        Assert.Equal(1, selectionChanges);
        Assert.Equal(1, history.ResetCount);
    }

    sealed class RecordingUndoRedoService : IEditorUndoRedoService {
        public int ResetCount { get; private set; }
        public bool CanUndo => false;
        public bool CanRedo => false;
        public bool IsAtSavedState => true;
        public bool IsApplyingHistory => false;
        public void Record(IEditorHistoryOperation operation) { }
        public bool Undo() { return false; }
        public bool Redo() { return false; }
        public void MarkSaved() { }
        public void Reset() { ResetCount++; }
    }
}
