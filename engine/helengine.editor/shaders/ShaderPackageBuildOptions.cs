namespace helengine.editor {
    /// <summary>
    /// Configures shader package build settings for the editor pipeline.
    /// </summary>
    public class ShaderPackageBuildOptions {
        /// <summary>
        /// Stores target-specific build options.
        /// </summary>
        readonly ShaderTargetBuildOptions[] targets;

        /// <summary>
        /// Stores additional defines applied to every compilation.
        /// </summary>
        readonly ShaderDefine[] defines;

        /// <summary>
        /// Initializes a new shader package build options instance.
        /// </summary>
        /// <param name="targets">Target-specific build options.</param>
        /// <param name="bindingPolicy">Binding policy used for reflection normalization.</param>
        /// <param name="generateDebugInfo">True to generate debug information.</param>
        /// <param name="optimize">True to enable shader optimization.</param>
        /// <param name="treatWarningsAsErrors">True to treat warnings as errors.</param>
        /// <param name="defines">Additional defines applied to every compilation.</param>
        public ShaderPackageBuildOptions(
            ShaderTargetBuildOptions[] targets,
            ShaderBindingPolicy bindingPolicy,
            bool generateDebugInfo,
            bool optimize,
            bool treatWarningsAsErrors,
            ShaderDefine[] defines) {
            if (targets == null) {
                throw new ArgumentNullException(nameof(targets));
            }

            if (targets.Length == 0) {
                throw new ArgumentException("At least one target build option must be provided.", nameof(targets));
            }

            if (bindingPolicy == null) {
                throw new ArgumentNullException(nameof(bindingPolicy));
            }

            if (defines == null) {
                throw new ArgumentNullException(nameof(defines));
            }

            this.targets = targets;
            BindingPolicy = bindingPolicy;
            GenerateDebugInfo = generateDebugInfo;
            Optimize = optimize;
            TreatWarningsAsErrors = treatWarningsAsErrors;
            this.defines = defines;
        }

        /// <summary>
        /// Gets the target-specific build options.
        /// </summary>
        public IReadOnlyList<ShaderTargetBuildOptions> Targets {
            get {
                return targets;
            }
        }

        /// <summary>
        /// Gets the binding policy used for reflection normalization.
        /// </summary>
        public ShaderBindingPolicy BindingPolicy { get; }

        /// <summary>
        /// Gets a value indicating whether debug information is generated.
        /// </summary>
        public bool GenerateDebugInfo { get; }

        /// <summary>
        /// Gets a value indicating whether shader optimization is enabled.
        /// </summary>
        public bool Optimize { get; }

        /// <summary>
        /// Gets a value indicating whether warnings are treated as errors.
        /// </summary>
        public bool TreatWarningsAsErrors { get; }

        /// <summary>
        /// Gets the additional defines applied to every compilation.
        /// </summary>
        public IReadOnlyList<ShaderDefine> Defines {
            get {
                return defines;
            }
        }

        /// <summary>
        /// Checks whether the target list includes the requested target.
        /// </summary>
        /// <param name="target">Target to look up.</param>
        /// <returns>True when the target is included.</returns>
        public bool HasTarget(ShaderCompileTarget target) {
            for (int i = 0; i < targets.Length; i++) {
                if (targets[i].Target == target) {
                    return true;
                }
            }

            return false;
        }
    }
}
