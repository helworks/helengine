using System.Drawing;

namespace helengine.render.validation {
    /// <summary>
    /// Captures the outcome of validating a single renderer backend.
    /// </summary>
    public class RenderValidationResult {
        /// <summary>
        /// Initializes a new validation result.
        /// </summary>
        /// <param name="backend">Backend that was validated.</param>
        /// <param name="outputPath">Captured image output path.</param>
        /// <param name="centerPixel">Center pixel sampled from the captured image.</param>
        /// <param name="passed">True when the sampled pixel matched expectations.</param>
        /// <param name="message">Human-readable status message.</param>
        public RenderValidationResult(
            RenderBackend backend,
            string outputPath,
            Color centerPixel,
            bool passed,
            string message) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message must be provided.", nameof(message));
            }

            Backend = backend;
            OutputPath = outputPath;
            CenterPixel = centerPixel;
            Passed = passed;
            Message = message;
        }

        /// <summary>
        /// Gets the backend that was validated.
        /// </summary>
        public RenderBackend Backend { get; }

        /// <summary>
        /// Gets the output image path.
        /// </summary>
        public string OutputPath { get; }

        /// <summary>
        /// Gets the center pixel sampled from the output image.
        /// </summary>
        public Color CenterPixel { get; }

        /// <summary>
        /// Gets a value indicating whether validation passed.
        /// </summary>
        public bool Passed { get; }

        /// <summary>
        /// Gets a status message describing the validation outcome.
        /// </summary>
        public string Message { get; }
    }
}
