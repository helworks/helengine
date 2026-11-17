using helengine;
namespace helengine.core.tests.components {
    /// <summary>Native input boundary that transfers a distinct owned session for each focus acquisition.</summary>
    public sealed class NativeTextEntryTestBackend : ITextEntryBackend {
        /// <summary>Gets the latest session solely for publishing host events in tests.</summary>
        public NativeTextEntryTestSession Session { get; private set; }
        /// <summary>Transfers a fresh session and invalidates the previous host responder.</summary>
        public ITextEntrySession Begin(string text, int selectionStart, int selectionLength, int maximumLength) {
            CancelActive(); Session = new NativeTextEntryTestSession(); Session.Publish(text, selectionStart, selectionLength, -1, 0); return Session;
        }
        /// <summary>Cancels active platform input when the core is interrupted or disposed.</summary>
        public void CancelActive() { if (Session != null && Session.IsActive) { Session.Cancel(); } }
    }
}
