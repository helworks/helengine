using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace helengine {
    /// <summary>
    /// Captures raw keyboard and mouse state from a Windows host window.
    /// </summary>
    public sealed class InputBackendWindows : IInputBackend {
        /// <summary>
        /// Reads the current keyboard state for the entire desktop.
        /// </summary>
        /// <param name="lpKeyState">Receives the keyboard bitfield.</param>
        /// <returns>True when the keyboard state was captured.</returns>
        [DllImport("user32.dll")]
        static extern bool GetKeyboardState(byte[] lpKeyState);

        /// <summary>
        /// Reads the global cursor position.
        /// </summary>
        /// <param name="pt">Receives the screen-space cursor position.</param>
        /// <returns>True when the cursor position was retrieved.</returns>
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out Point pt);

        /// <summary>
        /// Maps a point between window coordinate spaces.
        /// </summary>
        /// <param name="hWndFrom">Source window handle.</param>
        /// <param name="hWndTo">Destination window handle.</param>
        /// <param name="pt">Point to map.</param>
        /// <param name="cPoints">Number of points to map.</param>
        /// <returns>Number of points mapped.</returns>
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        static extern int MapWindowPoints(HandleRef hWndFrom, HandleRef hWndTo, out Point pt, int cPoints);

        /// <summary>
        /// Gets the handle for the current foreground window.
        /// </summary>
        /// <returns>Foreground window handle.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// Determines whether the specified window is a child of another window.
        /// </summary>
        /// <param name="parentWindow">Candidate parent window handle.</param>
        /// <param name="childWindow">Candidate child window handle.</param>
        /// <returns>True when <paramref name="childWindow"/> is a child of <paramref name="parentWindow"/>.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsChild(IntPtr parentWindow, IntPtr childWindow);

        /// <summary>
        /// Window control used to convert screen coordinates into client coordinates.
        /// </summary>
        readonly Control Window;
        /// <summary>
        /// Tracks whether the mouse wheel has accumulated delta for the current frame.
        /// </summary>
        int ScrollWheelAccumulator;
        /// <summary>
        /// Cached mouse state updated on each capture.
        /// </summary>
        MouseState CurrentMouseState;
        /// <summary>
        /// Known keyboard buttons captured from the current frame.
        /// </summary>
        readonly List<Keys> CapturedKeys;
        /// <summary>
        /// Cached keyboard bitfield used to capture key releases and presses.
        /// </summary>
        readonly byte[] KeyStateBytes;
        /// <summary>
        /// Precomputed key codes that map to the engine's keyboard enum.
        /// </summary>
        readonly byte[] DefinedKeyCodes;

        /// <summary>
        /// Initializes a new Windows input backend bound to one window handle.
        /// </summary>
        /// <param name="windowHandle">Handle to the window receiving input.</param>
        public InputBackendWindows(IntPtr windowHandle) {
            Window = Control.FromHandle(windowHandle);
            if (Window == null) {
                throw new ArgumentException("Window handle must refer to a valid control.", nameof(windowHandle));
            }

            KeyStateBytes = new byte[256];
            CapturedKeys = new List<Keys>(10);
            DefinedKeyCodes = BuildDefinedKeyCodes();
            Window.MouseWheel += OnMouseWheel;
        }

        /// <summary>
        /// Captures the current raw input frame from the attached window.
        /// </summary>
        /// <returns>Captured input frame.</returns>
        public InputFrameState CaptureFrame() {
            InputFrameState frame = new InputFrameState();
            if (!IsWindowReady()) {
                return frame;
            }

            frame.Mouse = CaptureMouseState();
            frame.Keyboard = CaptureKeyboardState();
            return frame;
        }

        /// <summary>
        /// Builds the keyboard scan-code list used to query the desktop state.
        /// </summary>
        /// <returns>Known key codes supported by the backend.</returns>
        byte[] BuildDefinedKeyCodes() {
            Keys[] values = (Keys[])Enum.GetValues(typeof(Keys));
            List<byte> keyCodes = new List<byte>(Math.Min(values.Length, 255));
            for (int i = 0; i < values.Length; i++) {
                int keyCode = (int)values[i];
                if (keyCode < 1 || keyCode > 255) {
                    continue;
                }

                keyCodes.Add((byte)keyCode);
            }

            return keyCodes.ToArray();
        }

        /// <summary>
        /// Captures the current keyboard state.
        /// </summary>
        /// <returns>Keyboard state for the current frame.</returns>
        KeyboardState CaptureKeyboardState() {
            if (!IsWindowForegroundActive()) {
                CapturedKeys.Clear();
                return new KeyboardState();
            }

            if (!GetKeyboardState(KeyStateBytes)) {
                return new KeyboardState(CapturedKeys, Console.CapsLock, Console.NumberLock);
            }

            CapturedKeys.RemoveAll(key => IsKeyReleased((byte)key));
            for (int i = 0; i < DefinedKeyCodes.Length; i++) {
                byte keyCode = DefinedKeyCodes[i];
                if (IsKeyReleased(keyCode)) {
                    continue;
                }

                Keys key = (Keys)keyCode;
                if (!CapturedKeys.Contains(key)) {
                    CapturedKeys.Add(key);
                }
            }

            return new KeyboardState(CapturedKeys, Console.CapsLock, Console.NumberLock);
        }

        /// <summary>
        /// Captures the current mouse state relative to the attached window.
        /// </summary>
        /// <returns>Mouse state for the current frame.</returns>
        MouseState CaptureMouseState() {
            MouseState mouseState = CurrentMouseState;
            mouseState.ScrollWheelValue = ScrollWheelAccumulator;

            Point pos;
            if (GetCursorPos(out pos)) {
                MapWindowPoints(new HandleRef(null, IntPtr.Zero), new HandleRef(Window, Window.Handle), out pos, 1);
                mouseState.X = pos.X;
                mouseState.Y = pos.Y;
            }

            if (!IsWindowForegroundActive()) {
                ReleaseAllButtons(ref mouseState);
            } else {
                MouseButtons buttons = Control.MouseButtons;
                mouseState.LeftButton = (buttons & MouseButtons.Left) == MouseButtons.Left ? ButtonState.Pressed : ButtonState.Released;
                mouseState.MiddleButton = (buttons & MouseButtons.Middle) == MouseButtons.Middle ? ButtonState.Pressed : ButtonState.Released;
                mouseState.RightButton = (buttons & MouseButtons.Right) == MouseButtons.Right ? ButtonState.Pressed : ButtonState.Released;
                mouseState.XButton1 = (buttons & MouseButtons.XButton1) == MouseButtons.XButton1 ? ButtonState.Pressed : ButtonState.Released;
                mouseState.XButton2 = (buttons & MouseButtons.XButton2) == MouseButtons.XButton2 ? ButtonState.Pressed : ButtonState.Released;
            }

            CurrentMouseState = mouseState;
            return mouseState;
        }

        /// <summary>
        /// Determines whether the window is valid for coordinate mapping.
        /// </summary>
        /// <returns>True when the window exists and has a valid handle.</returns>
        bool IsWindowReady() {
            return Window != null && !Window.IsDisposed && !Window.Disposing && Window.IsHandleCreated;
        }

        /// <summary>
        /// Determines whether the target window currently owns foreground input focus.
        /// </summary>
        /// <returns>True when the target window is foreground or contains the foreground child window.</returns>
        bool IsWindowForegroundActive() {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero) {
                return false;
            }

            IntPtr windowHandle = Window.Handle;
            if (foregroundWindow == windowHandle) {
                return true;
            }

            return IsChild(windowHandle, foregroundWindow);
        }

        /// <summary>
        /// Returns whether one captured key remains pressed in the current scan code state.
        /// </summary>
        /// <param name="key">Key code to inspect.</param>
        /// <returns>True when the key is still down.</returns>
        bool IsKeyReleased(byte key) {
            return (KeyStateBytes[key] & 0x80) == 0;
        }

        /// <summary>
        /// Clears all mouse buttons so inactive windows cannot receive click interactions.
        /// </summary>
        /// <param name="mouseState">Mouse state to clear.</param>
        void ReleaseAllButtons(ref MouseState mouseState) {
            mouseState.LeftButton = ButtonState.Released;
            mouseState.MiddleButton = ButtonState.Released;
            mouseState.RightButton = ButtonState.Released;
            mouseState.XButton1 = ButtonState.Released;
            mouseState.XButton2 = ButtonState.Released;
        }

        /// <summary>
        /// Handles MouseWheel events by accumulating the scroll delta.
        /// </summary>
        /// <param name="sender">The control raising the event.</param>
        /// <param name="e">Mouse event arguments containing the wheel delta.</param>
        void OnMouseWheel(object sender, MouseEventArgs e) {
            ScrollWheelAccumulator += e.Delta;
        }
    }
}

