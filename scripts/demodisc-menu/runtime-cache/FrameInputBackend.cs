namespace DemoDisc.MenuContractTests {
    /// <summary>Supplies deterministic device frames to the real input system.</summary>
    internal sealed class FrameInputBackend : IInputBackend {
        /// <summary>Input frame returned on the next capture.</summary>
        public InputFrameState Frame { get; set; }

        /// <summary>Allows synthetic input regardless of the foreground window.</summary>
        public bool ReceiveInputInBackground { get; set; }

        /// <summary>Returns the explicitly configured raw devices.</summary>
        /// <returns>One raw input frame.</returns>
        public InputFrameState CaptureFrame() {
            return Frame;
        }
    }
}
