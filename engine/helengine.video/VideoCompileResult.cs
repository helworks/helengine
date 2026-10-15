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
        /// Gets every compiled overlay with its absolute interval (reading time included) and screen box, in scene order,
        /// so hosts can check what is on screen together; empty when validation failed.
        /// </summary>
        public List<VideoOverlayPlacement> Overlays { get; } = [];

        /// <summary>
        /// Gets where the speaker's head is on screen, by scene id, for every scene whose take media has head boxes (after
        /// the face framing), so hosts can keep their own text off it; empty when validation failed.
        /// </summary>
        public Dictionary<string, List<VideoHeadSample>> Heads { get; } = new Dictionary<string, List<VideoHeadSample>>(StringComparer.Ordinal);

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
                return Diagnostics.Any(diagnostic => diagnostic.Severity is VideoDiagnosticSeverity.Error or VideoDiagnosticSeverity.Pending);
            }
        }
    }
}
