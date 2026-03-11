namespace helengine.editor.iconbuilder {
    /// <summary>
    /// Entry point for the toolbar icon builder tool.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Default output directory used when no explicit path is supplied.
        /// </summary>
        static readonly string DefaultOutputDirectory =
            Path.Combine("helengine.ui", "helengine.editor.app", "content", "icons", "toolbar");

        /// <summary>
        /// Executes the icon builder.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Zero when generation succeeded; otherwise one.</returns>
        public static int Main(string[] args) {
            try {
                ToolbarIconBuilder builder = new ToolbarIconBuilder();
                string outputDirectory = GetOutputDirectory(args);
                int iconSize = GetIconSize(args);
                builder.BuildAll(outputDirectory, iconSize);
                return 0;
            } catch (Exception exception) {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        /// <summary>
        /// Resolves the output directory from command-line arguments.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Absolute or relative output directory path.</returns>
        static string GetOutputDirectory(string[] args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            for (int argumentIndex = 0; argumentIndex < args.Length; argumentIndex++) {
                string argument = args[argumentIndex];
                if (!string.Equals(argument, "--output-dir", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (argumentIndex + 1 >= args.Length) {
                    throw new InvalidOperationException("Missing value for --output-dir.");
                }

                return args[argumentIndex + 1];
            }

            return DefaultOutputDirectory;
        }

        /// <summary>
        /// Resolves the icon size from command-line arguments.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Requested icon size in pixels.</returns>
        static int GetIconSize(string[] args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            for (int argumentIndex = 0; argumentIndex < args.Length; argumentIndex++) {
                string argument = args[argumentIndex];
                if (!string.Equals(argument, "--size", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (argumentIndex + 1 >= args.Length) {
                    throw new InvalidOperationException("Missing value for --size.");
                }

                if (!int.TryParse(args[argumentIndex + 1], out int iconSize)) {
                    throw new InvalidOperationException("Icon size must be an integer.");
                }

                if (iconSize < 16) {
                    throw new InvalidOperationException("Icon size must be at least 16 pixels.");
                }

                return iconSize;
            }

            return 64;
        }
    }
}
