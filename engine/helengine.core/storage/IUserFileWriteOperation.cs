namespace helengine {
    /// <summary>Caller-owned transactional write. Advance polls on the initiating thread; no generated object is touched by the worker.</summary>
    public interface IUserFileWriteOperation : IDisposable {
        /// <summary>Gets the last polled state. Pending cancellation remains Pending until the worker determines whether commit occurred.</summary>
        UserFileOperationStatus Status { get; }
        /// <summary>Gets the portable error, including Conflict, InvalidPayload and Canceled. A postcommit durability error can coexist with Committed.</summary>
        UserFileError Error { get; }
        /// <summary>Gets native diagnostic details after terminal completion.</summary>
        string ErrorMessage { get; }
        /// <summary>Gets whether complete publication occurred. Once true, cancellation and disposal never conceal that result; callers must inspect it even when Error is nonzero.</summary>
        bool Committed { get; }
        /// <summary>Gets lowercase SHA256 of the complete published bytes when Committed, otherwise the empty string. It is the next descriptor compare-and-swap revision.</summary>
        string Revision { get; }
        /// <summary>Polls native completion without filesystem access or waiting, preserving any committed terminal result.</summary>
        void Advance();
        /// <summary>Requests cancellation before commit. Advance must still resolve pending work; a racing commit is reported as committed.</summary>
        void Cancel();
    }
}
