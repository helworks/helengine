// // MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

using System.Runtime.InteropServices;

namespace helengine {
    public class MouseWindows : Mouse {
        [DllImportAttribute("user32.dll", EntryPoint = "SetCursorPos")]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point pt);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        internal static extern int MapWindowPoints(HandleRef hWndFrom, HandleRef hWndTo, out Point pt, int cPoints);

        private Control _window;
        private MouseState mouseState;

        public MouseWindows(IntPtr windowHandle) {
            _window = Control.FromHandle(windowHandle);
        }

        public override MouseState GetState() {
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

        public override void SetPosition(int x, int y) {
            //PrimaryWindow.MouseState.X = x;
            //PrimaryWindow.MouseState.Y = y;

            var pt = _window.PointToScreen(new Point(x, y));
            SetCursorPos(pt.X, pt.Y);
        }
    }
}
