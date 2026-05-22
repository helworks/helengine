namespace helengine {
    /// <summary>
    /// Captures a diagnostic message emitted during shader compilation.
    /// </summary>
    public class ShaderCompileDiagnostic {
        /// <summary>
        /// Initializes a new diagnostic instance.
        /// </summary>
        /// <param name="severity">Severity classification for the diagnostic.</param>
        /// <param name="message">Compiler message text.</param>
        /// <param name="filePath">Source file path for the diagnostic; use an empty string when unavailable.</param>
        /// <param name="line">1-based line number for the diagnostic location, or 0 when unavailable.</param>
        /// <param name="column">1-based column number for the diagnostic location, or 0 when unavailable.</param>
        public ShaderCompileDiagnostic(
            ShaderDiagnosticSeverity severity,
            string message,
            string filePath,
            int line,
            int column) {
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Diagnostic message must be provided.", nameof(message));
            }

            if (filePath == null) {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (line < 0) {
                throw new ArgumentOutOfRangeException(nameof(line), "Line cannot be negative.");
            }

            if (column < 0) {
                throw new ArgumentOutOfRangeException(nameof(column), "Column cannot be negative.");
            }

            Severity = severity;
            Message = message;
            FilePath = filePath;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Gets the severity classification.
        /// </summary>
        public ShaderDiagnosticSeverity Severity { get; }

        /// <summary>
        /// Gets the compiler message text.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the source file path associated with the diagnostic.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the 1-based line number for the diagnostic location, or 0 when unavailable.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Gets the 1-based column number for the diagnostic location, or 0 when unavailable.
        /// </summary>
        public int Column { get; }
    }
}
