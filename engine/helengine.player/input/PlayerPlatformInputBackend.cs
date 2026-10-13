using helengine.baseplatform.Definitions;

namespace helengine.player {
    /// <summary>
    /// Input backend the Play player hands to the core: it wraps the desktop backend and shows the game only the input
    /// devices the selected platform's hardware has. Undeclared devices are reported idle (no keys, a still mouse with
    /// no buttons, an inactive pointer, no text). When the platform has a gamepad, slot 0 is the merge of the first
    /// desktop controller and a keyboard-emulated pad; the keyboard drives that pad even when keyboard input itself is
    /// hidden from the game.
    /// </summary>
    public sealed class PlayerPlatformInputBackend : IInputBackend, IInputForegroundState {
        /// <summary>Desktop backend that captures the raw keyboard, mouse and pointer state.</summary>
        readonly IInputBackend Wrapped;

        /// <summary>Focus state of the window the wrapped backend reads, used to ignore the controller while in the background.</summary>
        readonly IInputForegroundState Foreground;

        /// <summary>Devices the platform declares; everything else is reported idle.</summary>
        readonly PlayerInputDevices Devices;

        /// <summary>Desktop controller source for gamepad slot 0.</summary>
        readonly IPlayerPhysicalGamepadReader PhysicalGamepad;

        /// <summary>Keyboard-emulated pad merged into gamepad slot 0.</summary>
        readonly PlayerKeyboardGamepadMap KeyboardGamepad;

        /// <summary>
        /// Initializes the device filter.
        /// </summary>
        /// <param name="wrapped">Desktop backend that captures raw input.</param>
        /// <param name="foreground">Focus state of the window <paramref name="wrapped"/> reads (normally the same object).</param>
        /// <param name="devices">Non-empty set of devices the platform declares.</param>
        /// <param name="physicalGamepad">Desktop controller source for gamepad slot 0.</param>
        /// <param name="keyboardGamepad">Keyboard-emulated pad merged into gamepad slot 0.</param>
        /// <exception cref="ArgumentNullException">A collaborator is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The device set is empty or names unknown devices.</exception>
        public PlayerPlatformInputBackend(
            IInputBackend wrapped,
            IInputForegroundState foreground,
            PlayerInputDevices devices,
            IPlayerPhysicalGamepadReader physicalGamepad,
            PlayerKeyboardGamepadMap keyboardGamepad) {
            if (devices == PlayerInputDevices.None || (devices & ~PlayerInputDevicesText.AllDevices) != PlayerInputDevices.None) {
                throw new ArgumentOutOfRangeException(nameof(devices), devices, "The player needs at least one known input device.");
            }

            Wrapped = wrapped ?? throw new ArgumentNullException(nameof(wrapped));
            Foreground = foreground ?? throw new ArgumentNullException(nameof(foreground));
            PhysicalGamepad = physicalGamepad ?? throw new ArgumentNullException(nameof(physicalGamepad));
            KeyboardGamepad = keyboardGamepad ?? throw new ArgumentNullException(nameof(keyboardGamepad));
            Devices = devices;
        }

        /// <summary>
        /// Gets or sets whether input keeps flowing while the player window is in the background; forwarded to the
        /// wrapped backend and also applied to the desktop controller.
        /// </summary>
        public bool ReceiveInputInBackground {
            get => Wrapped.ReceiveInputInBackground;
            set => Wrapped.ReceiveInputInBackground = value;
        }

        /// <summary>
        /// Gets whether the player window owns foreground focus, as reported by the wrapped backend.
        /// </summary>
        public bool IsForegroundActive => Foreground.IsForegroundActive;

        /// <summary>
        /// Captures one frame from the wrapped backend, idles every undeclared device and, when the platform has a gamepad,
        /// fills slot 0 from the desktop controller merged with the keyboard pad.
        /// </summary>
        /// <returns>The filtered input frame.</returns>
        public InputFrameState CaptureFrame() {
            InputFrameState frame = Wrapped.CaptureFrame();
            KeyboardState keyboard = frame.Keyboard;

            if (!HasDevice(PlayerInputDevices.Keyboard)) {
                frame.Keyboard = new KeyboardState();
                frame.Text = new InputTextState();
            }
            if (!HasDevice(PlayerInputDevices.Mouse)) {
                frame.Mouse = new MouseState();
            }
            if (!HasDevice(PlayerInputDevices.Pointer)) {
                frame.Pointer = new InputPointerState();
            }

            if (HasDevice(PlayerInputDevices.Gamepad)) {
                InputGamepadState physical = new InputGamepadState();
                if (ReceiveInputInBackground || Foreground.IsForegroundActive) {
                    PhysicalGamepad.TryReadGamepad(out physical);
                }

                InputGamepadState merged = PlayerGamepadMerger.Merge(physical, KeyboardGamepad.Map(keyboard));
                frame.Gamepads = new[] { merged };
                frame.GamepadCount = 1;
            } else {
                frame.Gamepads = Array.Empty<InputGamepadState>();
                frame.GamepadCount = 0;
            }

            return frame;
        }

        /// <summary>
        /// Reports whether the platform declares one device.
        /// </summary>
        /// <param name="device">Single device flag.</param>
        /// <returns>True when the device is declared.</returns>
        bool HasDevice(PlayerInputDevices device) {
            return (Devices & device) != PlayerInputDevices.None;
        }
    }
}
