namespace helengine.painting {
    /// <summary>Nonblocking state of a submitted GPU operation or immutable snapshot.</summary>
    public enum PaintSubmissionStatus {
        /// <summary>Work is accepted but its resources are not yet ready for consumers.</summary>
        Pending,
        /// <summary>All work for this handle completed successfully.</summary>
        Completed,
        /// <summary>The operation failed and the backend exposes a diagnostic.</summary>
        Failed
    }
}
