namespace helshader {
    /// <summary>
    /// Builds preprocessor define lists for shader compilation.
    /// </summary>
    public class ShaderDefineBuilder {
        /// <summary>
        /// Builds a define list for a target and variant.
        /// </summary>
        /// <param name="variant">Variant definition.</param>
        /// <param name="target">Target backend.</param>
        /// <param name="globalDefines">Global defines to include.</param>
        /// <returns>Define list.</returns>
        public string[] BuildDefines(ShaderManifestVariant variant, string target, IReadOnlyList<string> globalDefines) {
            if (variant == null) {
                throw new ArgumentNullException(nameof(variant));
            }

            if (string.IsNullOrWhiteSpace(target)) {
                throw new ArgumentException("Target must be provided.", nameof(target));
            }

            List<string> defines = new List<string>();
            AddGlobalDefines(defines, globalDefines);
            AddVariantDefines(defines, variant.Defines);
            AddTargetDefine(defines, target);
            return defines.ToArray();
        }

        /// <summary>
        /// Adds global defines to the list.
        /// </summary>
        /// <param name="defines">Destination define list.</param>
        /// <param name="globalDefines">Global defines.</param>
        void AddGlobalDefines(List<string> defines, IReadOnlyList<string> globalDefines) {
            if (globalDefines == null) {
                return;
            }

            for (int i = 0; i < globalDefines.Count; i++) {
                string define = globalDefines[i];
                if (string.IsNullOrWhiteSpace(define)) {
                    continue;
                }

                defines.Add(define);
            }
        }

        /// <summary>
        /// Adds variant defines to the list.
        /// </summary>
        /// <param name="defines">Destination define list.</param>
        /// <param name="variantDefines">Variant defines.</param>
        void AddVariantDefines(List<string> defines, string[] variantDefines) {
            if (variantDefines == null) {
                return;
            }

            for (int i = 0; i < variantDefines.Length; i++) {
                string define = variantDefines[i];
                if (string.IsNullOrWhiteSpace(define)) {
                    continue;
                }

                defines.Add(define);
            }
        }

        /// <summary>
        /// Adds the target macro define.
        /// </summary>
        /// <param name="defines">Destination define list.</param>
        /// <param name="target">Target backend.</param>
        void AddTargetDefine(List<string> defines, string target) {
            string macro = ResolveTargetMacro(target);
            if (!string.IsNullOrWhiteSpace(macro)) {
                defines.Add(macro);
            }
        }

        /// <summary>
        /// Resolves the target macro define name.
        /// </summary>
        /// <param name="target">Target backend.</param>
        /// <returns>Macro define.</returns>
        string ResolveTargetMacro(string target) {
            if (string.Equals(target, "dx9", StringComparison.OrdinalIgnoreCase)) {
                return "HEL_DX9=1";
            }

            if (string.Equals(target, "dx11", StringComparison.OrdinalIgnoreCase)) {
                return "HEL_DX11=1";
            }

            if (string.Equals(target, "dx12", StringComparison.OrdinalIgnoreCase)) {
                return "HEL_DX12=1";
            }

            if (string.Equals(target, "vulkan", StringComparison.OrdinalIgnoreCase)) {
                return "HEL_VULKAN=1";
            }

            if (string.Equals(target, "metal", StringComparison.OrdinalIgnoreCase)) {
                return "HEL_METAL=1";
            }

            throw new InvalidOperationException($"Unsupported target '{target}'.");
        }
    }
}
