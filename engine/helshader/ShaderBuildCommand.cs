using helengine;

namespace helshader {
    /// <summary>
    /// Executes shader build commands based on a manifest.
    /// </summary>
    public class ShaderBuildCommand {
        /// <summary>
        /// Runs the build command using the provided options.
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

            if (options.Clean) {
                CleanOutputDirectories(paths);
            }

            PrepareOutputDirectories(paths);

            ShaderManifestShader[] shaders = FilterShaders(manifest, options, paths.RootPath);
            string[] targets = SelectTargets(manifest, options);

            ShaderCompiler compiler = new ShaderCompiler(manifest.Tools);
            ShaderProfileResolver profileResolver = new ShaderProfileResolver();
            ShaderDefineBuilder defineBuilder = new ShaderDefineBuilder();
            ShaderStageResolver stageResolver = new ShaderStageResolver();
            ShaderOutputNamer outputNamer = new ShaderOutputNamer();

            for (int shaderIndex = 0; shaderIndex < shaders.Length; shaderIndex++) {
                ShaderManifestShader shader = shaders[shaderIndex];
                ShaderManifestVariant[] variants = FilterVariants(shader, options);
                string sourcePath = Path.Combine(paths.RootPath, shader.File);
                if (!File.Exists(sourcePath)) {
                    throw new FileNotFoundException($"Shader source file was not found: {sourcePath}", sourcePath);
                }
                string[] includeDirs = ResolveIncludeDirs(paths.RootPath, manifest.IncludeDirs);

                for (int entryIndex = 0; entryIndex < shader.Entries.Length; entryIndex++) {
                    ShaderManifestEntryPoint entry = shader.Entries[entryIndex];
                    ShaderStage stage = stageResolver.Parse(entry.Stage);
                    string profile;

                    for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++) {
                        string target = targets[targetIndex];
                        profile = profileResolver.ResolveProfile(manifest, target, stage);

                        for (int variantIndex = 0; variantIndex < variants.Length; variantIndex++) {
                            ShaderManifestVariant variant = variants[variantIndex];
                            string[] defines = defineBuilder.BuildDefines(variant, target, options.Defines);
                            string outputName = outputNamer.GetBinaryFileName(shader.Name, stage, target, variant.Name);
                            string outputPath = Path.Combine(paths.BinaryDir, outputName);

                            ShaderCompileItem item = new ShaderCompileItem(
                                shader.Name,
                                sourcePath,
                                outputPath,
                                entry.Entry,
                                profile,
                                stage,
                                target,
                                variant.Name,
                                defines,
                                includeDirs);

                            if (options.Verbose) {
                                Console.WriteLine($"Compiling {shader.Name} {entry.Entry} {target} {variant.Name} -> {outputPath}");
                            }

                            compiler.Compile(item, paths.RootPath);
                        }
                    }
                }

                if (options.EmitModules) {
                    GenerateModule(shader, variants, targets, paths);
                }
            }
        }

        /// <summary>
        /// Ensures output directories exist.
        /// </summary>
        /// <param name="paths">Resolved output paths.</param>
        void PrepareOutputDirectories(ShaderPathInfo paths) {
            Directory.CreateDirectory(paths.BinaryDir);
            Directory.CreateDirectory(paths.ReflectionDir);
            Directory.CreateDirectory(paths.CodegenDir);
            Directory.CreateDirectory(paths.ModuleDir);
            Directory.CreateDirectory(paths.MslDir);
            Directory.CreateDirectory(paths.DebugDir);
        }

        /// <summary>
        /// Deletes output directories before rebuilding.
        /// </summary>
        /// <param name="paths">Resolved output paths.</param>
        void CleanOutputDirectories(ShaderPathInfo paths) {
            DeleteDirectory(paths.BinaryDir);
            DeleteDirectory(paths.ReflectionDir);
            DeleteDirectory(paths.CodegenDir);
            DeleteDirectory(paths.ModuleDir);
            DeleteDirectory(paths.MslDir);
            DeleteDirectory(paths.DebugDir);
        }

        /// <summary>
        /// Deletes a directory if it exists.
        /// </summary>
        /// <param name="path">Directory path.</param>
        void DeleteDirectory(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return;
            }

            if (!Directory.Exists(path)) {
                return;
            }

            Directory.Delete(path, true);
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
        /// Resolves include directories for compilation.
        /// </summary>
        /// <param name="rootPath">Shader root path.</param>
        /// <param name="includeDirs">Include directory list.</param>
        /// <returns>Resolved include directories.</returns>
        string[] ResolveIncludeDirs(string rootPath, string[] includeDirs) {
            if (includeDirs == null || includeDirs.Length == 0) {
                return Array.Empty<string>();
            }

            List<string> resolved = new List<string>();
            for (int i = 0; i < includeDirs.Length; i++) {
                string dir = includeDirs[i];
                if (string.IsNullOrWhiteSpace(dir)) {
                    continue;
                }

                if (Path.IsPathRooted(dir)) {
                    resolved.Add(Path.GetFullPath(dir));
                    continue;
                }

                resolved.Add(Path.GetFullPath(Path.Combine(rootPath, dir)));
            }

            return resolved.ToArray();
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
            ShaderModuleCodegenResult codeResult = codeGenerator.Generate(moduleData, paths.CodegenDir);

            ShaderModuleCompiler compiler = new ShaderModuleCompiler();
            string outputPath = Path.Combine(paths.ModuleDir, $"{shader.Name}.shader.dll");
            ShaderModuleCompilationRequest request = new ShaderModuleCompilationRequest(codeResult.SourcePath, outputPath);
            ShaderModuleCompilationResult result = compiler.Compile(request);
            if (!result.Success) {
                throw new InvalidOperationException($"Module compilation failed: {string.Join(Environment.NewLine, result.Diagnostics)}");
            }
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
