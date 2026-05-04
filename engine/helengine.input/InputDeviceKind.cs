namespace helengine;

/// <summary>
/// Describes the family that owns a control.
/// </summary>
public enum InputDeviceKind {
    /// <summary>
    /// A gamepad or gamepad-style controller.
    /// </summary>
    Gamepad,
    /// <summary>
    /// A pointer device such as a mouse or pen cursor.
    /// </summary>
    Pointer
#if HELENGINE_INPUT_KEYBOARD
    ,
    /// <summary>
    /// A keyboard device compiled in for targets that support it.
    /// </summary>
    Keyboard
#endif
#if HELENGINE_INPUT_MOUSE
    ,
    /// <summary>
    /// A mouse device compiled in for targets that support it.
    /// </summary>
    Mouse
#endif
}
