using System.Runtime.InteropServices;

namespace helengine.player {
    /// <summary>
    /// Managed mirror of the native <c>XINPUT_STATE</c> structure that <c>XInputGetState</c> fills.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputState {
        /// <summary>Packet counter that changes whenever the controller state changes.</summary>
        public uint PacketNumber;

        /// <summary>Raw controller report.</summary>
        public XInputGamepad Gamepad;
    }
}
