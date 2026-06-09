namespace helengine {
    /// <summary>
    /// Stores the shader compiler backends explicitly registered by bootstrap code for later compile-service construction.
    /// </summary>
    public class ShaderBackendRegistry {
        /// <summary>
        /// Stores registered backends by compile target.
        /// </summary>
        readonly Dictionary<ShaderCompileTarget, IShaderBackend> Backends;

        /// <summary>
        /// Initializes an empty backend registry.
        /// </summary>
        public ShaderBackendRegistry() {
            Backends = new Dictionary<ShaderCompileTarget, IShaderBackend>();
        }

        /// <summary>
        /// Registers one backend implementation for its declared compile target.
        /// </summary>
        /// <param name="backend">Backend implementation to register.</param>
        public void Register(IShaderBackend backend) {
            if (backend == null) {
                throw new ArgumentNullException(nameof(backend));
            }

            Backends[backend.Target] = backend;
        }

        /// <summary>
        /// Returns whether the registry contains a backend for the requested compile target.
        /// </summary>
        /// <param name="target">Compile target to check.</param>
        /// <returns>True when a backend has been registered for the target.</returns>
        public bool ContainsTarget(ShaderCompileTarget target) {
            return Backends.ContainsKey(target);
        }

        /// <summary>
        /// Registers every stored backend into the supplied compile service.
        /// </summary>
        /// <param name="compileService">Compile service that should receive the registered backends.</param>
        public void RegisterBackends(ShaderCompileService compileService) {
            if (compileService == null) {
                throw new ArgumentNullException(nameof(compileService));
            }

            foreach (ShaderCompileTarget target in Backends.Keys) {
                compileService.RegisterBackend(Backends[target]);
            }
        }

        /// <summary>
        /// Creates a compile service pre-populated with the registry's currently registered backends.
        /// </summary>
        /// <param name="includeResolver">Resolver used for shader includes.</param>
        /// <param name="cache">Cache used to store compiled shader results.</param>
        /// <param name="sourceHasher">Hasher used to build compile cache keys.</param>
        /// <returns>Compile service configured with the registered backends.</returns>
        public ShaderCompileService CreateCompileService(
            IShaderIncludeResolver includeResolver,
            IShaderCompileCache cache,
            ShaderSourceHasher sourceHasher) {
            ShaderCompileService compileService = new ShaderCompileService(includeResolver, cache, sourceHasher);
            RegisterBackends(compileService);
            return compileService;
        }
    }
}
