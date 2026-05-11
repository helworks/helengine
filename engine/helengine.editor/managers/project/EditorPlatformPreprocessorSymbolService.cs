using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Resolves shared platform-specific preprocessor symbols used by gameplay and generated-core codegen.
    /// </summary>
    internal static class EditorPlatformPreprocessorSymbolService {
        /// <summary>
        /// Resolves the platform-specific symbols that should be defined while compiling authored gameplay code.
        /// </summary>
        /// <param name="platformId">Stable platform identifier for the active build.</param>
        /// <returns>Ordered platform-specific symbols.</returns>
        public static IReadOnlyList<string> ResolveGameplaySymbols(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId, "windows", StringComparison.OrdinalIgnoreCase)) {
                return [
                    "HELENGINE_INPUT_KEYBOARD",
                    "HELENGINE_INPUT_MOUSE",
                    "DESKTOP_PLATFORM"
                ];
            }

            if (string.Equals(platformId, "ps2", StringComparison.OrdinalIgnoreCase)) {
                return [
                    "PS2_PLATFORM"
                ];
            }

            if (string.Equals(platformId, "psp", StringComparison.OrdinalIgnoreCase)) {
                return [
                    "PSP_PLATFORM"
                ];
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Resolves the generated-core input codegen symbols required by the supplied platform definition.
        /// </summary>
        /// <param name="platformDefinition">Typed platform metadata exposed by the active builder.</param>
        /// <returns>Ordered platform and generated-core compatibility symbols.</returns>
        public static IReadOnlyList<string> ResolvePortableInputSymbols(PlatformDefinition platformDefinition) {
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }

            List<string> symbols = [.. ResolveGameplaySymbols(platformDefinition.PlatformId)];
            symbols.Add("HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION");
            symbols.Add("HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION");
            return symbols;
        }
    }
}
