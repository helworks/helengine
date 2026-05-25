namespace helengine;

/// <summary>
/// Describes one standard platform action mapped to one physical control.
/// </summary>
public sealed class StandardPlatformActionBinding {
    /// <summary>
    /// Initializes one standard platform action binding.
    /// </summary>
    /// <param name="action">Engine-owned platform action represented by the binding.</param>
    /// <param name="control">Physical control that should drive the platform action.</param>
    public StandardPlatformActionBinding(StandardPlatformAction action, InputControlId control) {
        Action = action;
        Control = control;
    }

    /// <summary>
    /// Gets the engine-owned platform action represented by this binding.
    /// </summary>
    public StandardPlatformAction Action { get; }

    /// <summary>
    /// Gets the physical control that should drive the platform action.
    /// </summary>
    public InputControlId Control { get; }
}
