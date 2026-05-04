namespace helengine;

/// <summary>
/// Captures the state of one abstract gamepad device for the current frame.
/// </summary>
public struct InputGamepadState {
    /// <summary>
    /// Gets or sets a value indicating whether the device is currently connected.
    /// </summary>
    public bool Connected { get; set; }

    /// <summary>
    /// Gets or sets the bitmask of active buttons on the gamepad.
    /// </summary>
    public ulong Buttons { get; set; }

    /// <summary>
    /// Gets or sets the left stick horizontal axis.
    /// </summary>
    public short LeftStickX { get; set; }

    /// <summary>
    /// Gets or sets the left stick vertical axis.
    /// </summary>
    public short LeftStickY { get; set; }

    /// <summary>
    /// Gets or sets the right stick horizontal axis.
    /// </summary>
    public short RightStickX { get; set; }

    /// <summary>
    /// Gets or sets the right stick vertical axis.
    /// </summary>
    public short RightStickY { get; set; }

    /// <summary>
    /// Gets or sets the left trigger value.
    /// </summary>
    public short LeftTrigger { get; set; }

    /// <summary>
    /// Gets or sets the right trigger value.
    /// </summary>
    public short RightTrigger { get; set; }

    /// <summary>
    /// Returns whether an abstract gamepad button is currently down.
    /// </summary>
    /// <param name="button">Button to query.</param>
    /// <returns>True when the button bit is active.</returns>
    public bool IsButtonDown(InputGamepadButton button) {
        return (Buttons & (1UL << (int)button)) != 0;
    }

    /// <summary>
    /// Sets the state of one abstract gamepad button.
    /// </summary>
    /// <param name="button">Button to update.</param>
    /// <param name="isDown">True to mark the button active; false to clear it.</param>
    public void SetButtonDown(InputGamepadButton button, bool isDown) {
        ulong mask = 1UL << (int)button;
        if (isDown) {
            Buttons = Buttons | mask;
        } else {
            Buttons = Buttons & ~mask;
        }
    }
}
