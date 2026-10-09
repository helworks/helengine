namespace helengine.timeline {
    /// <summary>
    /// Raised when a timeline definition cannot be read or fails validation. It carries every located diagnostic so callers
    /// (and planner models) can fix all problems in one pass.
    /// </summary>
    public sealed class TimelineFormatException : FormatException {
        /// <summary>
        /// Creates the exception from the diagnostics that rejected the definition.
        /// </summary>
        /// <param name="diagnostics">At least one located diagnostic.</param>
        public TimelineFormatException(IReadOnlyList<TimelineDiagnostic> diagnostics)
            : base(BuildMessage(diagnostics)) {
            Diagnostics = diagnostics;
        }

        /// <summary>
        /// Gets every diagnostic that rejected the definition, in document order.
        /// </summary>
        public IReadOnlyList<TimelineDiagnostic> Diagnostics { get; }

        /// <summary>
        /// Joins the diagnostics into one message, one located problem per line.
        /// </summary>
        /// <param name="diagnostics">Diagnostics to join.</param>
        /// <returns>The exception message.</returns>
        static string BuildMessage(IReadOnlyList<TimelineDiagnostic> diagnostics) {
            if (diagnostics == null || diagnostics.Count == 0) {
                throw new ArgumentException("A timeline format error needs at least one diagnostic.", nameof(diagnostics));
            }
            List<string> lines = new List<string>(diagnostics.Count + 1);
            lines.Add("The timeline is invalid:");
            for (int index = 0; index < diagnostics.Count; index++) {
                lines.Add(diagnostics[index].ToString());
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
