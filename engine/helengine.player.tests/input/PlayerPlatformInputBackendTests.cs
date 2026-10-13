using helengine.baseplatform.Definitions;

namespace helengine.player.tests.input {
    /// <summary>
    /// Verifies that the player forwards only the platform's declared input devices and builds gamepad slot 0 from the
    /// desktop controller merged with the keyboard-emulated pad.
    /// </summary>
    public sealed class PlayerPlatformInputBackendTests {
        /// <summary>
        /// A gamepad-only platform (PS2) sees no keyboard keys, no mouse movement or buttons, no pointer and no text, even
        /// while the desktop devices are busy.
        /// </summary>
        [Fact]
        public void CaptureFrame_WhenGamepadOnly_BlanksKeyboardMousePointerAndText() {
            FakeWrappedInputBackend wrapped = CreateBusyDesktop(Keys.Z);
            PlayerPlatformInputBackend backend = CreateBackend(wrapped, PlayerInputDevices.Gamepad, new FakePhysicalGamepadReader());

            InputFrameState frame = backend.CaptureFrame();

            Assert.Equal(0, frame.Keyboard.GetPressedKeyCount());
            Assert.Equal(new MouseState(), frame.Mouse);
            Assert.False(frame.Pointer.Connected);
            Assert.Equal(0UL, frame.Pointer.Buttons);
            Assert.Equal(0, frame.Text.CharacterCount);
        }

        /// <summary>Declared desktop devices pass through unchanged and no gamepad is reported when none is declared.</summary>
        [Fact]
        public void CaptureFrame_WhenKeyboardMousePointerDeclared_ForwardsThemAndReportsNoGamepad() {
            FakeWrappedInputBackend wrapped = CreateBusyDesktop(Keys.Z);
            FakePhysicalGamepadReader physical = new FakePhysicalGamepadReader { IsConnected = true };
            PlayerPlatformInputBackend backend = CreateBackend(
                wrapped,
                PlayerInputDevices.Keyboard | PlayerInputDevices.Mouse | PlayerInputDevices.Pointer,
                physical);

            InputFrameState frame = backend.CaptureFrame();

            Assert.True(frame.Keyboard.IsKeyDown(Keys.Z));
            Assert.Equal(wrapped.Frame.Mouse, frame.Mouse);
            Assert.True(frame.Pointer.Connected);
            Assert.Equal(1, frame.Text.CharacterCount);
            Assert.Equal(0, frame.GamepadCount);
            Assert.Equal(0, physical.ReadCount);
        }

        /// <summary>
        /// With the gamepad declared, slot 0 is the connected merge of the desktop controller and the keyboard pad.
        /// </summary>
        [Fact]
        public void CaptureFrame_WhenGamepadDeclared_MergesPhysicalAndKeyboardPadIntoSlotZero() {
            FakeWrappedInputBackend wrapped = CreateBusyDesktop(Keys.Z, Keys.D);
            InputGamepadState physicalPad = new InputGamepadState { Connected = true, LeftStickY = -20000 };
            physicalPad.SetButtonDown(InputGamepadButton.Start, true);
            FakePhysicalGamepadReader physical = new FakePhysicalGamepadReader { IsConnected = true, Gamepad = physicalPad };
            PlayerPlatformInputBackend backend = CreateBackend(wrapped, PlayerInputDevices.Gamepad, physical);

            InputFrameState frame = backend.CaptureFrame();

            Assert.Equal(1, frame.GamepadCount);
            InputGamepadState pad = frame.Gamepads[0];
            Assert.True(pad.Connected);
            Assert.True(pad.IsButtonDown(InputGamepadButton.South));
            Assert.True(pad.IsButtonDown(InputGamepadButton.Start));
            Assert.Equal(32767, pad.LeftStickX);
            Assert.Equal(-20000, pad.LeftStickY);
        }

        /// <summary>Without a desktop controller the keyboard pad alone fills a connected slot 0.</summary>
        [Fact]
        public void CaptureFrame_WhenNoPhysicalController_KeyboardPadStillConnected() {
            FakeWrappedInputBackend wrapped = CreateBusyDesktop(Keys.Enter);
            PlayerPlatformInputBackend backend = CreateBackend(wrapped, PlayerInputDevices.Gamepad, new FakePhysicalGamepadReader());

            InputFrameState frame = backend.CaptureFrame();

            Assert.Equal(1, frame.GamepadCount);
            Assert.True(frame.Gamepads[0].Connected);
            Assert.Equal(1UL << (int)InputGamepadButton.Start, frame.Gamepads[0].Buttons);
        }

