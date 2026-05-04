namespace helengine;

/// <summary>
/// Captures all raw input for a single frame.
/// </summary>
public struct InputFrameState {
    /// <summary>
    /// Gets or sets the captured keyboard state for the current frame.
    /// </summary>
    public KeyboardState Keyboard { get; set; }

    /// <summary>
    /// Gets or sets the captured mouse state for the current frame.
    /// </summary>
    public MouseState Mouse { get; set; }

    /// <summary>
    /// Gets or sets the current pointer device state.
    /// </summary>
    public InputPointerState Pointer { get; set; }

    /// <summary>
    /// Gets or sets the captured gamepad states.
    /// </summary>
    public InputGamepadState[] Gamepads { get; set; }

    /// <summary>
    /// Gets or sets the number of valid gamepad entries in <see cref="Gamepads"/>.
    /// </summary>
    public int GamepadCount { get; set; }

    /// <summary>
    /// Gets or sets the captured text input state.
    /// </summary>
    public InputTextState Text { get; set; }
}
