namespace helshader {
    /// <summary>
    /// Parses command line arguments for the shader tool.
    /// </summary>
    public class ShaderCommandLineParser {
        /// <summary>
        /// Parses the provided argument list into a command options instance.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Parsed command options.</returns>
        public ShaderCommandOptions Parse(string[] args) {
            ShaderCommandOptions options = new ShaderCommandOptions();
            if (args == null || args.Length == 0) {
                options.Command = ShaderCommandType.None;
                return options;
            }

            options.Command = ParseCommand(args[0]);
            int index = 1;
            while (index < args.Length) {
                string arg = args[index];
                if (string.Equals(arg, "--manifest", StringComparison.OrdinalIgnoreCase)) {
                    options.ManifestPath = ReadValue(args, ref index, "--manifest");
                } else if (string.Equals(arg, "--shader", StringComparison.OrdinalIgnoreCase)) {
                    options.ShaderName = ReadValue(args, ref index, "--shader");
                } else if (string.Equals(arg, "--file", StringComparison.OrdinalIgnoreCase)) {
                    options.ShaderFile = ReadValue(args, ref index, "--file");
                } else if (string.Equals(arg, "--target", StringComparison.OrdinalIgnoreCase)) {
                    options.Target = ReadValue(args, ref index, "--target");
                } else if (string.Equals(arg, "--variant", StringComparison.OrdinalIgnoreCase)) {
                    options.Variant = ReadValue(args, ref index, "--variant");
                } else if (string.Equals(arg, "--define", StringComparison.OrdinalIgnoreCase)) {
                    string define = ReadValue(args, ref index, "--define");
                    options.Defines.Add(define);
                } else if (string.Equals(arg, "--all-targets", StringComparison.OrdinalIgnoreCase)) {
                    options.AllTargets = true;
                    index++;
                } else if (string.Equals(arg, "--emit-modules", StringComparison.OrdinalIgnoreCase)) {
                    options.EmitModules = true;
                    index++;
                } else if (string.Equals(arg, "--clean", StringComparison.OrdinalIgnoreCase)) {
                    options.Clean = true;
                    index++;
                } else if (string.Equals(arg, "--verbose", StringComparison.OrdinalIgnoreCase)) {
                    options.Verbose = true;
                    index++;
                } else {
                    throw new InvalidOperationException($"Unknown argument '{arg}'.");
                }
            }

            return options;
        }

        /// <summary>
        /// Parses the command name into a command type.
        /// </summary>
        /// <param name="command">Command name string.</param>
        /// <returns>Parsed command type.</returns>
        ShaderCommandType ParseCommand(string command) {
            if (string.IsNullOrWhiteSpace(command)) {
                return ShaderCommandType.None;
            }

            if (string.Equals(command, "build", StringComparison.OrdinalIgnoreCase)) {
                return ShaderCommandType.Build;
            }

            if (string.Equals(command, "codegen", StringComparison.OrdinalIgnoreCase)) {
                return ShaderCommandType.Codegen;
            }

            if (string.Equals(command, "validate", StringComparison.OrdinalIgnoreCase)) {
                return ShaderCommandType.Validate;
            }

            return ShaderCommandType.None;
        }

        /// <summary>
        /// Reads the value that follows a flag argument.
        /// </summary>
        /// <param name="args">Argument list.</param>
        /// <param name="index">Current index pointer.</param>
        /// <param name="name">Argument name for error messages.</param>
        /// <returns>Argument value.</returns>
        string ReadValue(string[] args, ref int index, string name) {
            if (index + 1 >= args.Length) {
                throw new InvalidOperationException($"Argument '{name}' expects a value.");
            }

            index++;
            string value = args[index];
            index++;
            return value;
        }
    }
}
