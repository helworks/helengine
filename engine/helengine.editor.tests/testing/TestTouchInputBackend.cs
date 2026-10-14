namespace helengine.editor.tests.testing {
    /// <summary>Feeds deterministic contact lifetimes through the production tracker without a native window.</summary>
    internal sealed class TestTouchInputBackend : IInputBackend, IDisposable {
        /// <summary>Owns the same contact tracker used by Android.</summary>
        public InputTouchTracker Touches { get; } = new InputTouchTracker();
        /// <summary>Reports touch projection so the pointer router cannot deliver duplicate cursor clicks.</summary>
        public bool IsTouchPointer { get; set; } = true;
#if DESKTOP_PLATFORM
        /// <summary>Allows tests to publish a simultaneous legacy pointer press.</summary>
        public MouseState Mouse { get; set; }
        /// <summary>Fulfills the desktop backend policy contract without altering synthetic contacts.</summary>
        public bool ReceiveInputInBackground { get; set; }
#endif
        /// <summary>Returns borrowed alternating contact buffers, retaining all events between captures.</summary>
        public InputFrameState CaptureFrame() {
            InputFrameState frame = Touches.CaptureFrame();
            frame.Pointer = new InputPointerState { IsTouch = IsTouchPointer, Connected = true };
#if DESKTOP_PLATFORM
            frame.Mouse = Mouse;
#endif
            return frame;
        }
        /// <summary>Releases tracker storage after the core has disposed its interaction router.</summary>
        public void Dispose() { Touches.Dispose(); }
    }
}
