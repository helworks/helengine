namespace helengine.editor {
    /// <summary>
    /// Represents the result of invoking the shader tool process.
    /// </summary>
    public class ShaderToolResult {
        /// <summary>
        /// Initializes a new shader tool result container.
        /// </summary>
        /// <param name="exitCode">Process exit code.</param>
        /// <param name="output">Captured standard output.</param>
        /// <param name="errorOutput">Captured standard error output.</param>
        public ShaderToolResult(int exitCode, string output, string errorOutput) {
            if (output == null) {
                throw new ArgumentNullException(nameof(output));
            }

            if (errorOutput == null) {
                throw new ArgumentNullException(nameof(errorOutput));
            }

            ExitCode = exitCode;
            Output = output;
            ErrorOutput = errorOutput;
        }

        /// <summary>
        /// Gets the process exit code.
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// Gets the standard output text.
        /// </summary>
        public string Output { get; }

        /// <summary>
        /// Gets the standard error output text.
        /// </summary>
        public string ErrorOutput { get; }
    }
}
