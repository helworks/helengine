namespace helengine.editor {
    /// <summary>
    /// Builds the runtime script modules for editor Play, but only when a script input changed. Moving an object or
    /// otherwise editing a scene must not rebuild scripts, so the builder fingerprints exactly the inputs that change the
    /// compiled modules: every <c>.cs</c> source and <c>code.module.json</c> manifest under the project's assets, the
    /// resolved gameplay symbol set for the platform and environment, and the engine assemblies the modules reference.
    /// A matching fingerprint with every module assembly present skips the compile; any mismatch deletes the previous
    /// Play output (so assemblies of removed modules can never be loaded) and compiles from scratch, and the fingerprint
    /// is stored only after a successful compile.
    /// </summary>
    public sealed class EditorPlayScriptBuilder : IEditorPlayScriptBuilder {
        /// <summary>
        /// File name of the stored fingerprint inside the Play output root.
        /// </summary>
        public const string FingerprintFileName = "play-script-build.fingerprint";

        /// <summary>
        /// Version token mixed into every fingerprint; bump it when the fingerprint inputs change meaning.
        /// </summary>
        const string FingerprintVersionToken = "play-script-fingerprint=1";

        /// <summary>
        /// Project-relative folder that holds every authored script and code module manifest.
        /// </summary>
        const string AssetsFolderName = "assets";

        /// <summary>
        /// File name of folder-scoped code module manifests.
        /// </summary>
        const string ModuleManifestFileName = "code.module.json";

        /// <summary>
        /// Absolute authored project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Performs the actual compile when the fingerprint is stale.
        /// </summary>
        readonly IEditorPlayScriptCompiler Compiler;

        /// <summary>
        /// Resolves the selected project environment for a platform; the same environment the Play cook uses.
        /// </summary>
        readonly Func<string, string> EnvironmentIdResolver;

        /// <summary>
        /// Discovers the project's code modules to know which module assemblies a valid build must contain.
        /// </summary>
        readonly EditorCodeModuleManifestService ModuleManifestService;

        /// <summary>
        /// Initializes the Play script builder.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative authored project root.</param>
        /// <param name="compiler">Compiler that builds and publishes the runtime modules.</param>
        /// <param name="environmentIdResolver">Resolves the selected environment id for a platform; empty means none.</param>
        /// <exception cref="ArgumentException">Thrown when the project root is blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the compiler or environment resolver is null.</exception>
        public EditorPlayScriptBuilder(string projectRootPath, IEditorPlayScriptCompiler compiler, Func<string, string> environmentIdResolver) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            Compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            EnvironmentIdResolver = environmentIdResolver ?? throw new ArgumentNullException(nameof(environmentIdResolver));
            ModuleManifestService = new EditorCodeModuleManifestService(ProjectRootPath);
        }

        /// <summary>
        /// Returns the up-to-date runtime modules for the platform, compiling only when a script input changed.
        /// </summary>
        /// <param name="platformId">Platform whose gameplay symbols the scripts compile against.</param>
        /// <param name="runtimeModulesRootPath">Receives the runtime module root; it exists on disk after success.</param>
        /// <returns>Success when the modules are current or were rebuilt, otherwise the compile failure.</returns>
        /// <exception cref="ArgumentException">Thrown when the platform id is blank.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the environment resolver or compiler returns nothing.</exception>
        public EditorBuildExecutionResult Build(string platformId, out string runtimeModulesRootPath) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            string environmentId = EnvironmentIdResolver(platformId);
            if (environmentId == null) {
                throw new InvalidOperationException($"No Play environment could be resolved for platform '{platformId}'.");
            }

            string playOutputRootPath = Compiler.ResolvePlayOutputRootPath(platformId);
            string fingerprintFilePath = Path.Combine(playOutputRootPath, FingerprintFileName);
            string fingerprint = EditorScriptBuildFingerprint.Compute(DescribeBuildInputs(platformId, environmentId));
            string expectedModulesRootPath = Compiler.ResolveRuntimeModulesRootPath(platformId);
            if (EditorScriptBuildFingerprint.MatchesStored(fingerprintFilePath, fingerprint)
                && AllRuntimeModuleAssembliesExist(expectedModulesRootPath)) {
                runtimeModulesRootPath = expectedModulesRootPath;
                return EditorBuildExecutionResult.Success("Play scripts are up to date; script build skipped.");
            }

            if (Directory.Exists(playOutputRootPath)) {
                Directory.Delete(playOutputRootPath, true);
            }

            EditorBuildExecutionResult result = Compiler.Build(platformId, environmentId, out runtimeModulesRootPath);
            if (result == null) {
                throw new InvalidOperationException("The Play script compiler returned no result.");
            } else if (!result.Succeeded) {
                return result;
            }

            Directory.CreateDirectory(runtimeModulesRootPath);
            EditorScriptBuildFingerprint.WriteStored(fingerprintFilePath, fingerprint);
            return result;
        }

        /// <summary>
        /// Describes every input that changes the compiled Play modules for one platform and environment.
        /// </summary>
        /// <param name="platformId">Platform whose gameplay symbols are defined.</param>
        /// <param name="environmentId">Selected project environment, or empty when none is selected.</param>
        /// <returns>Fingerprint inputs: manifests by content, sources by stamp, engine assemblies by stamp, and symbols.</returns>
        EditorScriptBuildInputs DescribeBuildInputs(string platformId, string environmentId) {
            string assetsRootPath = Path.Combine(ProjectRootPath, AssetsFolderName);
            string[] manifestFilePaths = Directory.Exists(assetsRootPath)
                ? Directory.GetFiles(assetsRootPath, ModuleManifestFileName, SearchOption.AllDirectories)
                : Array.Empty<string>();
            IReadOnlyList<string> gameplaySymbols = EditorPlatformPreprocessorSymbolService.ResolveGameplaySymbols(platformId, environmentId);
            List<string> tokens = new List<string>(gameplaySymbols.Count + 3) {
                FingerprintVersionToken,
                "platform=" + platformId,
                "environment=" + environmentId
            };
            for (int index = 0; index < gameplaySymbols.Count; index++) {
                tokens.Add("symbol=" + gameplaySymbols[index]);
            }

            return new EditorScriptBuildInputs(
                manifestFilePaths,
                new[] { assetsRootPath },
                EditorGameSolutionService.ResolveRuntimeEngineAssemblyPaths(),
                tokens);
        }

        /// <summary>
        /// Checks that the module root exists and holds the assembly of every runtime module the project declares.
        /// </summary>
        /// <param name="runtimeModulesRootPath">Runtime module root laid out as <c>&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c>.</param>
        /// <returns>True when a previous build left every expected runtime module assembly in place.</returns>
        bool AllRuntimeModuleAssembliesExist(string runtimeModulesRootPath) {
            if (!Directory.Exists(runtimeModulesRootPath)) {
                return false;
            }

            EditorCodeModuleManifestEntry[] modules = ModuleManifestService.Load().Modules;
            for (int index = 0; index < modules.Length; index++) {
                if (modules[index].ModuleKind != EditorCodeModuleKind.Runtime) {
                    continue;
                }

                string moduleId = modules[index].ModuleId;
                if (!File.Exists(Path.Combine(runtimeModulesRootPath, moduleId, moduleId + ".dll"))) {
                    return false;
                }
            }

            return true;
        }
    }
}
