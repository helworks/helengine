namespace helengine.vfx.cli {
    /// <summary>
    /// Parsed command-line arguments for the VFX export CLI.
    /// </summary>
    public class VfxCliArguments {
        public string SourceFolder { get; private set; }
        public string MaskFolder { get; private set; }
        public string EffectId { get; private set; }
        public string OutputFolder { get; private set; }
        public IReadOnlyDictionary<string, string> ParameterValues { get; private set; }

        public static bool TryParse(string[] args, out VfxCliArguments parsed, out string error) {
            string sourceFolder = null;
            string maskFolder = null;
            string effectId = null;
            string outputFolder = null;
            var parameterValues = new Dictionary<string, string>();

            for (int i = 0; i < args.Length; i++) {
                switch (args[i]) {
                    case "--source":
                        if (!TryReadValue(args, ref i, out sourceFolder, out error)) { parsed = null; return false; }
                        break;
                    case "--mask":
                        if (!TryReadValue(args, ref i, out maskFolder, out error)) { parsed = null; return false; }
                        break;
                    case "--effect":
                        if (!TryReadValue(args, ref i, out effectId, out error)) { parsed = null; return false; }
                        break;
                    case "--out":
                        if (!TryReadValue(args, ref i, out outputFolder, out error)) { parsed = null; return false; }
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

            if (sourceFolder == null || maskFolder == null || effectId == null || outputFolder == null) {
                parsed = null;
                error = "Usage: helengine.vfx.cli --source <folder> --mask <folder> --effect <id> --out <folder> [--param name=value ...]";
                return false;
            }

            parsed = new VfxCliArguments {
                SourceFolder = sourceFolder,
                MaskFolder = maskFolder,
                EffectId = effectId,
                OutputFolder = outputFolder,
                ParameterValues = parameterValues
            };
            error = null;
            return true;
        }

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
