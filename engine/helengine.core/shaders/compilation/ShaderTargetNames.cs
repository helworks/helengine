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
    }
}
