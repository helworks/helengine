namespace helengine.editor {
    /// <summary>Shares shader target and generated package selection between CLI authoring and scene cooks.</summary>
    internal static class EditorPlatformShaderTargetResolver {
        /// <summary>Resolves the native shader API while preserving the existing console and legacy desktop selections.</summary>
        /// <param name="platformId">Build target identifier, independent of the authoring host.</param>
        /// <returns>Shader package target used by the selected player.</returns>
        internal static ShaderCompileTarget Resolve(string platformId) {
            if (string.Equals(platformId, "xbox", StringComparison.OrdinalIgnoreCase)) {
                return ShaderCompileTarget.Xbox;
            } else if (string.Equals(platformId, "linux", StringComparison.OrdinalIgnoreCase)) {
                return ShaderCompileTarget.Vulkan;
            } else if (string.Equals(platformId, "macos", StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformId, "ios", StringComparison.OrdinalIgnoreCase)) {
                return ShaderCompileTarget.Metal;
            }
            return string.Equals(platformId, "psvita", StringComparison.OrdinalIgnoreCase)
                ? ShaderCompileTarget.PsVita
                : ShaderCompileTarget.DirectX11;
        }

        /// <summary>Names a generated standard shader payload with the same target suffix as its compiled contents.</summary>
        /// <param name="platformId">Build target that owns the generated shader.</param>
        /// <returns>Content-relative shader package path.</returns>
        internal static string ResolveStandardShaderRelativePath(string platformId) {
            return CanonicalPackagedAssetPath.Normalize("cooked/shaders/ForwardStandardShader." + ShaderTargetNames.GetTargetName(Resolve(platformId)) + ".hasset");
        }
    }
}
