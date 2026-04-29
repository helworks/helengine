namespace helengine {
    /// <summary>
    /// Represents a logged message with severity and timestamp.
    /// </summary>
    public readonly struct LogEntry {
        /// <summary>
        /// Initializes a new log entry.
        /// </summary>
        /// <param name="level">Severity level for the message.</param>
        /// <param name="message">Message text.</param>
        /// <param name="timestamp">Timestamp associated with the entry.</param>
        public LogEntry(LogLevel level, string message, DateTime timestamp) {
            Level = level;
            Message = message ?? string.Empty;
            Timestamp = timestamp;
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
        /// Gets the timestamp for the entry.
        /// </summary>
        public DateTime Timestamp { get; }
    }
}
