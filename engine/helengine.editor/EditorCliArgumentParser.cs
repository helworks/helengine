namespace helengine.editor {
    /// <summary>
    /// Parses the editor application's command-line arguments.
    /// </summary>
    public static class EditorCliArgumentParser {
        /// <summary>
        /// Returns true when the supplied argument list requests headless build mode.
        /// </summary>
        /// <param name="args">Command-line arguments supplied by the shell.</param>
        /// <returns>True when build mode was requested.</returns>
        public static bool IsBuildModeRequested(string[] args) {
            if (args == null || args.Length == 0) {
                return false;
            }

            for (int index = 0; index < args.Length; index++) {
                if (IsSwitchMatch(args[index], "--build")) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true when the supplied argument list requests headless editor-command mode.
        /// </summary>
        /// <param name="args">Command-line arguments supplied by the shell.</param>
        /// <returns>True when editor-command mode was requested.</returns>
        public static bool IsEditorCommandModeRequested(string[] args) {
            if (args == null || args.Length == 0) {
                return false;
            }

            for (int index = 0; index < args.Length; index++) {
                if (IsSwitchMatch(args[index], "--editor-command")) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to parse one headless build invocation from the supplied arguments.
        /// </summary>
        /// <param name="args">Command-line arguments supplied by the shell.</param>
        /// <param name="options">Parsed build options when parsing succeeds.</param>
        /// <param name="errorMessage">Human-readable parse error when parsing fails.</param>
        /// <returns>True when the arguments form one valid headless build invocation.</returns>
        public static bool TryParseBuildOptions(string[] args, out EditorCliBuildOptions options, out string errorMessage) {
            options = null;
            errorMessage = string.Empty;

            if (args == null || args.Length == 0) {
                errorMessage = "Build mode requires `--project`, `--build`, and `--output` arguments.";
                return false;
            }

            string projectPath = string.Empty;
            string platformId = string.Empty;
            string outputDirectoryPath = string.Empty;
            bool useCommonOutputDirectory = false;

            for (int index = 0; index < args.Length; index++) {
                string argument = args[index];
                if (string.IsNullOrWhiteSpace(argument)) {
                    continue;
                }

                if (TryReadInlineValue(argument, "--project", out string inlineProjectPath)) {
                    projectPath = inlineProjectPath;
                    continue;
                }

                if (TryReadInlineValue(argument, "--build", out string inlinePlatformId)) {
                    platformId = inlinePlatformId;
                    continue;
                }

                if (TryReadInlineValue(argument, "--output", out string inlineOutputDirectoryPath)) {
                    outputDirectoryPath = inlineOutputDirectoryPath;
                    continue;
                }

                if (IsSwitchMatch(argument, "--project")) {
                    if (!TryReadFollowingValue(args, ref index, out projectPath, out errorMessage)) {
                        return false;
                    }
                    continue;
                }

                if (IsSwitchMatch(argument, "--build")) {
                    if (!TryReadFollowingValue(args, ref index, out platformId, out errorMessage)) {
                        return false;
                    }
                    continue;
                }

                if (IsSwitchMatch(argument, "--output")) {
                    if (!TryReadFollowingValue(args, ref index, out outputDirectoryPath, out errorMessage)) {
                        return false;
                    }
                    continue;
                }

                if (IsSwitchMatch(argument, "--full-graph") || IsSwitchMatch(argument, "--common-output")) {
                    useCommonOutputDirectory = true;
                    continue;
                }

                if (argument.StartsWith("-", StringComparison.Ordinal)) {
                    errorMessage = $"Unrecognized command-line argument '{argument}'.";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(projectPath)) {
                errorMessage = "Build mode requires a project path supplied through `--project`.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(platformId)) {
                errorMessage = "Build mode requires a target platform supplied through `--build`.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(outputDirectoryPath)) {
                errorMessage = "Build mode requires an output directory supplied through `--output`.";
                return false;
            }

            options = new EditorCliBuildOptions(projectPath, platformId, outputDirectoryPath, useCommonOutputDirectory);
            return true;
        }

        /// <summary>
        /// Attempts to parse one headless editor-command invocation from the supplied arguments.
        /// </summary>
        /// <param name="args">Command-line arguments supplied by the shell.</param>
        /// <param name="options">Parsed editor-command options when parsing succeeds.</param>
        /// <param name="errorMessage">Human-readable parse error when parsing fails.</param>
        /// <returns>True when the arguments form one valid headless editor-command invocation.</returns>
        public static bool TryParseEditorCommandOptions(string[] args, out EditorCliCommandOptions options, out string errorMessage) {
            options = null;
            errorMessage = string.Empty;

            if (args == null || args.Length == 0) {
                errorMessage = "Editor command mode requires `--project` and `--editor-command` arguments.";
                return false;
            }

            string projectPath = string.Empty;
            string commandId = string.Empty;

            for (int index = 0; index < args.Length; index++) {
                string argument = args[index];
                if (string.IsNullOrWhiteSpace(argument)) {
                    continue;
                }

                if (TryReadInlineValue(argument, "--project", out string inlineProjectPath)) {
                    projectPath = inlineProjectPath;
                    continue;
                }

                if (TryReadInlineValue(argument, "--editor-command", out string inlineCommandId)) {
                    commandId = inlineCommandId;
                    continue;
                }

                if (IsSwitchMatch(argument, "--project")) {
                    if (!TryReadFollowingValue(args, ref index, out projectPath, out errorMessage)) {
                        return false;
                    }
                    continue;
                }

                if (IsSwitchMatch(argument, "--editor-command")) {
                    if (!TryReadFollowingValue(args, ref index, out commandId, out errorMessage)) {
                        return false;
                    }
                    continue;
                }

                if (argument.StartsWith("-", StringComparison.Ordinal)) {
                    errorMessage = $"Unrecognized command-line argument '{argument}'.";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(projectPath)) {
                errorMessage = "Editor command mode requires a project path supplied through `--project`.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(commandId)) {
                errorMessage = "Editor command mode requires a command id supplied through `--editor-command`.";
                return false;
            }

            options = new EditorCliCommandOptions(projectPath, commandId);
            return true;
        }

        /// <summary>
        /// Returns true when one argument matches a switch, including inline `--switch=value` forms.
        /// </summary>
        /// <param name="argument">Command-line argument to inspect.</param>
        /// <param name="switchName">Expected switch name.</param>
        /// <returns>True when the argument matches the requested switch.</returns>
        static bool IsSwitchMatch(string argument, string switchName) {
            if (string.IsNullOrWhiteSpace(argument) || string.IsNullOrWhiteSpace(switchName)) {
                return false;
            }

            if (string.Equals(argument, switchName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return argument.StartsWith(switchName + "=", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads one inline `--switch=value` argument value.
        /// </summary>
        /// <param name="argument">Command-line argument to inspect.</param>
        /// <param name="switchName">Expected switch name.</param>
        /// <param name="value">Parsed switch value when available.</param>
        /// <returns>True when the argument contains an inline value for the requested switch.</returns>
        static bool TryReadInlineValue(string argument, string switchName, out string value) {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(argument) || string.IsNullOrWhiteSpace(switchName)) {
                return false;
            }

            string prefix = switchName + "=";
            if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            value = argument[prefix.Length..];
            return true;
        }

        /// <summary>
        /// Reads one following argument value for a switch.
        /// </summary>
        /// <param name="args">Command-line argument array.</param>
        /// <param name="index">Current switch index, advanced when the value is consumed.</param>
        /// <param name="value">Parsed value when available.</param>
        /// <param name="errorMessage">Human-readable parse error when the value is missing.</param>
        /// <returns>True when a value was consumed.</returns>
        static bool TryReadFollowingValue(string[] args, ref int index, out string value, out string errorMessage) {
            value = string.Empty;
            errorMessage = string.Empty;

            int valueIndex = index + 1;
            if (args == null || valueIndex >= args.Length) {
                errorMessage = "Missing command-line value after the requested switch.";
                return false;
            }

            value = args[valueIndex];
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("-", StringComparison.Ordinal)) {
                errorMessage = "Missing command-line value after the requested switch.";
                return false;
            }

            index = valueIndex;
            return true;
        }
    }
}
