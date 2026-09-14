namespace helengine.editor;

/// <summary>
/// Coordinates scene-transition reset of the existing selection and history owners.
/// </summary>
public sealed class EditorSceneWorkspaceCoordinator {
    readonly EditorSelectionService SelectionService;
    readonly IEditorUndoRedoService HistoryService;

    /// <summary>
    /// Initializes a scene workspace coordinator from the existing scene-scoped services.
    /// </summary>
    /// <param name="selectionService">Selection owner.</param>
    /// <param name="historyService">Undo/redo owner.</param>
    public EditorSceneWorkspaceCoordinator(
        EditorSelectionService selectionService,
        IEditorUndoRedoService historyService) {
        SelectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
        HistoryService = historyService ?? throw new ArgumentNullException(nameof(historyService));
    }

    /// <summary>
    /// Clears scene selection before entity teardown and resets the existing history cursor.
    /// </summary>
    public void ResetForSceneTransition() {
        SelectionService.ClearSelection();
        HistoryService.Reset();
    }
}
