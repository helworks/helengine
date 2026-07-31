namespace helengine {
    /// <summary>
    /// Provides shared runtime shader binding policy presets.
    /// </summary>
    public static class ShaderBindingPolicies {
        /// <summary>
        /// Gets the default binding policy with resource class shifts for cross-API layouts.
        /// </summary>
        public static ShaderBindingPolicy Default {
            get {
                return new ShaderBindingPolicy(0, 0, 100, 200, 300);
            }
        }
    }
}
