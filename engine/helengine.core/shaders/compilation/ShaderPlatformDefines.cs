namespace helengine {
    /// <summary>
    /// Builds standard preprocessor defines for shader platform compilation.
    /// </summary>
    public static class ShaderPlatformDefines {
        /// <summary>
        /// Builds a define list for a target platform and shader model.
        /// </summary>
        /// <param name="target">Compilation target.</param>
        /// <param name="shaderModel">Shader model version.</param>
        /// <param name="additionalDefines">Additional defines to include.</param>
        /// <returns>Array of defines for compilation.</returns>
        public static ShaderDefine[] BuildDefines(
            ShaderCompileTarget target,
            ShaderModel shaderModel,
            IReadOnlyList<ShaderDefine> additionalDefines) {
            if (shaderModel == null) {
                throw new ArgumentNullException(nameof(shaderModel));
            }

            if (additionalDefines == null) {
                throw new ArgumentNullException(nameof(additionalDefines));
            }

            List<ShaderDefine> defines = new List<ShaderDefine>(additionalDefines.Count + 3);
            AddTargetDefine(target, defines);
            defines.Add(new ShaderDefine("HEL_SM_MAJOR", shaderModel.Major.ToString()));
            defines.Add(new ShaderDefine("HEL_SM_MINOR", shaderModel.Minor.ToString()));

            for (int i = 0; i < additionalDefines.Count; i++) {
                defines.Add(additionalDefines[i]);
            }

            return defines.ToArray();
        }

        /// <summary>
        /// Adds the target-specific API define to the list.
        /// </summary>
        /// <param name="target">Compilation target.</param>
        /// <param name="defines">List to append to.</param>
        static void AddTargetDefine(ShaderCompileTarget target, List<ShaderDefine> defines) {
            string defineName = GetTargetDefineName(target);
            defines.Add(new ShaderDefine(defineName, "1"));
        }

        /// <summary>
        /// Maps a target into its standard API define name.
        /// </summary>
        /// <param name="target">Compilation target.</param>
        /// <returns>Define name to emit.</returns>
        static string GetTargetDefineName(ShaderCompileTarget target) {
            switch (target) {
                case ShaderCompileTarget.DirectX9:
                    return "HEL_API_DX9";
                case ShaderCompileTarget.DirectX11:
                    return "HEL_API_DX11";
                case ShaderCompileTarget.DirectX12:
                    return "HEL_API_DX12";
                case ShaderCompileTarget.Vulkan:
                    return "HEL_API_VULKAN";
                case ShaderCompileTarget.Metal:
                    return "HEL_API_METAL";
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), "Unsupported compile target.");
            }
        }
    }
}
