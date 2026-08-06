namespace helengine.vfx.cli {
    /// <summary>
    /// Parsed command-line arguments for the VFX export CLI.
    /// </summary>
    public class VfxCliArguments {
        /// <summary>
        /// One-line invocation summary printed whenever arguments are missing or malformed.
        /// </summary>
        public const string UsageLine =
            "Usage: helengine.vfx.cli --input <Role>=<folder> [--input <Role>=<folder> ...] --effect <id> --out <folder> [--param name=value ...]";

        /// <summary>
        /// Input folder paths keyed by the role name the selected effect expects (e.g. "Source",
        /// "Mask"). Empty when only help was requested.
        /// </summary>
        public IReadOnlyDictionary<string, string> InputFolders { get; private set; }

        /// <summary>
        /// Id of the effect to run, or the effect to describe when help was requested.
        /// </summary>
        public string EffectId { get; private set; }

        /// <summary>
        /// Folder the processed EXR frames are written into. Null when only help was requested.
        /// </summary>
        public string OutputFolder { get; private set; }

        /// <summary>
        /// Raw effect parameter name/value pairs collected from every <c>--param</c> occurrence.
        /// </summary>
        public IReadOnlyDictionary<string, string> ParameterValues { get; private set; }

        /// <summary>
        /// True when the caller asked for help instead of an export run; the export-specific
        /// arguments are then optional and may be null or empty.
        /// </summary>
        public bool ShowHelp { get; private set; }

        /// <summary>
        /// Parses a raw argument array, reporting a caller-facing message instead of throwing when
        /// the arguments are malformed or incomplete.
        /// </summary>
        /// <param name="args">Raw process arguments.</param>
        /// <param name="parsed">Receives the parsed arguments on success, null on failure.</param>
        /// <param name="error">Receives a caller-facing error message on failure, null on success.</param>
        /// <returns>True when the arguments parsed successfully.</returns>
        public static bool TryParse(string[] args, out VfxCliArguments parsed, out string error) {
            var inputFolders = new Dictionary<string, string>(StringComparer.Ordinal);
            string effectId = null;
            string outputFolder = null;
            bool showHelp = false;
            var parameterValues = new Dictionary<string, string>();

            for (int i = 0; i < args.Length; i++) {
                switch (args[i]) {
                    case "--input":
                        if (!TryReadValue(args, ref i, out string inputText, out error)) { parsed = null; return false; }
                        string[] inputParts = inputText.Split('=', 2);
                        if (inputParts.Length != 2) {
                            parsed = null;
                            error = $"Invalid --input value '{inputText}'. Expected Role=folder.";
                            return false;
                        }
                        inputFolders[inputParts[0]] = inputParts[1];
                        break;
                    case "--effect":
                        if (!TryReadValue(args, ref i, out effectId, out error)) { parsed = null; return false; }
                        break;
                    case "--out":
                        if (!TryReadValue(args, ref i, out outputFolder, out error)) { parsed = null; return false; }
                        break;
                    case "--help":
                    case "-h":
                        showHelp = true;
                        break;
                    case "--param":
                        if (!TryReadValue(args, ref i, out string paramText, out error)) { parsed = null; return false; }
                        string[] parts = paramText.Split('=', 2);
                        if (parts.Length != 2) {
                            parsed = null;
                            error = $"Invalid --param value '{paramText}'. Expected name=value.";
                            return false;
                        }
                        parameterValues[parts[0]] = parts[1];
                        break;
                    default:
                        parsed = null;
                        error = $"Unknown argument '{args[i]}'.";
                        return false;
                }
            }

            if (!showHelp && (inputFolders.Count == 0 || effectId == null || outputFolder == null)) {
                parsed = null;
                error = UsageLine;
                return false;
            }

            parsed = new VfxCliArguments {
                InputFolders = inputFolders,
                EffectId = effectId,
                OutputFolder = outputFolder,
                ParameterValues = parameterValues,
                ShowHelp = showHelp
            };
            error = null;
            return true;
        }

        /// <summary>
        /// Reads the value that follows a flag, advancing the loop index past it.
        /// </summary>
        /// <param name="args">Raw process arguments.</param>
        /// <param name="i">Index of the flag; advanced to the index of its value on success.</param>
        /// <param name="value">Receives the flag's value on success, null on failure.</param>
        /// <param name="error">Receives a caller-facing error message on failure, null on success.</param>
        /// <returns>True when a value followed the flag.</returns>
        static bool TryReadValue(string[] args, ref int i, out string value, out string error) {
            if (i + 1 >= args.Length) {
                value = null;
                error = $"Argument '{args[i]}' requires a value.";
                return false;
            }
            i++;
            value = args[i];
            error = null;
            return true;
        }
    }
}
