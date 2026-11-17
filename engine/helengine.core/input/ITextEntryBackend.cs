namespace helengine {
    /// <summary>Optional host-owned native input service; all methods run on the host UI thread and only one owned session is active.</summary>
    public interface ITextEntryBackend {
        /// <summary>Begins single-line native composition for complete bounded UTF-16 text, canceling the previous session; ownership transfers to the caller.</summary>
        [NativeOwnedReturn]
        ITextEntrySession Begin(string text, int selectionStart, int selectionLength, int maximumLength);
        /// <summary>Cancels current input on application interruption or core shutdown without submitting its text.</summary>
        void CancelActive();
    }
}
