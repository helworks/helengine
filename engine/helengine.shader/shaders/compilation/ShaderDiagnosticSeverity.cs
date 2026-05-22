namespace helengine {
    /// <summary>
    /// Represents the severity of a shader compiler diagnostic.
    /// </summary>
    public enum ShaderDiagnosticSeverity {
        /// <summary>
        /// Informational diagnostic that does not indicate a failure.
        /// </summary>
        Info,

        /// <summary>
        /// Warning diagnostic that may indicate a portability issue.
        /// </summary>
        Warning,

        /// <summary>
        /// Error diagnostic that causes shader compilation to fail.
        /// </summary>
        Error
    }
}
