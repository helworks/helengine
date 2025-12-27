using helengine;

namespace helshader {
    /// <summary>
    /// Builds output file names for compiled shader artifacts.
    /// </summary>
    public class ShaderOutputNamer {
        /// <summary>
        /// Provides stage suffix mappings.
        /// </summary>
        readonly ShaderStageResolver stageResolver;

        /// <summary>
        /// Initializes a new output namer.
        /// </summary>
        public ShaderOutputNamer() {
            stageResolver = new ShaderStageResolver();
        }

        /// <summary>
        /// Builds the output file name for a compiled shader.
        /// </summary>
        /// <param name="shaderName">Shader module name.</param>
        /// <param name="stage">Shader stage.</param>
        /// <param name="target">Target backend.</param>
        /// <param name="variant">Variant name.</param>
        /// <returns>Output file name.</returns>
        public string GetBinaryFileName(string shaderName, ShaderStage stage, string target, string variant) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            if (string.IsNullOrWhiteSpace(target)) {
                throw new ArgumentException("Target must be provided.", nameof(target));
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new ArgumentException("Variant must be provided.", nameof(variant));
            }

            string stageSuffix = stageResolver.GetStageSuffix(stage);
            string extension = ResolveExtension(target);
            return $"{shaderName}.{stageSuffix}.{target}.{variant}.{extension}";
        }

        /// <summary>
        /// Resolves the file extension for a target backend.
        /// </summary>
        /// <param name="target">Target backend.</param>
        /// <returns>File extension without a leading dot.</returns>
        string ResolveExtension(string target) {
            if (string.Equals(target, "vulkan", StringComparison.OrdinalIgnoreCase)) {
                return "spirv";
            }

            if (string.Equals(target, "metal", StringComparison.OrdinalIgnoreCase)) {
                return "msl";
            }

            return "bin";
        }
    }
}
