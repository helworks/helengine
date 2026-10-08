namespace helengine.video {
    /// <summary>
    /// One finding about an edit document: a validation error, a pending issue blocking a final render, or a warning.
    /// Validation and compilation report through this single shape.
    /// </summary>
    public sealed class VideoDiagnostic {
        /// <summary>
        /// Gets or sets the stable machine-readable code, e.g. <c>anchor_unresolved</c>.
        /// </summary>
        public string Code { get; set; } = "";

        /// <summary>
        /// Gets or sets the scene id the finding belongs to, or null for document-level findings.
        /// </summary>
        public string Scene { get; set; }

        /// <summary>
        /// Gets or sets the JSON path of the offending value, e.g. <c>scenes[1].entry.duration_sec</c>.
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// Gets or sets the human-readable explanation.
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// Gets or sets how serious the finding is.
        /// </summary>
        public VideoDiagnosticSeverity Severity { get; set; }

        /// <summary>
        /// Creates one diagnostic.
        /// </summary>
        /// <param name="severity">Severity.</param>
        /// <param name="code">Machine-readable code.</param>
        /// <param name="scene">Scene id or null.</param>
        /// <param name="path">JSON path.</param>
        /// <param name="message">Explanation.</param>
        /// <returns>New diagnostic.</returns>
        public static VideoDiagnostic Create(VideoDiagnosticSeverity severity, string code, string scene, string path, string message) {
            return new VideoDiagnostic { Severity = severity, Code = code, Scene = scene, Path = path, Message = message };
        }
    }
}
