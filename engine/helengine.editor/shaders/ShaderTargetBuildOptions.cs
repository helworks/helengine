namespace helengine.editor {
    /// <summary>
    /// Describes shader compilation settings for a specific backend target.
    /// </summary>
    public class ShaderTargetBuildOptions {
        /// <summary>
        /// Initializes a new target build options instance.
        /// </summary>
        /// <param name="target">Compilation target backend.</param>
        /// <param name="shaderModel">Shader model to use for the target.</param>
        public ShaderTargetBuildOptions(ShaderCompileTarget target, ShaderModel shaderModel) {
            if (shaderModel == null) {
                throw new ArgumentNullException(nameof(shaderModel));
            }

            Target = target;
            ShaderModel = shaderModel;
        }

        /// <summary>
        /// Gets the compilation target backend.
        /// </summary>
        public ShaderCompileTarget Target { get; }

        /// <summary>
        /// Gets the shader model version to use for the target.
        /// </summary>
        public ShaderModel ShaderModel { get; }
    }
}
