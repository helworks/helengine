using System.Runtime.InteropServices;

namespace helengine.player {
    /// <summary>
    /// Reads the first connected XInput controller (Xbox-style pads and anything exposed through XInput) through
    /// <c>xinput1_4.dll</c>. When the DLL cannot be loaded or no controller is connected the reader simply reports no
    /// controller; neither case throws, because both are ordinary desktop states.
    /// </summary>
    public sealed class XInputPlayerGamepadReader : IPlayerPhysicalGamepadReader {
        /// <summary>XInput library shipped with Windows 8 and later.</summary>
        public const string XInputLibraryName = "xinput1_4.dll";

        /// <summary>Number of controller slots XInput exposes.</summary>
        public const uint MaxControllerCount = 4;

        /// <summary>
        /// Number of reads between scans for a newly connected controller while none is connected; polling disconnected
        /// XInput slots is slow, so it is not done every frame.
        /// </summary>
        public const int DisconnectedRescanInterval = 60;

        /// <summary><c>ERROR_SUCCESS</c>: the controller is connected and its state was read.</summary>
        const uint ErrorSuccess = 0;

        /// <summary>Marker for "no controller slot selected".</summary>
        const int NoController = -1;

        /// <summary>
        /// Reads the current state of one XInput controller slot.
        /// </summary>
        /// <param name="userIndex">Controller slot, 0 to 3.</param>
        /// <param name="state">Receives the controller state.</param>
        /// <returns><c>ERROR_SUCCESS</c> when connected; <c>ERROR_DEVICE_NOT_CONNECTED</c> otherwise.</returns>
        [DllImport(XInputLibraryName, EntryPoint = "XInputGetState", ExactSpelling = true)]
        static extern uint XInputGetState(uint userIndex, out XInputState state);

        /// <summary>Whether <c>xinput1_4.dll</c> loaded; when false the reader never calls into it.</summary>
        readonly bool IsLibraryAvailable;

        /// <summary>Slot of the controller read last frame, or <see cref="NoController"/>.</summary>
        int ControllerIndex;

        /// <summary>Reads left until the next scan for a controller while none is connected.</summary>
        int ReadsUntilRescan;

        /// <summary>
        /// Initializes the reader, probing once whether the XInput library can be loaded on this machine.
        /// </summary>
        public XInputPlayerGamepadReader() {
            IsLibraryAvailable = NativeLibrary.TryLoad(XInputLibraryName, typeof(XInputPlayerGamepadReader).Assembly, null, out IntPtr _);
            ControllerIndex = NoController;
            ReadsUntilRescan = 0;
        }

        /// <summary>
        /// Gets whether the XInput library loaded; false means desktop controllers are unavailable and only the keyboard
        /// pad drives the game.
        /// </summary>
        public bool IsAvailable => IsLibraryAvailable;

        /// <summary>
        /// Reads the controller used last frame, or scans the XInput slots for the first connected controller.
        /// </summary>
        /// <param name="gamepad">Receives the mapped controller state, or a neutral pad when none is connected.</param>
        /// <returns>True when a controller was connected and read.</returns>
        public bool TryReadGamepad(out InputGamepadState gamepad) {
            gamepad = new InputGamepadState();
            if (!IsLibraryAvailable) {
                return false;
            }

            XInputState state;
            if (ControllerIndex != NoController && XInputGetState((uint)ControllerIndex, out state) == ErrorSuccess) {
                gamepad = XInputGamepadMapper.Map(state.Gamepad);
                return true;
            }

            ControllerIndex = NoController;
            if (ReadsUntilRescan > 0) {
                ReadsUntilRescan--;
                return false;
            }

            for (uint index = 0; index < MaxControllerCount; index++) {
                if (XInputGetState(index, out state) == ErrorSuccess) {
                    ControllerIndex = (int)index;
                    gamepad = XInputGamepadMapper.Map(state.Gamepad);
                    return true;
                }
            }

            ReadsUntilRescan = DisconnectedRescanInterval;
            return false;
        }
    }
}
