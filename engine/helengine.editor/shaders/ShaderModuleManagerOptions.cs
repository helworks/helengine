namespace helengine.editor {
    /// <summary>
    /// Configures shader module compilation and hot-reload behavior for the editor.
    /// </summary>
    public class ShaderModuleManagerOptions {
        /// <summary>
        /// Initializes a new options container for the shader module manager.
        /// </summary>
        /// <param name="shaderRootPath">Root directory containing shader source files.</param>
        /// <param name="packageOutputPath">Output directory for compiled shader packages.</param>
        /// <param name="buildOptions">Shader package build options.</param>
        /// <param name="runtimeTarget">Target backend used for runtime loading.</param>
        /// <param name="shaderBackendRegistry">Registry that supplies compiler backends for every configured build target.</param>
        /// <param name="buildDelayMilliseconds">Delay window used to coalesce file changes.</param>
        public ShaderModuleManagerOptions(
            string shaderRootPath,
            string packageOutputPath,
            ShaderPackageBuildOptions buildOptions,
            ShaderCompileTarget runtimeTarget,
            ShaderBackendRegistry shaderBackendRegistry,
            int buildDelayMilliseconds) {
            if (string.IsNullOrWhiteSpace(shaderRootPath)) {
                throw new ArgumentException("Shader root path must be provided.", nameof(shaderRootPath));
            }

            if (string.IsNullOrWhiteSpace(packageOutputPath)) {
                throw new ArgumentException("Package output path must be provided.", nameof(packageOutputPath));
            }

            if (buildOptions == null) {
                throw new ArgumentNullException(nameof(buildOptions));
            }

            if (!buildOptions.HasTarget(runtimeTarget)) {
                throw new ArgumentException("Runtime target must be included in the build options.", nameof(runtimeTarget));
            }

            if (shaderBackendRegistry == null) {
                throw new ArgumentNullException(nameof(shaderBackendRegistry));
            }

            if (buildDelayMilliseconds < 1) {
                throw new ArgumentOutOfRangeException(nameof(buildDelayMilliseconds), "Build delay must be at least 1ms.");
            }

            ValidateRegisteredTargets(buildOptions, shaderBackendRegistry);
            ShaderRootPath = shaderRootPath;
            PackageOutputPath = packageOutputPath;
            BuildOptions = buildOptions;
            RuntimeTarget = runtimeTarget;
            ShaderBackendRegistry = shaderBackendRegistry;
            BuildDelayMilliseconds = buildDelayMilliseconds;
        }

        /// <summary>
        /// Gets the shader source root directory.
        /// </summary>
        public string ShaderRootPath { get; }

        /// <summary>
        /// Gets the shader package output directory.
        /// </summary>
        public string PackageOutputPath { get; }

        /// <summary>
        /// Gets the shader package build options.
        /// </summary>
        public ShaderPackageBuildOptions BuildOptions { get; }

        /// <summary>
        /// Gets the runtime backend used for package loading.
        /// </summary>
        public ShaderCompileTarget RuntimeTarget { get; }

        /// <summary>
        /// Gets the registry that supplies shader compiler backends for package builds.
        /// </summary>
        public ShaderBackendRegistry ShaderBackendRegistry { get; }

        /// <summary>
        /// Gets the delay window used to coalesce file change events.
        /// </summary>
        public int BuildDelayMilliseconds { get; }

        /// <summary>
        /// Validates that every configured build target has one registered backend in the supplied registry.
        /// </summary>
        /// <param name="buildOptions">Build options whose targets must be supported.</param>
        /// <param name="shaderBackendRegistry">Registry that should satisfy the target list.</param>
        static void ValidateRegisteredTargets(
            ShaderPackageBuildOptions buildOptions,
            ShaderBackendRegistry shaderBackendRegistry) {
            for (int i = 0; i < buildOptions.Targets.Count; i++) {
                ShaderCompileTarget target = buildOptions.Targets[i].Target;
                if (!shaderBackendRegistry.ContainsTarget(target)) {
                    throw new ArgumentException("Every shader package target must have a registered backend.", nameof(shaderBackendRegistry));
                }
            }
        }
    }
}
