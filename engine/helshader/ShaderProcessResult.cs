namespace helshader {
    /// <summary>
    /// Stores process execution results for external tools.
    /// </summary>
    public class ShaderProcessResult {
        /// <summary>
        /// Initializes a new process result.
        /// </summary>
        /// <param name="exitCode">Process exit code.</param>
        /// <param name="output">Standard output text.</param>
        /// <param name="errorOutput">Standard error output text.</param>
        public ShaderProcessResult(int exitCode, string output, string errorOutput) {
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
