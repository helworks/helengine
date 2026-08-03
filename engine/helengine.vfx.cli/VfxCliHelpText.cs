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
        /// <returns>Help text describing how to invoke the tool and which effects exist.</returns>
        public static string BuildGeneralHelp() {
            var builder = new StringBuilder();
            builder.AppendLine(VfxCliArguments.UsageLine);
            builder.AppendLine("       helengine.vfx.cli --help [--effect <id>]");
            builder.AppendLine();
            builder.Append("Known effect ids: ");
            builder.Append(string.Join(", ", VfxEffectRegistry.KnownIds));
            return builder.ToString();
        }

        /// <summary>
        /// Builds the per-effect help block listing every parameter the effect accepts along with its
        /// value shape, default, and description.
        /// </summary>
        /// <param name="effect">Effect to describe.</param>
        /// <returns>Help text describing the effect and its parameters.</returns>
        public static string BuildEffectHelp(IVfxEffect effect) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Effect '{effect.Id}' ({effect.DisplayName}) parameters:");
            builder.Append(BuildParameterList(effect));
            return builder.ToString();
        }

        /// <summary>
        /// Builds just the indented parameter list for an effect, shared by help output and by the
        /// error message emitted when an unknown parameter name is supplied.
        /// </summary>
        /// <param name="effect">Effect whose parameters should be listed.</param>
        /// <returns>One indented line per parameter.</returns>
        public static string BuildParameterList(IVfxEffect effect) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }

            var builder = new StringBuilder();
            foreach (VfxEffectParameterDescriptor parameter in effect.Parameters) {
                builder.AppendLine(
                    $"  {parameter.Name} ({parameter.Type}, default {parameter.DefaultValueText}) - {parameter.Description}");
            }
            return builder.ToString();
        }
    }
}
