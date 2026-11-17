namespace helengine {
    /// <summary>Portable lifecycle of an owned asynchronous user document request.</summary>
    public enum UserFileOperationStatus {
        /// <summary>The worker has not published its result to the caller.</summary>
        Pending,
        /// <summary>The entire bounded result is available.</summary>
        Succeeded,
        /// <summary>The request failed and Error explains why.</summary>
        Failed,
        /// <summary>The caller canceled or disposed this request.</summary>
        Canceled
    }
}
