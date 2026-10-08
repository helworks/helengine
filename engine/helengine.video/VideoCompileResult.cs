using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Output of compiling an edit: the executable composition (absent when the edit has errors) and every diagnostic.
    /// </summary>
    public sealed class VideoCompileResult {
        /// <summary>
        /// Gets or sets the composition, or null when the edit has errors.
        /// </summary>
        public CompositionDocument Composition { get; set; }

        /// <summary>
        /// Gets or sets every finding of validation and compilation.
        /// </summary>
        public List<VideoDiagnostic> Diagnostics { get; set; } = [];

        /// <summary>
        /// Gets whether the edit could not be compiled.
        /// </summary>
        public bool HasErrors {
            get {
                return Diagnostics.Any(diagnostic => diagnostic.Severity == VideoDiagnosticSeverity.Error);
            }
        }

        /// <summary>
        /// Gets whether a final render must be refused (errors or pending issues).
        /// </summary>
        public bool BlocksFinal {
            get {
                return Diagnostics.Any(diagnostic => diagnostic.Severity != VideoDiagnosticSeverity.Warning);
            }
        }
    }
}
