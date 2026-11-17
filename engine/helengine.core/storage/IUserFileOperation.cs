namespace helengine {
    /// <summary>Caller-owned request. Call Advance on the initiating thread; borrowed results remain valid until cancellation or disposal.</summary>
    public interface IUserFileOperation : IDisposable {
        /// <summary>Gets the last polled state; every state other than Pending is terminal.</summary>
        UserFileOperationStatus Status { get; }
        /// <summary>Gets the portable failure code; successful and pending requests have None.</summary>
        UserFileError Error { get; }
        /// <summary>Gets native diagnostic details for failed requests without masking the portable error.</summary>
        string ErrorMessage { get; }
        /// <summary>Gets the complete listing count after success; failed listings expose no partial entries.</summary>
        int EntryCount { get; }
        /// <summary>Returns a borrowed metadata entry after listing success; invalid indexes fail.</summary>
        [NativeBorrowedReturn]
        UserFileEntry GetEntry(int index);
        /// <summary>Gets borrowed binary data after read success; valid only while this request remains alive.</summary>
        [NativeBorrowedReturn]
        byte[] Bytes { get; }
        /// <summary>Polls native completion without filesystem access or waiting; materializes generated results on the initiating thread.</summary>
        void Advance();
        /// <summary>Cancels pending or completed work and invalidates borrowed results; completion cannot restore canceled state.</summary>
        void Cancel();
    }
}
