using System.Text;
using helengine.vfx;

namespace helengine.vfx.cli {
    /// <summary>
    /// Builds the CLI's human-facing help output from the effect registry and each effect's declared
    /// parameter descriptors, so help text can never drift from what the effects actually accept.
    /// </summary>
    public static class VfxCliHelpText {
        /// <summary>
        /// Builds the general help block: invocation forms plus every registered effect id.
        /// </summary>
        /// <param name="catalog">Effects available to this invocation.</param>
        /// <returns>Help text describing how to invoke the tool and which effects exist.</returns>
        public static string BuildGeneralHelp(VfxEffectCatalog catalog) {
            var builder = new StringBuilder();
            builder.AppendLine(VfxCliArguments.UsageLine);
            builder.AppendLine("       helengine.vfx.cli composition capabilities [--project <dir>] --json");
            builder.AppendLine("       helengine.vfx.cli composition compile-edit --input <edit.json> --assets-root <root> --out <json> [--project <dir>] [--final true]");
            builder.AppendLine("       helengine.vfx.cli composition validate --input <json> --assets-root <root> [--project <dir>]");
            builder.AppendLine("       helengine.vfx.cli composition render --input <json> --assets-root <root> --out <file> --profile <id> [--project <dir>]");
            builder.AppendLine("       helengine.vfx.cli composition frame --input <json> --assets-root <root> --time <n/d> --out <png> [--project <dir>]");
            builder.AppendLine("       helengine.vfx.cli --help [--effect <id>] [--project <dir>]");
            builder.AppendLine("       helengine.vfx.cli captions --help (SRT/Whisper to transparent PNG sequence)");
            builder.AppendLine();
            builder.Append("Known effect ids: ");
            builder.Append(string.Join(", ", catalog.KnownIds));
            return builder.ToString();
        }

        /// <summary>
        /// Builds the per-effect help block listing every parameter the effect accepts along with its
        /// value shape, default, and description.
        /// </summary>
        /// <param name="effect">Effect to describe.</param>
        /// <returns>Help text describing the effect and its parameters.</returns>
        public static string BuildEffectHelp(EffectAsset effect) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Effect '{effect.EffectId}' ({effect.DisplayName}), {effect.Passes.Length} pass(es)");
            builder.AppendLine($"  Required --input role(s): {string.Join(", ", effect.Inputs.Select(input => input.Name))}");
            builder.AppendLine("Parameters:");
            builder.Append(BuildParameterList(effect));
            return builder.ToString();
        }

        /// <summary>
        /// Builds just the indented parameter list for an effect, shared by help output and by the
        /// error message emitted when an unknown parameter name is supplied.
        /// </summary>
        /// <param name="effect">Effect whose parameters should be listed.</param>
        /// <returns>One indented line per parameter.</returns>
        public static string BuildParameterList(EffectAsset effect) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }

            var builder = new StringBuilder();
            foreach (EffectParameterAsset parameter in effect.Parameters) {
                string defaultText = parameter.Type == EffectParameterType.Enum
                    ? parameter.AllowedValues[(int)parameter.DefaultValue.X]
                    : string.Join(",", VfxParameterSlotResolver.Defaults(parameter).Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                builder.AppendLine(
                    $"  {parameter.Name} ({parameter.Type}, default {defaultText}) - {parameter.Description}");
            }
            return builder.ToString();
        }
    }
}
