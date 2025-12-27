namespace helengine {
    /// <summary>
    /// Represents a stable cache key for compiled shader artifacts.
    /// </summary>
    public class ShaderCompileCacheKey {
        /// <summary>
        /// Stores the precomputed key string.
        /// </summary>
        readonly string key;

        /// <summary>
        /// Initializes a new shader compile cache key.
        /// </summary>
        /// <param name="sourceHash">Hash of the shader source.</param>
        /// <param name="entryPoint">Entry point function name.</param>
        /// <param name="stage">Shader stage.</param>
        /// <param name="target">Compilation target.</param>
        /// <param name="shaderModel">Shader model version.</param>
        /// <param name="variant">Variant name.</param>
        /// <param name="definesSignature">Signature string for the define set.</param>
        /// <param name="bindingPolicySignature">Signature string for the binding policy.</param>
        public ShaderCompileCacheKey(
            string sourceHash,
            string entryPoint,
            ShaderStage stage,
            ShaderCompileTarget target,
            ShaderModel shaderModel,
            string variant,
            string definesSignature,
            string bindingPolicySignature) {
            if (string.IsNullOrWhiteSpace(sourceHash)) {
                throw new ArgumentException("Source hash must be provided.", nameof(sourceHash));
            }

            if (string.IsNullOrWhiteSpace(entryPoint)) {
                throw new ArgumentException("Entry point must be provided.", nameof(entryPoint));
            }

            if (shaderModel == null) {
                throw new ArgumentNullException(nameof(shaderModel));
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new ArgumentException("Variant name must be provided.", nameof(variant));
            }

            if (definesSignature == null) {
                throw new ArgumentNullException(nameof(definesSignature));
            }

            if (bindingPolicySignature == null) {
                throw new ArgumentNullException(nameof(bindingPolicySignature));
            }

            SourceHash = sourceHash;
            EntryPoint = entryPoint;
            Stage = stage;
            Target = target;
            ShaderModel = shaderModel;
            Variant = variant;
            DefinesSignature = definesSignature;
            BindingPolicySignature = bindingPolicySignature;
            key = BuildKey();
        }

        /// <summary>
        /// Gets the hash of the shader source.
        /// </summary>
        public string SourceHash { get; }

        /// <summary>
        /// Gets the entry point function name.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Gets the shader stage.
        /// </summary>
        public ShaderStage Stage { get; }

        /// <summary>
        /// Gets the compilation target.
        /// </summary>
        public ShaderCompileTarget Target { get; }

        /// <summary>
        /// Gets the shader model version.
        /// </summary>
        public ShaderModel ShaderModel { get; }

        /// <summary>
        /// Gets the variant name.
        /// </summary>
        public string Variant { get; }

        /// <summary>
        /// Gets the signature string for the define set.
        /// </summary>
        public string DefinesSignature { get; }

        /// <summary>
        /// Gets the signature string for the binding policy.
        /// </summary>
        public string BindingPolicySignature { get; }

        /// <summary>
        /// Returns the stable key string for cache lookup.
        /// </summary>
        /// <returns>Cache key string.</returns>
        public override string ToString() {
            return key;
        }

        /// <summary>
        /// Builds the stable cache key string from the stored parts.
        /// </summary>
        /// <returns>Cache key string.</returns>
        string BuildKey() {
            return string.Join(
                "|",
                SourceHash,
                EntryPoint,
                Stage.ToString(),
                Target.ToString(),
                ShaderModel.ToString(),
                Variant,
                DefinesSignature,
                BindingPolicySignature);
        }
    }
}
