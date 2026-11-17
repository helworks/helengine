using helengine;
namespace helengine.core.tests.components {
    /// <summary>Explicit host seam for delivering complete native snapshots to the real control in headless lifecycle tests.</summary>
    public sealed class NativeTextEntryTestSession : ITextEntrySession {
        /// <summary>Gets the test snapshot revision.</summary>
        public int Revision { get; private set; }
        /// <summary>Gets complete text delivered at the host boundary.</summary>
        public string Text { get; private set; } = "";
        /// <summary>Gets the selected range start.</summary>
        public int SelectionStart { get; private set; }
        /// <summary>Gets the selected code-unit count.</summary>
        public int SelectionLength { get; private set; }
        /// <summary>Gets the active composition start or minus one.</summary>
        public int MarkedStart { get; private set; } = -1;
        /// <summary>Gets the active composition length.</summary>
        public int MarkedLength { get; private set; }
        /// <summary>Gets whether this owned session remains active.</summary>
        public bool IsActive { get; private set; } = true;
        /// <summary>Gets or sets whether the native Return action has occurred.</summary>
        public bool Submitted { get; set; }
        /// <summary>Gets whether cancellation ended the session.</summary>
        public bool Canceled { get; private set; }
        /// <summary>Gets whether the owning control released this session.</summary>
        public bool Disposed { get; private set; }
        /// <summary>Gets how many complete external replacements occurred, allowing equal assignments to preserve IME state.</summary>
        public int ReplacementCount { get; private set; }
        /// <summary>Gets or sets an explicit host rejection used to verify atomic external edits in the real control.</summary>
        public bool RejectReplacement { get; set; }
        /// <summary>Publishes a coherent host snapshot without running an engine callback.</summary>
        public void Publish(string text, int start, int length, int markedStart, int markedLength) {
            Text = text; SelectionStart = start; SelectionLength = length; MarkedStart = markedStart; MarkedLength = markedLength; Revision++;
        }
        /// <summary>Models nonblocking polling; queued publication already supplies coherent facts.</summary>
        public void Poll() { }
        /// <summary>Models explicit external replacement, ending old composition.</summary>
        public void Replace(string text, int start, int length) { if (RejectReplacement) { throw new InvalidOperationException("Host rejected complete replacement."); } ReplacementCount++; Publish(text, start, length, -1, 0); }
        /// <summary>Models native selection without changing marked text.</summary>
        public void Select(int start, int length) { SelectionStart = start; SelectionLength = length; Revision++; }
        /// <summary>Ends this session without reaching another session owned by the host.</summary>
        public void Cancel() { IsActive = false; Canceled = true; Revision++; }
        /// <summary>Records deterministic release so tests detect dangling control ownership.</summary>
        public void Dispose() { Cancel(); Disposed = true; }
    }
}
