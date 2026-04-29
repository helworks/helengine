using Avalonia.Input;

namespace helengine.editor.launcher.Theme;

/// <summary>
/// Centralizes launcher cursor definitions so interactive controls use the same pointer affordances.
/// </summary>
public static class LauncherCursors {
    /// <summary>
    /// Gets the hand cursor used for clickable launcher controls such as action buttons and project cards.
    /// </summary>
    public static Cursor Hand { get; } = new(StandardCursorType.Hand);
}
