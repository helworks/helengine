namespace helengine {
    /// <summary>
    /// Provides simple engine logging helpers for informational, warning, and error messages.
    /// </summary>
    public static class Logger {
        /// <summary>
        /// Raised whenever a log message is written.
        /// </summary>
        public static event Action<LogEntry> MessageLogged;

        /// <summary>
        /// Raised when a warning message is written.
        /// </summary>
        public static event Action<LogEntry> WarningLogged;

        /// <summary>
        /// Raised when an error message is written.
        /// </summary>
        public static event Action<LogEntry> ErrorLogged;

        /// <summary>
        /// Writes an informational message to the debug output stream.
        /// </summary>
        /// <param name="message">Message to write.</param>
        public static void WriteLine(string message) {
            Write(LogLevel.Info, message);
        }

        /// <summary>
        /// Writes a warning message to the debug output stream.
        /// </summary>
        /// <param name="message">Warning message to write.</param>
        public static void WriteWarning(string message) {
            Write(LogLevel.Warning, message);
        }

        /// <summary>
        /// Writes an error message to the debug output stream.
        /// </summary>
        /// <param name="message">Error message to write.</param>
        public static void WriteError(string message) {
            Write(LogLevel.Error, message);
        }

        /// <summary>
        /// Writes a message with a specific severity level and raises log events.
        /// </summary>
        /// <param name="level">Severity level for the message.</param>
        /// <param name="message">Message to write.</param>
        static void Write(LogLevel level, string message) {
            string text = message ?? string.Empty;
            var entry = new LogEntry(level, text, DateTime.UtcNow);

            string output = text;
            if (level == LogLevel.Warning) {
                output = $"Warning: {text}";
            } else if (level == LogLevel.Error) {
                output = $"Error: {text}";
            }

            System.Diagnostics.Debug.WriteLine(output);

            MessageLogged?.Invoke(entry);
            if (level == LogLevel.Warning) {
                WarningLogged?.Invoke(entry);
            } else if (level == LogLevel.Error) {
                ErrorLogged?.Invoke(entry);
            }
        }
    }
}
