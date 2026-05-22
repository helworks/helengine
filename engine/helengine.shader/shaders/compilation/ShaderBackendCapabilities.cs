namespace helengine {
    /// <summary>
    /// Describes the compilation features available for a shader backend.
    /// </summary>
    public class ShaderBackendCapabilities {
        /// <summary>
        /// Initializes a new backend capabilities description.
        /// </summary>
        /// <param name="minimumShaderModel">Minimum shader model supported.</param>
        /// <param name="maximumShaderModel">Maximum shader model supported.</param>
        /// <param name="supportedStages">Stages supported by the backend.</param>
        /// <param name="supportsRayTracing">True when ray tracing stages are supported.</param>
        public ShaderBackendCapabilities(
            ShaderModel minimumShaderModel,
            ShaderModel maximumShaderModel,
            IReadOnlyList<ShaderStage> supportedStages,
            bool supportsRayTracing) {
            if (minimumShaderModel == null) {
                throw new ArgumentNullException(nameof(minimumShaderModel));
            }

            if (maximumShaderModel == null) {
                throw new ArgumentNullException(nameof(maximumShaderModel));
            }

            if (supportedStages == null) {
                throw new ArgumentNullException(nameof(supportedStages));
            }

            MinimumShaderModel = minimumShaderModel;
            MaximumShaderModel = maximumShaderModel;
            SupportedStages = supportedStages;
            SupportsRayTracing = supportsRayTracing;
        }

        /// <summary>
        /// Gets the minimum shader model supported by the backend.
        /// </summary>
        public ShaderModel MinimumShaderModel { get; }

        /// <summary>
        /// Gets the maximum shader model supported by the backend.
        /// </summary>
        public ShaderModel MaximumShaderModel { get; }

        /// <summary>
        /// Gets the stages supported by the backend.
        /// </summary>
        public IReadOnlyList<ShaderStage> SupportedStages { get; }

        /// <summary>
        /// Gets a value indicating whether ray tracing stages are supported.
        /// </summary>
        public bool SupportsRayTracing { get; }
    }
}
