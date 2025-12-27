namespace helshader {
    /// <summary>
    /// Generates shader module code from reflection data without compiling binaries.
    /// </summary>
    public class ShaderCodegenCommand {
        /// <summary>
        /// Runs the code generation command using the provided options.
        /// </summary>
        /// <param name="options">Command options.</param>
        public void Execute(ShaderCommandOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.ManifestPath)) {
                throw new InvalidOperationException("Manifest path is required.");
            }

            string manifestPath = Path.GetFullPath(options.ManifestPath);
            ShaderManifestLoader loader = new ShaderManifestLoader();
            ShaderManifest manifest = loader.Load(manifestPath);
            ShaderPathResolver pathResolver = new ShaderPathResolver();
            ShaderPathInfo paths = pathResolver.Resolve(manifestPath, manifest);

            Directory.CreateDirectory(paths.CodegenDir);
            Directory.CreateDirectory(paths.ModuleDir);

            ShaderManifestShader[] shaders = FilterShaders(manifest, options, paths.RootPath);
            string[] targets = SelectTargets(manifest, options);

            for (int shaderIndex = 0; shaderIndex < shaders.Length; shaderIndex++) {
                ShaderManifestShader shader = shaders[shaderIndex];
                ShaderManifestVariant[] variants = FilterVariants(shader, options);
                GenerateModule(shader, variants, targets, paths);
            }
        }

        /// <summary>
        /// Filters shaders based on command options.
        /// </summary>
        /// <param name="manifest">Shader manifest.</param>
        /// <param name="options">Command options.</param>
        /// <returns>Filtered shader list.</returns>
        ShaderManifestShader[] FilterShaders(ShaderManifest manifest, ShaderCommandOptions options, string rootPath) {
            List<ShaderManifestShader> results = new List<ShaderManifestShader>();

            for (int i = 0; i < manifest.Shaders.Length; i++) {
                ShaderManifestShader shader = manifest.Shaders[i];
                if (!MatchesShaderFilter(shader, options, rootPath)) {
                    continue;
                }

                results.Add(shader);
            }

            if (results.Count == 0) {
                throw new InvalidOperationException("No shaders matched the requested filters.");
            }

            return results.ToArray();
        }

        /// <summary>
        /// Determines whether a shader matches the provided filters.
        /// </summary>
        /// <param name="shader">Shader entry.</param>
        /// <param name="options">Command options.</param>
        /// <returns>True when the shader matches.</returns>
        bool MatchesShaderFilter(ShaderManifestShader shader, ShaderCommandOptions options, string rootPath) {
            if (!string.IsNullOrWhiteSpace(options.ShaderName) &&
                !string.Equals(shader.Name, options.ShaderName, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ShaderFile)) {
                string file = NormalizePath(GetShaderFilePath(rootPath, shader.File));
                string requested = NormalizePath(GetShaderFilePath(rootPath, options.ShaderFile));
                if (!string.Equals(file, requested, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Filters variants based on command options.
        /// </summary>
        /// <param name="shader">Shader entry.</param>
        /// <param name="options">Command options.</param>
        /// <returns>Filtered variant list.</returns>
        ShaderManifestVariant[] FilterVariants(ShaderManifestShader shader, ShaderCommandOptions options) {
            if (string.IsNullOrWhiteSpace(options.Variant)) {
                return shader.Variants;
            }

            List<ShaderManifestVariant> results = new List<ShaderManifestVariant>();
            for (int i = 0; i < shader.Variants.Length; i++) {
                ShaderManifestVariant variant = shader.Variants[i];
                if (string.Equals(variant.Name, options.Variant, StringComparison.OrdinalIgnoreCase)) {
                    results.Add(variant);
                }
            }

            if (results.Count == 0) {
                throw new InvalidOperationException($"Variant '{options.Variant}' was not found for shader '{shader.Name}'.");
            }

            return results.ToArray();
        }

        /// <summary>
        /// Selects target backends based on command options.
        /// </summary>
        /// <param name="manifest">Shader manifest.</param>
        /// <param name="options">Command options.</param>
        /// <returns>Target list.</returns>
        string[] SelectTargets(ShaderManifest manifest, ShaderCommandOptions options) {
            if (options.AllTargets || string.IsNullOrWhiteSpace(options.Target)) {
                return manifest.Targets;
            }

            if (!TargetExists(manifest.Targets, options.Target)) {
                throw new InvalidOperationException($"Target '{options.Target}' is not defined in the manifest.");
            }

            return new[] { options.Target };
        }

        /// <summary>
        /// Generates shader module code and compiles it into a DLL.
        /// </summary>
        /// <param name="shader">Shader manifest entry.</param>
        /// <param name="variants">Variant list.</param>
        /// <param name="targets">Target list.</param>
        /// <param name="paths">Resolved output paths.</param>
        void GenerateModule(ShaderManifestShader shader, ShaderManifestVariant[] variants, string[] targets, ShaderPathInfo paths) {
            ShaderModuleDataBuilder builder = new ShaderModuleDataBuilder();
            ShaderModuleData moduleData = builder.Build(shader, variants, targets, paths);

            ShaderModuleCodeGenerator codeGenerator = new ShaderModuleCodeGenerator();
            codeGenerator.Generate(moduleData, paths.CodegenDir);
        }

        /// <summary>
        /// Normalizes a path for comparison.
        /// </summary>
        /// <param name="path">Path string.</param>
        /// <returns>Normalized path.</returns>
        string NormalizePath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/');
            return normalized.Trim();
        }

        /// <summary>
        /// Resolves a shader file path relative to the root when needed.
        /// </summary>
        /// <param name="rootPath">Shader root path.</param>
        /// <param name="filePath">Shader file path.</param>
        /// <returns>Absolute shader file path.</returns>
        string GetShaderFilePath(string rootPath, string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                return string.Empty;
            }

            if (Path.IsPathRooted(filePath)) {
                return Path.GetFullPath(filePath);
            }

            return Path.GetFullPath(Path.Combine(rootPath, filePath));
        }

        /// <summary>
        /// Determines whether a target exists in the manifest list.
        /// </summary>
        /// <param name="targets">Manifest target list.</param>
        /// <param name="target">Target to locate.</param>
        /// <returns>True when the target exists.</returns>
        bool TargetExists(string[] targets, string target) {
            for (int i = 0; i < targets.Length; i++) {
                if (string.Equals(targets[i], target, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }
    }
}
