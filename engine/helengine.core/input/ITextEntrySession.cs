namespace helengine {
    /// <summary>Caller-owned native input session. Poll on the UI thread, then consume the coherent snapshot only when Revision changes.</summary>
    public interface ITextEntrySession : IDisposable {
        /// <summary>Gets the monotonic snapshot revision, including selection, composition and terminal changes.</summary>
        int Revision { get; }
        /// <summary>Gets the complete UTF-16 text, bounded by the maximum accepted at Begin; no partial payload is returned.</summary>
        string Text { get; }
        /// <summary>Gets the first UTF-16 code unit in the selected range.</summary>
        int SelectionStart { get; }
        /// <summary>Gets the selected UTF-16 code-unit count; zero represents a caret.</summary>
        int SelectionLength { get; }
        /// <summary>Gets the first marked UTF-16 code unit, or minus one when no native composition remains.</summary>
        int MarkedStart { get; }
        /// <summary>Gets the marked UTF-16 code-unit count without committing the composition.</summary>
        int MarkedLength { get; }
        /// <summary>Gets whether this session still owns the host's active native responder.</summary>
        bool IsActive { get; }
        /// <summary>Gets whether Return committed this single-line session.</summary>
        bool Submitted { get; }
        /// <summary>Gets whether cancellation, focus replacement or interruption ended this session.</summary>
        bool Canceled { get; }
        /// <summary>Refreshes one coherent snapshot without blocking; no callbacks enter the portable control.</summary>
        void Poll();
        /// <summary>Replaces complete text and selection after an external edit, explicitly ending prior marked composition.</summary>
        void Replace(string text, int selectionStart, int selectionLength);
        /// <summary>Changes selection without replacing or unmarking the current native composition.</summary>
        void Select(int selectionStart, int selectionLength);
        /// <summary>Ends this session without submit; a canceled old session cannot affect a newer responder.</summary>
        void Cancel();
    }
}
