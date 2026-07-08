namespace helengine {
    /// <summary>
    /// Represents a logged message with severity and a monotonic process-relative timestamp.
    /// </summary>
    public readonly struct LogEntry {
        /// <summary>
        /// Initializes a new log entry.
        /// </summary>
        /// <param name="level">Severity level for the message.</param>
        /// <param name="message">Message text.</param>
        /// <param name="timestampSeconds">Seconds elapsed on the logger monotonic clock when the entry was written.</param>
        public LogEntry(LogLevel level, string message, double timestampSeconds) {
            Level = level;
            Message = message ?? string.Empty;
            TimestampSeconds = timestampSeconds;
        }

        /// <summary>
        /// Gets the severity level for the entry.
        /// </summary>
        public LogLevel Level { get; }

        /// <summary>
        /// Gets the message text.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the monotonic logger timestamp for the entry in seconds since the logger clock started.
        /// </summary>
        public double TimestampSeconds { get; }
    }
}
