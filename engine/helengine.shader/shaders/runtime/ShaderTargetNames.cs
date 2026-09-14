namespace helengine {
    /// <summary>
    /// Provides stable string identifiers for runtime shader package targets by reading the shared shader target descriptor table.
    /// </summary>
    public static class ShaderTargetNames {
        /// <summary>
        /// Returns a stable lowercase target identifier.
        /// </summary>
        /// <param name="target">Target to map.</param>
        /// <returns>Lowercase target name.</returns>
        public static string GetTargetName(ShaderCompileTarget target) {
            return ShaderTargetDescriptors.Get(target).Name;
        }

        /// <summary>
        /// Attempts to parse a target name into a compile target enum value.
        /// </summary>
        /// <param name="name">Target name to parse.</param>
        /// <param name="target">Parsed target value when successful.</param>
        /// <returns>True when the name matches a known target.</returns>
        public static bool TryParseTarget(string name, out ShaderCompileTarget target) {
            ShaderTargetDescriptor descriptor;
            if (!ShaderTargetDescriptors.TryGetByName(name, out descriptor)) {
                target = ShaderCompileTarget.DirectX11;
                return false;
            }

            target = descriptor.Target;
            return true;
        }

        /// <summary>
        /// Parses a target name into a compile target enum value.
        /// </summary>
        /// <param name="name">Target name to parse.</param>
        /// <returns>Parsed compile target.</returns>
        public static ShaderCompileTarget ParseTarget(string name) {
            ShaderCompileTarget target;
            if (!TryParseTarget(name, out target)) {
                throw new ArgumentException("Target name was not recognized.", nameof(name));
            }

            return target;
        }
    }
}
