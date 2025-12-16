namespace helengine;

/// <summary>
/// Describes how the pointer is currently interacting with an element.
/// </summary>
public enum PointerInteraction {
    /// <summary>
    /// No pointer interaction is occurring.
    /// </summary>
    None,
    /// <summary>
    /// Pointer is hovering over the element.
    /// </summary>
    Hover,
    /// <summary>
    /// Pointer has left the element.
    /// </summary>
    Leave,
    /// <summary>
    /// Pointer has initiated a press action.
    /// </summary>
    Press,
    /// <summary>
    /// Pointer has released a press action.
    /// </summary>
    Release
}
