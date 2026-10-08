namespace helengine.video {
    /// <summary>
    /// How serious a diagnostic is for validation and rendering.
    /// </summary>
    public enum VideoDiagnosticSeverity {
        /// <summary>
        /// The document is invalid and cannot be compiled.
        /// </summary>
        Error = 0,

        /// <summary>
        /// The document compiles for previews but something must be resolved before a final render.
        /// </summary>
        Pending = 1,

        /// <summary>
        /// The compiler adjusted a value; rendering may proceed.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Information about how the result was produced, such as estimated text measurement; never blocks rendering.
        /// </summary>
        Info = 3
    }
}
