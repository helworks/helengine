namespace helengine;

/// <summary>
/// Stores the resolved state for one logical action during a frame.
/// </summary>
public struct InputActionState {
    /// <summary>
    /// Gets or sets the current normalized value of the action.
    /// </summary>
    public float Value { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the action is currently active.
    /// </summary>
    public bool IsDown { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the action transitioned to active on this frame.
    /// </summary>
    public bool WasPressed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the action transitioned to inactive on this frame.
    /// </summary>
    public bool WasReleased { get; set; }
}
