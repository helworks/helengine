namespace helengine {
    /// <summary>
    /// Coordinates shader compilation across registered backend compilers.
    /// </summary>
    public class ShaderCompileService {
        /// <summary>
        /// Stores registered backend compilers.
        /// </summary>
        readonly Dictionary<ShaderCompileTarget, IShaderBackend> backends;

        /// <summary>
        /// Stores the include resolver used during compilation.
        /// </summary>
        readonly IShaderIncludeResolver includeResolver;

        /// <summary>
        /// Stores the cache used to reuse compiled shader results.
        /// </summary>
        readonly IShaderCompileCache cache;

        /// <summary>
        /// Stores the hasher used to build cache keys.
        /// </summary>
        readonly ShaderSourceHasher sourceHasher;

        /// <summary>
        /// Initializes a new shader compile service.
        /// </summary>
        /// <param name="includeResolver">Resolver used for shader includes.</param>
        /// <param name="cache">Cache used to store compiled shader results.</param>
        /// <param name="sourceHasher">Hasher used to build cache keys.</param>
        public ShaderCompileService(
            IShaderIncludeResolver includeResolver,
            IShaderCompileCache cache,
            ShaderSourceHasher sourceHasher) {
            if (includeResolver == null) {
                throw new ArgumentNullException(nameof(includeResolver));
            }

            if (cache == null) {
                throw new ArgumentNullException(nameof(cache));
            }

            if (sourceHasher == null) {
                throw new ArgumentNullException(nameof(sourceHasher));
            }

            this.includeResolver = includeResolver;
            this.cache = cache;
            this.sourceHasher = sourceHasher;
            backends = new Dictionary<ShaderCompileTarget, IShaderBackend>();
        }

        /// <summary>
        /// Registers a backend compiler with the service.
        /// </summary>
        /// <param name="backend">Backend compiler instance.</param>
        public void RegisterBackend(IShaderBackend backend) {
            if (backend == null) {
                throw new ArgumentNullException(nameof(backend));
            }

            backends[backend.Target] = backend;
        }

        /// <summary>
        /// Compiles a shader entry point using a registered backend.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <returns>Compilation result.</returns>
        public ShaderCompileResult Compile(ShaderCompileRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            IShaderBackend backend = GetBackend(request.Target);
            ShaderCompileCacheKey cacheKey = CreateCacheKey(request);
            ShaderCompileResult cached;
            if (cache.TryGet(cacheKey, out cached)) {
                return cached;
            }

            ShaderCompileResult result = backend.Compile(request, includeResolver);
            cache.Store(cacheKey, result);
            return result;
        }

        /// <summary>
        /// Loads shader source from disk and compiles the requested entry point.
        /// </summary>
        /// <param name="path">Path to the shader source file.</param>
        /// <param name="programName">Logical program name for the entry point.</param>
        /// <param name="entryPoint">Entry point function name.</param>
        /// <param name="stage">Pipeline stage for the entry point.</param>
        /// <param name="target">Backend target to compile for.</param>
        /// <param name="shaderModel">Shader model to compile against.</param>
        /// <param name="variant">Variant name for this compilation.</param>
        /// <param name="defines">Preprocessor defines to apply.</param>
        /// <param name="options">Shared compilation options.</param>
        /// <returns>Compilation result.</returns>
        public ShaderCompileResult CompileFromFile(
            string path,
            string programName,
            string entryPoint,
            ShaderStage stage,
            ShaderCompileTarget target,
            ShaderModel shaderModel,
            string variant,
            IReadOnlyList<ShaderDefine> defines,
            ShaderCompileOptions options) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Shader path must be provided.", nameof(path));
            }

            if (!File.Exists(path)) {
                throw new FileNotFoundException("Shader source file does not exist.", path);
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Shader source directory could not be resolved.");
            }

            ContentManager contentManager = new ContentManager(directory);
            TextContent sourceContent = contentManager.Load<TextContent>(path);
            string source = sourceContent.Text;
            ShaderSourceInfo sourceInfo = new ShaderSourceInfo(path, source);
            ShaderCompileRequest request = new ShaderCompileRequest(
                sourceInfo,
                programName,
                entryPoint,
                stage,
                target,
                shaderModel,
                variant,
                defines,
                options);
            return Compile(request);
        }

        /// <summary>
        /// Returns the registered backend for the requested target.
        /// </summary>
        /// <param name="target">Compilation target.</param>
        /// <returns>Registered backend compiler.</returns>
        IShaderBackend GetBackend(ShaderCompileTarget target) {
            IShaderBackend backend;
            if (backends.TryGetValue(target, out backend)) {
                return backend;
            }

            throw new InvalidOperationException("No shader backend is registered for the requested target.");
        }

        /// <summary>
        /// Builds a cache key for the provided compile request.
        /// </summary>
        /// <param name="request">Compile request to describe.</param>
        /// <returns>Cache key for the request.</returns>
        ShaderCompileCacheKey CreateCacheKey(ShaderCompileRequest request) {
            string sourceHash = sourceHasher.ComputeHash(request.Source.Source);
            string definesSignature = BuildDefinesSignature(request.Defines);
            string bindingSignature = BuildBindingPolicySignature(request.Options.BindingPolicy);
            return new ShaderCompileCacheKey(
                sourceHash,
                request.ProgramName,
                request.EntryPoint,
                request.Stage,
                request.Target,
                request.ShaderModel,
                request.Variant,
                definesSignature,
                bindingSignature);
        }

        /// <summary>
        /// Builds a stable signature string for a define list.
        /// </summary>
        /// <param name="defines">Define list to describe.</param>
        /// <returns>Stable define signature string.</returns>
        string BuildDefinesSignature(IReadOnlyList<ShaderDefine> defines) {
            if (defines.Count == 0) {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < defines.Count; i++) {
                ShaderDefine define = defines[i];
                builder.Append(define.Name);
                builder.Append('=');
                builder.Append(define.Value);
                builder.Append(';');
            }

            return builder.ToString();
        }

        /// <summary>
        /// Builds a stable signature string for a binding policy.
        /// </summary>
        /// <param name="policy">Binding policy to describe.</param>
        /// <returns>Stable binding policy signature string.</returns>
        string BuildBindingPolicySignature(ShaderBindingPolicy policy) {
            return string.Concat(
                policy.DefaultSpace.ToString(),
                ":b", policy.ConstantBufferShift.ToString(),
                ":t", policy.TextureShift.ToString(),
                ":s", policy.SamplerShift.ToString(),
                ":u", policy.StorageShift.ToString());
        }
    }
}
