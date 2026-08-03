using helengine.vfx;

namespace helengine.vfx.cli {
    /// <summary>
    /// Validates the caller's <c>--param</c> values against the selected effect's declared parameters
    /// before any GPU work starts, so a typo or an out-of-range value fails immediately with a clean
    /// message instead of silently running with defaults or crashing after device creation.
    /// </summary>
    public static class VfxCliParameterValidator {
        /// <summary>
        /// Rejects unknown parameter names and unparseable parameter values for the selected effect.
        /// </summary>
        /// <param name="effect">Effect the parameters were supplied for.</param>
        /// <param name="parameterValues">Raw parameter name/value pairs collected from the command line.</param>
        /// <param name="error">Receives a caller-facing error message on failure, null on success.</param>
        /// <returns>True when every supplied parameter is both known to the effect and parseable.</returns>
        public static bool TryValidate(IVfxEffect effect, IReadOnlyDictionary<string, string> parameterValues, out string error) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }
            if (parameterValues == null) {
                throw new ArgumentNullException(nameof(parameterValues));
            }

            var knownNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (VfxEffectParameterDescriptor parameter in effect.Parameters) {
                knownNames.Add(parameter.Name);
            }

            var unknownNames = new List<string>();
            foreach (string suppliedName in parameterValues.Keys) {
                if (!knownNames.Contains(suppliedName)) {
                    unknownNames.Add(suppliedName);
                }
            }

            if (unknownNames.Count > 0) {
                error = $"Unknown parameter name(s) for effect '{effect.Id}': {string.Join(", ", unknownNames)}."
                    + Environment.NewLine
                    + $"Effect '{effect.Id}' ({effect.DisplayName}) accepts:"
                    + Environment.NewLine
                    + VfxCliHelpText.BuildParameterList(effect).TrimEnd();
                return false;
            }

            // Resolving the slots here is what actually range-checks the values (easing names, colors,
            // numbers). Doing it before the GPU device exists is the whole point of this early pass;
            // the runner resolving them again later is cheap and keeps its API self-contained.
            try {
                effect.ResolveParameterSlots(parameterValues);
            } catch (ArgumentException ex) {
                error = ex.Message
                    + Environment.NewLine
                    + $"Effect '{effect.Id}' ({effect.DisplayName}) accepts:"
                    + Environment.NewLine
                    + VfxCliHelpText.BuildParameterList(effect).TrimEnd();
                return false;
            }

            error = null;
            return true;
        }
    }
}
