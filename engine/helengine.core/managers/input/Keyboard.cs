// // MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

using System;

namespace helengine {
    /// <summary>
    /// Allows getting keystrokes from keyboard.
    /// </summary>
	public abstract class Keyboard
	{
        /// <summary>
        /// Returns the current keyboard state.
        /// </summary>
        /// <returns>Current keyboard state.</returns>
		public abstract KeyboardState GetState();
	}
}
