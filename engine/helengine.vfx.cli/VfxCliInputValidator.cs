using helengine.vfx;

namespace helengine.vfx.cli {
    /// <summary>
    /// Validates the caller's <c>--input</c> roles against the selected effect's declared
    /// <see cref="IVfxEffect.InputRoles"/> before any GPU work starts, so a mistyped or missing role
    /// fails immediately with a clean message instead of an obscure folder-not-found error mid-run.
    /// </summary>
    public static class VfxCliInputValidator {
        /// <summary>
        /// Rejects input role names the effect does not declare, and reports any role the effect
        /// requires that the caller did not supply.
        /// </summary>
        /// <param name="effect">Effect the input folders were supplied for.</param>
        /// <param name="inputFolders">Raw folder paths keyed by role name, collected from the command line.</param>
        /// <param name="error">Receives a caller-facing error message on failure, null on success.</param>
        /// <returns>True when every declared role has a folder and no unknown role was supplied.</returns>
        public static bool TryValidate(IVfxEffect effect, IReadOnlyDictionary<string, string> inputFolders, out string error) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }
            if (inputFolders == null) {
                throw new ArgumentNullException(nameof(inputFolders));
            }

            var knownRoles = new HashSet<string>(effect.InputRoles, StringComparer.Ordinal);
            var unknownRoles = new List<string>();
            foreach (string suppliedRole in inputFolders.Keys) {
                if (!knownRoles.Contains(suppliedRole)) {
                    unknownRoles.Add(suppliedRole);
                }
            }
            if (unknownRoles.Count > 0) {
                error = $"Unknown input role(s) for effect '{effect.Id}': {string.Join(", ", unknownRoles)}."
                    + Environment.NewLine
                    + RequiredRolesMessage(effect);
                return false;
            }

            var missingRoles = new List<string>();
            foreach (string requiredRole in effect.InputRoles) {
                if (!inputFolders.ContainsKey(requiredRole)) {
                    missingRoles.Add(requiredRole);
                }
            }
            if (missingRoles.Count > 0) {
                error = $"Missing input role(s) for effect '{effect.Id}': {string.Join(", ", missingRoles)}."
                    + Environment.NewLine
                    + RequiredRolesMessage(effect);
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Builds the "here is what this effect actually needs" line shared by every validation failure.
        /// </summary>
        /// <param name="effect">Effect to describe.</param>
        /// <returns>A one-line summary of the effect's required input roles.</returns>
        static string RequiredRolesMessage(IVfxEffect effect) {
            return $"Effect '{effect.Id}' ({effect.DisplayName}) requires input role(s): {string.Join(", ", effect.InputRoles)}.";
        }
    }
}
