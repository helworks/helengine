namespace helengine;

/// <summary>
/// Captures the state of one pointer device for the current frame.
/// </summary>
public struct InputPointerState {
    /// <summary>
    /// Gets or sets a value indicating whether the pointer is currently available.
    /// </summary>
    public bool Connected { get; set; }

    /// <summary>
    /// Gets or sets the active pointer button bitmask.
    /// </summary>
    public ulong Buttons { get; set; }

    /// <summary>
    /// Gets or sets the absolute X position of the pointer.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the absolute Y position of the pointer.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Gets or sets the pointer delta on the X axis.
    /// </summary>
    public int DeltaX { get; set; }

    /// <summary>
    /// Gets or sets the pointer delta on the Y axis.
    /// </summary>
    public int DeltaY { get; set; }

    /// <summary>
    /// Gets or sets the pointer scroll delta.
    /// </summary>
    public int ScrollDelta { get; set; }

    /// <summary>
    /// Returns whether a pointer button is currently down.
    /// </summary>
    /// <param name="button">Button to query.</param>
    /// <returns>True when the button bit is active.</returns>
    public bool IsButtonDown(InputPointerButton button) {
        return (Buttons & (1UL << (int)button)) != 0;
    }

    /// <summary>
    /// Sets the state of one pointer button.
    /// </summary>
    /// <param name="button">Button to update.</param>
    /// <param name="isDown">True to mark the button active; false to clear it.</param>
    public void SetButtonDown(InputPointerButton button, bool isDown) {
        ulong mask = 1UL << (int)button;
        if (isDown) {
            Buttons = Buttons | mask;
        } else {
            Buttons = Buttons & ~mask;
        }
    }
}
