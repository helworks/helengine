namespace helengine {
    /// <summary>
    /// Describes compilation flags used across shader backends.
    /// </summary>
    public class ShaderCompileOptions {
        /// <summary>
        /// Initializes a new shader compile options set.
        /// </summary>
        /// <param name="bindingPolicy">Binding policy used to normalize reflection slots.</param>
        /// <param name="generateDebugInfo">True to generate debug information.</param>
        /// <param name="optimize">True to enable backend optimization.</param>
        /// <param name="treatWarningsAsErrors">True to convert warnings into errors.</param>
        public ShaderCompileOptions(
            ShaderBindingPolicy bindingPolicy,
            bool generateDebugInfo,
            bool optimize,
            bool treatWarningsAsErrors) {
            if (bindingPolicy == null) {
                throw new ArgumentNullException(nameof(bindingPolicy));
            }

            BindingPolicy = bindingPolicy;
            GenerateDebugInfo = generateDebugInfo;
            Optimize = optimize;
            TreatWarningsAsErrors = treatWarningsAsErrors;
        }

        /// <summary>
        /// Gets the binding policy used to normalize reflection slots.
        /// </summary>
        public ShaderBindingPolicy BindingPolicy { get; }

        /// <summary>
        /// Gets a value indicating whether debug information is generated.
        /// </summary>
        public bool GenerateDebugInfo { get; }

        /// <summary>
        /// Gets a value indicating whether backend optimization is enabled.
        /// </summary>
        public bool Optimize { get; }

        /// <summary>
        /// Gets a value indicating whether warnings are treated as errors.
        /// </summary>
        public bool TreatWarningsAsErrors { get; }
    }
}
