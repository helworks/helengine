namespace helengine {
    /// <summary>
    /// Provides stable string identifiers for shader compilation targets.
    /// </summary>
    public static class ShaderTargetNames {
        /// <summary>
        /// Returns a stable lowercase target identifier.
        /// </summary>
        /// <param name="target">Target to map.</param>
        /// <returns>Lowercase target name.</returns>
        public static string GetTargetName(ShaderCompileTarget target) {
            switch (target) {
                case ShaderCompileTarget.DirectX9:
                    return "dx9";
                case ShaderCompileTarget.DirectX11:
                    return "dx11";
                case ShaderCompileTarget.DirectX12:
                    return "dx12";
                case ShaderCompileTarget.Vulkan:
                    return "vulkan";
                case ShaderCompileTarget.Metal:
                    return "metal";
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), "Unsupported compile target.");
            }
        }

        /// <summary>
        /// Attempts to parse a target name into a compile target enum value.
        /// </summary>
        /// <param name="name">Target name to parse.</param>
        /// <param name="target">Parsed target value when successful.</param>
        /// <returns>True when the name matches a known target.</returns>
        public static bool TryParseTarget(string name, out ShaderCompileTarget target) {
            if (string.IsNullOrWhiteSpace(name)) {
                target = ShaderCompileTarget.DirectX11;
                return false;
            }

            switch (name.Trim().ToLowerInvariant()) {
                case "dx9":
                    target = ShaderCompileTarget.DirectX9;
                    return true;
                case "dx11":
                    target = ShaderCompileTarget.DirectX11;
                    return true;
                case "dx12":
                    target = ShaderCompileTarget.DirectX12;
                    return true;
                case "vulkan":
                    target = ShaderCompileTarget.Vulkan;
                    return true;
                case "metal":
                    target = ShaderCompileTarget.Metal;
                    return true;
                default:
                    target = ShaderCompileTarget.DirectX11;
                    return false;
            }
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
