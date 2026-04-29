// // MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.
namespace helengine {
    /// <summary>
    /// Allows reading position and button click information from mouse.
    /// </summary>
    public abstract class Mouse {
        /// <summary>
        /// Default mouse state used when no platform-specific state is available.
        /// </summary>
        private static readonly MouseState _defaultState = new MouseState();

        /// <summary>
        /// Gets mouse state information that includes position and button presses
        /// for the primary window
        /// </summary>
        /// <returns>Current state of the mouse.</returns>
        public abstract MouseState GetState();

        /// <summary>
        /// Sets mouse cursor's relative position to game-window.
        /// </summary>
        /// <param name="x">Relative horizontal position of the cursor.</param>
        /// <param name="y">Relative vertical position of the cursor.</param>
        public abstract void SetPosition(int x, int y);

        /// <summary>
        /// Enables or disables client-edge pointer wrapping for the current backend.
        /// </summary>
        /// <param name="isEnabled">True when active interactions should wrap at the client edges.</param>
        public virtual void SetPointerWrapEnabled(bool isEnabled) {
        }

        /// <summary>
        /// Returns and clears the delta offset produced by the most recent pointer wrap.
        /// </summary>
        /// <returns>
        /// Offset that must be added to the wrapped cursor delta so callers see continuous movement
        /// instead of the teleport distance.
        /// </returns>
        public virtual int2 ConsumePointerWrapDeltaOffset() {
            return new int2(_defaultState.X, _defaultState.Y);
        }
    }
}