        /// <summary>
        /// While the player window is in the background the desktop controller is ignored, matching the wrapped backend
        /// that already reports no keys then.
        /// </summary>
        [Fact]
        public void CaptureFrame_WhenWindowInBackground_IgnoresPhysicalController() {
            FakeWrappedInputBackend wrapped = CreateBusyDesktop();
            wrapped.IsForegroundActive = false;
            InputGamepadState physicalPad = new InputGamepadState { Connected = true };
            physicalPad.SetButtonDown(InputGamepadButton.South, true);
            FakePhysicalGamepadReader physical = new FakePhysicalGamepadReader { IsConnected = true, Gamepad = physicalPad };
            PlayerPlatformInputBackend backend = CreateBackend(wrapped, PlayerInputDevices.Gamepad, physical);

            InputFrameState frame = backend.CaptureFrame();

            Assert.Equal(0UL, frame.Gamepads[0].Buttons);
            Assert.Equal(0, physical.ReadCount);
        }

        /// <summary>When background input is enabled the desktop controller keeps working without focus.</summary>
        [Fact]
        public void CaptureFrame_WhenBackgroundInputEnabled_ReadsPhysicalControllerWithoutFocus() {
            FakeWrappedInputBackend wrapped = CreateBusyDesktop();
            wrapped.IsForegroundActive = false;
            InputGamepadState physicalPad = new InputGamepadState { Connected = true };
            physicalPad.SetButtonDown(InputGamepadButton.South, true);
            FakePhysicalGamepadReader physical = new FakePhysicalGamepadReader { IsConnected = true, Gamepad = physicalPad };
            PlayerPlatformInputBackend backend = CreateBackend(wrapped, PlayerInputDevices.Gamepad, physical);
            backend.ReceiveInputInBackground = true;

            InputFrameState frame = backend.CaptureFrame();

            Assert.True(wrapped.ReceiveInputInBackground);
            Assert.True(frame.Gamepads[0].IsButtonDown(InputGamepadButton.South));
        }

        /// <summary>The filter reports the wrapped backend's window focus unchanged.</summary>
        [Fact]
        public void IsForegroundActive_ForwardsWrappedFocus() {
            FakeWrappedInputBackend wrapped = CreateBusyDesktop();
            PlayerPlatformInputBackend backend = CreateBackend(wrapped, PlayerInputDevices.Gamepad, new FakePhysicalGamepadReader());

            Assert.True(backend.IsForegroundActive);
            wrapped.IsForegroundActive = false;
            Assert.False(backend.IsForegroundActive);
        }

        /// <summary>A platform without any device cannot be played, so the filter refuses an empty device set.</summary>
        [Fact]
        public void Ctor_WhenNoDevices_Throws() {
            FakeWrappedInputBackend wrapped = CreateBusyDesktop();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateBackend(wrapped, PlayerInputDevices.None, new FakePhysicalGamepadReader()));
        }

        /// <summary>
        /// Creates a focused fake desktop whose keyboard holds <paramref name="keys"/>, whose mouse is moved with the left
        /// button down, whose pointer is active and which typed one character.
        /// </summary>
        /// <param name="keys">Keys held on the desktop keyboard.</param>
        /// <returns>The fake wrapped backend.</returns>
        static FakeWrappedInputBackend CreateBusyDesktop(params Keys[] keys) {
            MouseState mouse = new MouseState { X = 120, Y = 80, LeftButton = ButtonState.Pressed };
            InputPointerState pointer = new InputPointerState { Connected = true, X = 120, Y = 80 };
            pointer.SetButtonDown(InputPointerButton.Primary, true);
            return new FakeWrappedInputBackend {
                IsForegroundActive = true,
                Frame = new InputFrameState {
                    Keyboard = new KeyboardState(keys),
                    Mouse = mouse,
                    Pointer = pointer,
                    Text = new InputTextState { Characters = new[] { 'z' }, CharacterCount = 1 }
                }
            };
        }

        /// <summary>
        /// Creates the filter under test with the default keyboard pad map.
        /// </summary>
        /// <param name="wrapped">Fake desktop backend.</param>
        /// <param name="devices">Devices the platform declares.</param>
        /// <param name="physical">Fake desktop controller.</param>
        /// <returns>The filter under test.</returns>
        static PlayerPlatformInputBackend CreateBackend(FakeWrappedInputBackend wrapped, PlayerInputDevices devices, FakePhysicalGamepadReader physical) {
            return new PlayerPlatformInputBackend(wrapped, wrapped, devices, physical, PlayerKeyboardGamepadMap.CreateDefault());
        }
    }
}
