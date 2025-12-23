// // MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

using System.Runtime.InteropServices;

namespace helengine {
    /// <summary>
    /// Windows-specific mouse implementation backed by user32 APIs.
    /// </summary>
    public class MouseWindows : Mouse {
        /// <summary>
        /// Updates the global cursor position.
        /// </summary>
        /// <param name="X">Screen-space X coordinate.</param>
        /// <param name="Y">Screen-space Y coordinate.</param>
        /// <returns>True when the cursor position was updated.</returns>
        [DllImportAttribute("user32.dll", EntryPoint = "SetCursorPos")]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int X, int Y);

        /// <summary>
        /// Reads the global cursor position.
        /// </summary>
        /// <param name="pt">Receives the screen-space cursor position.</param>
        /// <returns>True when the cursor position was retrieved.</returns>
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point pt);

        /// <summary>
        /// Maps a point between window coordinate spaces.
        /// </summary>
        /// <param name="hWndFrom">Source window handle.</param>
        /// <param name="hWndTo">Destination window handle.</param>
        /// <param name="pt">Point to map.</param>
        /// <param name="cPoints">Number of points to map.</param>
        /// <returns>Number of points mapped.</returns>
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        internal static extern int MapWindowPoints(HandleRef hWndFrom, HandleRef hWndTo, out Point pt, int cPoints);

        /// <summary>
        /// Window control used to convert screen coordinates into client coordinates.
        /// </summary>
        private Control _window;
        /// <summary>
        /// Cached mouse state updated on each query.
        /// </summary>
        private MouseState mouseState;

        /// <summary>
        /// Initializes a new Windows mouse tied to a window handle.
        /// </summary>
        /// <param name="windowHandle">Handle to the window receiving input.</param>
        public MouseWindows(IntPtr windowHandle) {
            _window = Control.FromHandle(windowHandle);
        }

        /// <summary>
        /// Gets the current mouse state relative to the target window.
        /// </summary>
        /// <returns>Mouse state snapshot.</returns>
        public override MouseState GetState() {
            if (!IsWindowReady()) {
                return mouseState;
            }

            Point pos;
            GetCursorPos(out pos);

            MapWindowPoints(new HandleRef(null, IntPtr.Zero), new HandleRef(_window, _window.Handle), out pos, 1);

            mouseState.X = pos.X;
            mouseState.Y = pos.Y;

            var buttons = Control.MouseButtons;
            mouseState.LeftButton = (buttons & MouseButtons.Left) == MouseButtons.Left ? ButtonState.Pressed : ButtonState.Released;
            mouseState.MiddleButton = (buttons & MouseButtons.Middle) == MouseButtons.Middle ? ButtonState.Pressed : ButtonState.Released;
            mouseState.RightButton = (buttons & MouseButtons.Right) == MouseButtons.Right ? ButtonState.Pressed : ButtonState.Released;
            mouseState.XButton1 = (buttons & MouseButtons.XButton1) == MouseButtons.XButton1 ? ButtonState.Pressed : ButtonState.Released;
            mouseState.XButton2 = (buttons & MouseButtons.XButton2) == MouseButtons.XButton2 ? ButtonState.Pressed : ButtonState.Released;

            return mouseState;
        }

        /// <summary>
        /// Sets the cursor position relative to the target window.
        /// </summary>
        /// <param name="x">Client-space X coordinate.</param>
        /// <param name="y">Client-space Y coordinate.</param>
        public override void SetPosition(int x, int y) {
            if (!IsWindowReady()) {
                return;
            }

            //PrimaryWindow.MouseState.X = x;
            //PrimaryWindow.MouseState.Y = y;

            var pt = _window.PointToScreen(new Point(x, y));
            SetCursorPos(pt.X, pt.Y);
        }

        /// <summary>
        /// Determines whether the window is valid for coordinate mapping.
        /// </summary>
        /// <returns>True when the window exists and has a valid handle.</returns>
        bool IsWindowReady() {
            return _window != null && !_window.IsDisposed && !_window.Disposing && _window.IsHandleCreated;
        }
    }
}
