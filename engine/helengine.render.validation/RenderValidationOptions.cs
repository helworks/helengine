namespace helengine.render.validation {
    /// <summary>
    /// Stores command-line options for a render validation run.
    /// </summary>
    public class RenderValidationOptions {
        /// <summary>
        /// Initializes a new options instance.
        /// </summary>
        /// <param name="backendSelection">Backend selection for the run.</param>
        /// <param name="outputDirectory">Directory where captured images are written.</param>
        /// <param name="frameWidth">Render width in pixels.</param>
        /// <param name="frameHeight">Render height in pixels.</param>
        /// <param name="frameCount">Number of frames to render before capture.</param>
        public RenderValidationOptions(
            RenderBackendSelection backendSelection,
            string outputDirectory,
            int frameWidth,
            int frameHeight,
            int frameCount) {
            if (string.IsNullOrWhiteSpace(outputDirectory)) {
                throw new ArgumentException("Output directory must be provided.", nameof(outputDirectory));
            }

            if (frameWidth <= 0) {
                throw new ArgumentOutOfRangeException(nameof(frameWidth), "Frame width must be greater than zero.");
            }

            if (frameHeight <= 0) {
                throw new ArgumentOutOfRangeException(nameof(frameHeight), "Frame height must be greater than zero.");
            }

            if (frameCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count must be greater than zero.");
            }

            BackendSelection = backendSelection;
            OutputDirectory = Path.GetFullPath(outputDirectory);
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            FrameCount = frameCount;
        }

        /// <summary>
        /// Gets the selected backend mode.
        /// </summary>
        public RenderBackendSelection BackendSelection { get; }

        /// <summary>
        /// Gets the output directory where images are stored.
        /// </summary>
        public string OutputDirectory { get; }

        /// <summary>
        /// Gets the render width in pixels.
        /// </summary>
        public int FrameWidth { get; }

        /// <summary>
        /// Gets the render height in pixels.
        /// </summary>
        public int FrameHeight { get; }

        /// <summary>
        /// Gets the number of frames rendered before capture.
        /// </summary>
        public int FrameCount { get; }

        /// <summary>
        /// Parses command-line options for the validation runner.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Parsed options instance.</returns>
        public static RenderValidationOptions Parse(string[] args) {
            RenderBackendSelection backendSelection = RenderBackendSelection.Both;
            string outputDirectory = Path.Combine(Environment.CurrentDirectory, "render-validation-output");
            int frameWidth = 512;
            int frameHeight = 512;
            int frameCount = 10;

            int index = 0;
            while (index < args.Length) {
                string argument = args[index];
                if (string.Equals(argument, "--backend", StringComparison.OrdinalIgnoreCase)) {
                    index++;
                    if (index >= args.Length) {
                        throw new InvalidOperationException("Missing value for --backend.");
                    }

                    backendSelection = ParseBackendSelection(args[index]);
                } else if (string.Equals(argument, "--output", StringComparison.OrdinalIgnoreCase)) {
                    index++;
                    if (index >= args.Length) {
                        throw new InvalidOperationException("Missing value for --output.");
                    }

                    outputDirectory = args[index];
                } else if (string.Equals(argument, "--width", StringComparison.OrdinalIgnoreCase)) {
                    index++;
                    if (index >= args.Length) {
                        throw new InvalidOperationException("Missing value for --width.");
                    }

                    frameWidth = ParsePositiveInt(args[index], "--width");
                } else if (string.Equals(argument, "--height", StringComparison.OrdinalIgnoreCase)) {
                    index++;
                    if (index >= args.Length) {
                        throw new InvalidOperationException("Missing value for --height.");
                    }

                    frameHeight = ParsePositiveInt(args[index], "--height");
                } else if (string.Equals(argument, "--frames", StringComparison.OrdinalIgnoreCase)) {
                    index++;
                    if (index >= args.Length) {
                        throw new InvalidOperationException("Missing value for --frames.");
                    }

                    frameCount = ParsePositiveInt(args[index], "--frames");
                } else {
                    throw new InvalidOperationException($"Unknown argument '{argument}'.");
                }

                index++;
            }

            return new RenderValidationOptions(backendSelection, outputDirectory, frameWidth, frameHeight, frameCount);
        }

        /// <summary>
        /// Resolves concrete backend runs from the selected mode.
        /// </summary>
        /// <returns>Array of concrete backends to execute.</returns>
        public RenderBackend[] ResolveBackends() {
            switch (BackendSelection) {
                case RenderBackendSelection.DirectX11:
                    return new[] { RenderBackend.DirectX11 };
                case RenderBackendSelection.Vulkan:
                    return new[] { RenderBackend.Vulkan };
                case RenderBackendSelection.Both:
                    return new[] { RenderBackend.DirectX11, RenderBackend.Vulkan };
                default:
                    throw new InvalidOperationException("Unsupported backend selection.");
            }
        }

        /// <summary>
        /// Parses backend selection from a command-line token.
        /// </summary>
        /// <param name="value">Backend token.</param>
        /// <returns>Parsed backend selection value.</returns>
        static RenderBackendSelection ParseBackendSelection(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException("Backend value must be provided.");
            }

            if (string.Equals(value, "dx11", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "directx11", StringComparison.OrdinalIgnoreCase)) {
                return RenderBackendSelection.DirectX11;
            }

            if (string.Equals(value, "vulkan", StringComparison.OrdinalIgnoreCase)) {
                return RenderBackendSelection.Vulkan;
            }

            if (string.Equals(value, "both", StringComparison.OrdinalIgnoreCase)) {
                return RenderBackendSelection.Both;
            }

            throw new InvalidOperationException("Unsupported backend value. Use dx11, vulkan, or both.");
        }

        /// <summary>
        /// Parses a strictly positive integer value from a command-line token.
        /// </summary>
        /// <param name="value">Token to parse.</param>
        /// <param name="argumentName">Argument name used for error context.</param>
        /// <returns>Parsed integer value.</returns>
        static int ParsePositiveInt(string value, string argumentName) {
            if (!int.TryParse(value, out int parsed) || parsed <= 0) {
                throw new InvalidOperationException($"Argument {argumentName} must be a positive integer.");
            }

            return parsed;
        }
    }
}
