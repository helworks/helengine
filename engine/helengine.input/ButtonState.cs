// // MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine;

/// <summary>
/// Defines a button state for buttons of mouse, gamepad or joystick.
/// </summary>
public enum ButtonState {
    /// <summary>
    /// The button is released.
    /// </summary>
    Released,

    /// <summary>
    /// The button is pressed.
    /// </summary>
    Pressed,

    /// <summary>
    /// The button transitioned from pressed to released this frame.
    /// </summary>
    JustReleased,

    /// <summary>
    /// The button transitioned from released to pressed this frame.
    /// </summary>
    JustPressed
}
