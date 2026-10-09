namespace helengine.timeline {
    /// <summary>
    /// One problem found in a timeline definition, located by a JSON-path-like address such as
    /// <c>tracks[2].clips[0].keyframes[1].time</c>, so a person or a planner model can correct exactly that field.
    /// </summary>
    public sealed class TimelineDiagnostic {
        /// <summary>
        /// Creates one diagnostic.
        /// </summary>
        /// <param name="path">Location of the offending field in the JSON form; empty for the document root.</param>
        /// <param name="message">Readable explanation of what is wrong and what is allowed.</param>
        public TimelineDiagnostic(string path, string message) {
            if (path == null) {
                throw new ArgumentNullException(nameof(path));
            }
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("A diagnostic needs a message.", nameof(message));
            }
            Path = path;
            Message = message;
        }

        /// <summary>
        /// Gets the location of the offending field in the JSON form, using snake_case names and zero-based indices.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the readable explanation of the problem.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Formats the diagnostic as <c>path: message</c>.
        /// </summary>
        /// <returns>The located message.</returns>
        public override string ToString() {
            if (Path.Length == 0) {
                return Message;
            }
            return Path + ": " + Message;
        }
    }
}
