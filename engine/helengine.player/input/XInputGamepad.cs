using System.Runtime.InteropServices;

namespace helengine.player {
    /// <summary>
    /// Managed mirror of the native <c>XINPUT_GAMEPAD</c> report: the raw button bits, trigger bytes and thumbstick
    /// values of one XInput controller, in the native field order so it can be filled by <c>XInputGetState</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepad {
        /// <summary>Bitmask of the held digital buttons (<c>XINPUT_GAMEPAD_*</c> flags).</summary>
        public ushort Buttons;

        /// <summary>Left trigger position, 0 (released) to 255 (fully pulled).</summary>
        public byte LeftTrigger;

        /// <summary>Right trigger position, 0 (released) to 255 (fully pulled).</summary>
        public byte RightTrigger;

        /// <summary>Left thumbstick horizontal position; negative is left.</summary>
        public short LeftThumbX;

        /// <summary>Left thumbstick vertical position; positive is up.</summary>
        public short LeftThumbY;

        /// <summary>Right thumbstick horizontal position; negative is left.</summary>
        public short RightThumbX;

        /// <summary>Right thumbstick vertical position; positive is up.</summary>
        public short RightThumbY;
    }
}
